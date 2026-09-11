Shader "LastCall/MetaballLiquid"
{
    // A 2D metaball fluid for the drink stages (GDD 24 §3.5, 2026-07-22). It draws over a
    // UI rect (a RawImage stretched across the pour surface): every falling droplet and the
    // pooled liquid contribute to one scalar field, and the field is thresholded so nearby
    // blobs melt into a single smooth mass instead of reading as separate balls. The pooled
    // body is a soft-topped rectangle clipped to the glass interior, so the liquid "fills the
    // glass" and rising volume is just a rising surface line. Fed entirely by MetaballFluid.cs.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color        ("Liquid Color", Color) = (0.30, 0.60, 1.0, 0.95)
        _FoamColor    ("Foam Color",   Color) = (0.97, 0.94, 0.87, 1.0)
        // What is being poured RIGHT NOW, which is not what is in the glass. One colour for
        // the whole body meant a cola falling into a vodka was drawn in the average of the
        // two before it had touched the surface, and the average jumped as the ratio moved —
        // the mixing happened in the palette instead of in the glass.
        _StreamColor  ("Stream Color", Color) = (0.30, 0.60, 1.0, 0.95)
        _Threshold    ("Threshold",    Range(0.01, 4)) = 0.60
        _EdgeWidth    ("Edge Width",   Range(0.001, 1.0)) = 0.18
        // How strongly the meniscus lands. Until 2026-08-03 the edge blended 85% of the way
        // to PURE WHITE, which at this scale drew a one-pixel white frame around the pool,
        // the falling stream and every single droplet (the author: "sıvıların etrafında çok
        // küçük pixele sahip beyaz çerçeve var"). A meniscus is the drink catching the
        // light, so the rim now LIFTS the drink's own channels — a rum rims amber, a
        // curacao rims pale blue — and there is no white to blend toward any more.
        _EdgeStrength ("Edge Strength",Range(0, 1)) = 0.55
        // How solid a FREE-FALLING drop is against the settled body. A stream in mid-air
        // has nothing behind it, where the drink in a glass is read through a translucent
        // wall — so drawing both at the body's alpha made the pour look like paint.
        _StreamAlpha  ("Stream Alpha", Range(0.1, 1)) = 0.55
        _Highlight    ("Top Highlight",Range(0, 1)) = 0.35
        // THE PIXEL-ART LIQUID (2026-09-11, the author: "dökülen sıvıların sıvı dokusu olması
        // için bir yol düşünelim, dümdüz boyalı alan gibi gözükmesin"). The size of one liquid
        // texel in UI units: 0 keeps the smooth look this shader had before, anything above it
        // turns on the textured one — value bands, an ordered dither, light from the field's own
        // gradient, depth, a meniscus and bubbles where the drink moves. See frag.
        _Texel        ("Texel (UI units, 0 = smooth)", Float) = 0
        _ViewOrigin   ("View Origin px", Vector) = (0, 0, 0, 0)
        _FlowT        ("Flow Clock", Float) = 0
        _DepthPx      ("Depth Ramp px", Float) = 150
        _Size         ("Rect Size px", Vector) = (600, 400, 0, 0)
        _DropCount    ("Drop Count",   Float) = 0
        _PoolMinX     ("Pool Min X",   Float) = 0
        _PoolMaxX     ("Pool Max X",   Float) = 0
        _PoolTopY     ("Pool Top Y",   Float) = 0
        _PoolBottomY  ("Pool Bottom Y",Float) = 0
        _PoolEdgeSoft ("Pool Edge Soft",Float) = 0.03
        _PoolStrength ("Pool Strength",Float) = 1.40

        // Standard UI stencil plumbing (lets the fluid live under a Mask if ever needed).
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        CGINCLUDE
            #include "UnityCG.cginc"

            #define MAX_DROPS 2176

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _FoamColor;
            fixed4 _StreamColor;
            float  _Threshold;
            float  _EdgeWidth;
            float  _EdgeStrength;
            float  _StreamAlpha;
            float  _Highlight;
            float  _Texel;
            float4 _ViewOrigin;    // the viewport's centre in surface px, so the texel grid is the world's
            float  _FlowT;         // advances only while the drink moves: nothing here ever animates at rest
            float  _DepthPx;
            float4 _Size;          // xy = rect size in px
            float  _DropCount;
            float  _PoolMinX;
            float  _PoolMaxX;
            float  _PoolTopY;
            float  _PoolBottomY;
            float  _PoolEdgeSoft;
            float  _PoolStrength;
            float4 _Drops[MAX_DROPS];   // xy = uv position, z = radius px, w = active flag

            // Live water surface (2026-07-22): the top of the pool is not a flat line but a
            // real wave. A height-field of columns (a shallow-water sim in MetaballFluid.cs)
            // carries travelling ripples that reflect off the glass walls, and a bulk lateral
            // tilt carries the slosh of a moving glass.
            #define HEIGHT_N 48
            float _SurfTilt;              // uv height gained per uv.x away from centre (slosh)
            float _SurfCenterX;           // uv x the tilt pivots around (pool centre)
            float _Heights[HEIGHT_N];     // uv surface displacement across the pool width
            float _HeightCount;
            // Pool rotation (2026-07-23): the pooled body tilts WITH the shaker so the liquid
            // and the tin read as one mass while you throw it about. The falling stream stays
            // vertical (it is not rotated).
            float _PoolAngle;             // radians
            float _PoolPivotY;            // uv y of the pool centre (rotation pivot with _SurfCenterX)

            // Rotates a uv into the pool's own (tilted) frame, in aspect-correct pixels.
            float2 rotUv (float2 uv)
            {
                float2 pivot = float2(_SurfCenterX, _PoolPivotY);
                float2 p = (uv - pivot) * _Size.xy;
                float c = cos(-_PoolAngle), s = sin(-_PoolAngle);
                float2 pr = float2(p.x * c - p.y * s, p.x * s + p.y * c);
                return pr / max(_Size.xy, float2(1, 1)) + pivot;
            }

            // The liquid line at a given uv.x: the resting level, tilted by the slosh and
            // displaced by the sampled height-field wave.
            float surfaceY (float ux)
            {
                int n = (int)_HeightCount;
                float wave = 0.0;
                if (n > 1)
                {
                    float span = max(_PoolMaxX - _PoolMinX, 1e-4);
                    float t = saturate((ux - _PoolMinX) / span);
                    float f = t * (n - 1);
                    int i0 = (int)floor(f);
                    int i1 = min(i0 + 1, n - 1);
                    wave = lerp(_Heights[i0], _Heights[i1], f - i0);
                }
                return _PoolTopY + _SurfTilt * (ux - _SurfCenterX) + wave;
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color    = v.color;
                return o;
            }

            // Sum of all droplet contributions at a uv, using a compact smooth kernel so the
            // blobs have finite reach and merge cleanly instead of leaving long metaball tails.
            //
            // The w flag says what a blob IS, and each kind is summed a SECOND time into its
            // own accumulator: foam (w≈2) since 2026-07-30, free-falling drops (w≈3) since
            // 2026-08-03. Every kind still feeds the one field that gets thresholded, so beer
            // and its head and the stream landing in it share a single continuous surface —
            // there is no seam and no second object. The accumulators only decide the COLOUR
            // and the ALPHA at each pixel, which is the only way the kinds actually differ.
            void dropFields (float2 uv, out float total, out float foam, out float stream,
                             out float2 grad, out float agit, out float speck)
            {
                total  = 0.0;
                foam   = 0.0;
                stream = 0.0;
                // The field's GRADIENT, summed in the same pass (dc/dp = -4t·p/r²): it is the
                // outward normal of the 2.5D body, which is how the liquid gets light and form
                // without a second loop over every blob.
                grad   = float2(0.0, 0.0);
                // How much of the field is MOVING: each blob's speed rides in the fraction of w.
                agit   = 0.0;
                // A FLECK: a particle flagged by a negative radius lights the one texel it sits
                // in, so the drink carries a scatter of light that moves when it moves.
                speck  = 0.0;
                float halfTexel = _Texel * 0.5;
                int n = (int)_DropCount;
                for (int i = 0; i < MAX_DROPS; i++)
                {
                    if (i >= n) break;
                    float4 d = _Drops[i];
                    if (d.w < 0.5) continue;
                    float2 du  = uv - d.xy;
                    float2 dpx = float2(du.x * _Size.x, du.y * _Size.y);   // to pixels -> circular
                    float  dist2 = dot(dpx, dpx);
                    float  r = max(abs(d.z), 0.001);
                    float  t = saturate(1.0 - dist2 / (r * r));
                    float  c = t * t;   // squared -> soft shoulders that fuse when overlapping
                    total += c;
                    grad  += -4.0 * t * dpx / (r * r);
                    agit  += c * frac(d.w) * 2.04;   // w = kind + speed share x 0.49
                    if (d.z < 0.0 && abs(dpx.x) < halfTexel && abs(dpx.y) < halfTexel) speck = 1.0;
                    // a RANGE, not a floor: with the stream flagged 3, "w > 1.5" would have
                    // coloured every falling drop as foam
                    if (d.w > 1.5 && d.w < 2.5) foam += c;
                    else if (d.w >= 2.5) stream += c;
                }
            }

            // The pooled liquid: clipped to the glass interior, its top a live water surface
            // (tilted + rippled) so falling drops melt into a moving line, not a flat lid.
            float poolField (float2 uv)
            {
                float soft = max(_PoolEdgeSoft, 0.0001);
                float xIn = smoothstep(_PoolMinX - soft, _PoolMinX + soft, uv.x) *
                            (1.0 - smoothstep(_PoolMaxX - soft, _PoolMaxX + soft, uv.x));
                float surf = surfaceY(uv.x);
                float yBelow = 1.0 - smoothstep(surf - soft * 1.5, surf + soft * 1.5, uv.y);
                float yAbove = smoothstep(_PoolBottomY - soft, _PoolBottomY + soft, uv.y);
                return xIn * yBelow * yAbove * _PoolStrength;
            }

            // The whole drink at one point of the viewport (uv 0..1 across it). vertA is the
            // Graphic's own alpha on the canvas pass; the texture pass takes the Graphic's alpha
            // when the texture is shown instead, so it passes 1.
            fixed4 shade (float2 uv, float vertA)
            {
                // THE TEXEL GRID. Snapped in the SURFACE's own pixels, not the viewport's, so the
                // grid belongs to the bench and does not swim as the viewport is refitted each frame.
                float texel = _Texel;
                int2 ti = int2(0, 0);
                if (texel > 0.0)
                {
                    float2 wpx = (uv - 0.5) * _Size.xy + _ViewOrigin.xy;
                    float2 cell = floor(wpx / texel);
                    ti = int2(cell);
                    uv = ((cell + 0.5) * texel - _ViewOrigin.xy) / max(_Size.xy, float2(1, 1)) + 0.5;
                }
                float2 ruv = rotUv(uv);                     // the pool's tilted frame
                float dropTotal, dropFoam, dropStream, dropAgit, dropSpeck;
                float2 grad;
                dropFields(uv, dropTotal, dropFoam, dropStream, grad, dropAgit, dropSpeck);
                float field = poolField(ruv) + dropTotal;

                // The edge: a texel is liquid or it is not in the pixel-art look; the smooth one
                // keeps its antialiased threshold from the field's screen-space rate of change.
                float a;
                if (texel > 0.0) a = step(_Threshold, field);
                else
                {
                    float aa = fwidth(field) + 1e-4;
                    a = smoothstep(_Threshold - aa, _Threshold + aa, field);
                }
                if (a <= 0.001) discard;

                // How much of this pixel is foam rather than beer. Scaled well past 1 so the
                // boundary is a short blend and not a long muddy gradient: a head has a definite
                // underside, and a soft fade across it was most of why the foam read as a smudge.
                // Both shares are taken against the SAME denominator the coverage is
                // thresholded against. They used to divide by dropTotal while `a` thresholded
                // poolField + dropTotal; those agree only because MetaballFluid disables the
                // rectangular pool every frame, and the day anyone revives it the stream and
                // the foam would over-claim the pixel they land on.
                float denom = max(field, 1e-4);
                float fm = saturate(dropFoam / denom * 2.2);

                // How much of this pixel is liquid still IN THE AIR. Where the stream owns the
                // field it wears its own colour; where the settled body owns it, the glass's
                // colour wins. The crossover is the field itself, so a pour mixes on the way in
                // — no frame where the whole drink changes colour at once.
                float st = saturate(dropStream / denom);

                fixed4 body = lerp(_Color, _FoamColor, fm);
                body = lerp(body, _StreamColor, st * (1.0 - fm));

                // Foam is a mass of BUBBLES, so it is shaded by its own field strength: the
                // hollows between bubbles sit back and the crowns catch the light. Without this
                // the head is one flat wash of cream with no form in it at all.
                float bub = saturate(dropFoam / max(_Threshold, 1e-4) - 0.55);
                body.rgb *= lerp(1.0, 0.82 + 0.30 * saturate(bub * 1.4), fm);

                // A bright rim right at the surface tension line — but barely any of it on foam,
                // because white on a cream head erases its edge (2026-07-30).
                //
                // The rim lifts the body's OWN colour toward _EdgeColor rather than replacing it
                // with white. See _EdgeTint: a hard blend to white drew a one-pixel pale frame
                // around the pool, the stream and every droplet, which is what the outline in
                // the screenshots was.
                float rim = 1.0 - saturate((field - _Threshold) / max(_EdgeWidth, 1e-4));
                rim = pow(rim, 1.5);
                // The lift is computed FROM the body, not blended toward a fixed _EdgeColor.
                // The 2026-08-03 pass said it did this and did not: it lerped toward
                // _EdgeColor, whose default is pure white, and nothing in C# ever set that
                // property — so the pale one-pixel frame survived at a fifth of its strength
                // and the comment claiming otherwise was simply wrong. Multiplying the drink's
                // own channels cannot produce white out of a dark drink no matter the tuning.
                fixed4 col = body;
                float bandAlpha = 1.0;
                float surf = surfaceY(ruv.x);
                if (texel > 0.0)
                {
                    // ── THE PIXEL-ART LIQUID ──────────────────────────────────────────────
                    // Five value bands, D2 D1 B0 L1 L2, that MULTIPLY the drink's own channels —
                    // never an add, never a blend toward white (the 2026-08-03 frame). s is in
                    // band units; 2 is the drink's own colour. The texel is the VESSEL's own art
                    // pixel (MetaballFluid.SetPixelGrid): a liquid pixel is a glass pixel.
                    float s = 2.0;
                    float pigment = saturate((_Color.a - 0.55) / 0.40);   // clear 0 .. bodied 1
                    float still = (1.0 - st) * (1.0 - fm);                // the drink itself
                    // HOW FAR IN FROM THE EDGE, in texel rows. (f - T) / |grad f| is the first-order
                    // distance to the iso-line: good within a kernel of it, which is all the edge
                    // shading needs, and true however steeply a packed body makes the field climb
                    // (the first pass read the edge off the field's VALUE, and a packed body
                    // climbs past it inside one texel, so the rim never showed).
                    float2 nrm = -grad;                           // the field climbs inward
                    float nl = length(nrm);
                    float2 nu = nl > 1e-5 ? nrm / nl : float2(0.0, 1.0);
                    float row = (field - _Threshold) / max(nl, 1e-4) / texel;
                    float up = saturate((nu.y - 0.35) / 0.35) * still;  // the drink's top face
                    // THE MENISCUS: the drink's top edge, wherever the particles put it — its
                    // first row catches the light, the next holds some of it.
                    s += up * (row < 1.0 ? 2.0 : (row < 2.0 ? 1.0 : 0.0));
                    // FORM: the sides and the underside, one row wide like a pixel artist's rim —
                    // the face turned to the light (up and left) a band lighter, the one turned
                    // away a band darker. The stream takes it too; it is what makes the rope round.
                    float lam = dot(nu, float2(-0.62, 0.78));
                    if (row < 1.0) s += (1.0 - up) * (lam > 0.25 ? 1.0 : (lam < -0.25 ? -1.0 : 0.0));
                    // DEPTH: a drink thickens the further below its surface you look — a coloured
                    // drink more than a clear one. Not the stream and not the head.
                    float depthPx = (surf - ruv.y) * _Size.y;
                    float deep = smoothstep(0.0, _DepthPx, depthPx) * still;
                    s -= deep * lerp(0.9, 1.6, pigment);
                    // FLECKS: one particle in eleven carries one (MetaballFluid.Upload), so a still
                    // drink holds a still scatter of light and a moving one carries it round — the
                    // flow shows INSIDE the body and not only at its edge. Churned drink adds
                    // more, flickering on the drink's own clock, which only turns while it moves:
                    // a settled glass is the same picture every frame, as the look tests demand.
                    s += dropSpeck * still * (row >= 1.0 ? 1.0 : 0.0);
                    float agit = saturate(dropAgit / denom);
                    float hs = frac(sin(dot(float2(ti) + floor(_FlowT * 12.0), float2(12.9898, 78.233))) * 43758.5453);
                    if (agit > 0.25 && hs < 0.30 * agit) s += 1.0;
                    // QUANTISE. A ramp steps from one band to the next through a thin checker
                    // seam a few texels tall. An ordered dither across the whole ramp (the first
                    // pass) laid a screen door over most of the drink.
                    float thr = ((ti.x + ti.y) & 1) != 0 ? 0.25 : 0.75;
                    float fs = saturate((frac(s) - 0.5) * 6.0 + 0.5);
                    float b = clamp(floor(s) + ((fs > thr) ? 1.0 : 0.0), 0.0, 4.0);
                    float k = b < 0.5 ? 0.58 : (b < 1.5 ? 0.78 : (b < 2.5 ? 1.0 : (b < 3.5 ? 1.18 : 1.34)));
                    // A lift is capped so no channel passes 0.94: chromaticity is kept exactly and
                    // a pale drink can not be pushed to white.
                    if (k > 1.0) k = max(1.0, min(k, 0.94 / max(max(body.r, body.g), max(body.b, 1e-3))));
                    col.rgb = lerp(body.rgb * k, body.rgb, fm);   // foam keeps its own bubbled matte
                    // OPACITY. A thicker column of drink lets less of the bar through — a clear
                    // drink most — and a lit pixel is a DENSER one: the lift above is capped so a
                    // bright drink is never pushed toward white, which leaves an orange juice's
                    // meniscus 7% lighter and invisible; letting less of the dark bar through it
                    // is what makes it read. A clear drink's shaded pixels thicken too, which is
                    // how it is read at all. (Unclamped: the product is clamped once, at the
                    // end — the first pass saturated THIS, so it was always 1.)
                    bandAlpha = (1.0 + (max(b - 2.0, 0.0) * 0.11 + abs(b - 2.0) * 0.08 * (1.0 - pigment)) * (1.0 - fm))
                              * (1.0 + deep * 0.30 * (1.0 - 0.5 * pigment));
                }
                else
                {
                fixed3 lift = saturate(body.rgb * 1.34 + 0.045);
                col.rgb = lerp(body.rgb, lift, rim * _EdgeStrength * (1.0 - fm * 0.75));

                // A glint riding the water line — the light on the surface — plus a soft sheen
                // down through the body so it reads as wet volume rather than a flat wash.
                // Foam is matte and full of air: it takes almost none of that sheen, which is
                // most of what stops it reading as pale beer.
                //
                // This whole term was DEAD until 2026-08-04. Upload() pinned _PoolTopY at 2.0
                // — off the top of a 0..1 uv — to stop the disabled rectangular pool leaving a
                // mark, and that also parked the liquid line out of frame, so band and sheen
                // were identically zero for every pixel of every drink ever drawn. It is fed
                // from the particles' own measured surface now. The band is deliberately tight
                // (one part in 64 rather than 26) and the lift is a MULTIPLY of the drink's own
                // channels, not an add toward white: the last thing this shader needs is
                // another pale line drawn across the top of a drink.
                float band = saturate(1.0 - abs(ruv.y - surf) * 64.0);
                float sheen = saturate((ruv.y - surf) * 5.0 + 0.5);
                float wet = (band * 0.55 + sheen * 0.30) * _Highlight * (1.0 - fm * 0.85);
                col.rgb = lerp(col.rgb, saturate(col.rgb * 1.5 + 0.03), wet);
                }

                // Liquid in the air is thinner than liquid at rest. A pixel owned entirely by
                // free-falling drops draws at _StreamAlpha of its own alpha; as the stream
                // lands, the settled particles take over the field and it fills in on its own,
                // so there is no moment where the pour changes into something else.
                //
                // The stream's share is guarded by (1 - fm) in BOTH the colour and the alpha,
                // and the two lerps are composed in the same order. They were not: at the tap
                // a pull sends stream drops falling THROUGH the head, so a pixel can be foam
                // and stream at once, and the unguarded alpha cut a see-through channel down a
                // head that is meant to be the most opaque thing on screen.
                float sa = st * (1.0 - fm);
                float bodyA = lerp(lerp(_Color.a, _FoamColor.a, fm), _StreamColor.a, sa);
                col.a = saturate(a * bodyA * lerp(1.0, _StreamAlpha, sa) * bandAlpha) * vertA;
                return col;
            }

            fixed4 frag (v2f IN) : SV_Target { return shade(IN.texcoord, IN.color.a); }

            // The texture pass: texcoord runs across the drink's own texture, which covers whole
            // texels a little past the viewport; _RtMap carries it into the viewport's uv.
            float4 _RtMap;
            fixed4 frag_rt (v2f IN) : SV_Target { return shade(IN.texcoord * _RtMap.xy + _RtMap.zw, 1.0); }
        ENDCG

        // 0 — drawn straight onto the canvas, every screen pixel of the viewport. Kept as the
        // path the texture pass replaced; MetaballFluid draws through pass 1.
        Pass
        {
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask [_ColorMask]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }

        // 1 — DRAWN SMALL (2026-09-11, the author: "oyun çok düşük sistemlerde de çalışmalı").
        // Into the drink's own texture at ONE PIXEL PER TEXEL, which the canvas then shows
        // point-sampled: the blob loop runs once per texel instead of once per screen pixel.
        // Written straight (One Zero) into a cleared texture; the canvas does the blending.
        Pass
        {
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_rt
            ENDCG
        }
    }
    Fallback Off
}
