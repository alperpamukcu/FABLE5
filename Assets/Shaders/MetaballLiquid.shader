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
        _EdgeColor    ("Edge Color",   Color) = (1.0, 1.0, 1.0, 1.0)
        _Threshold    ("Threshold",    Range(0.01, 4)) = 0.60
        _EdgeWidth    ("Edge Width",   Range(0.001, 1.0)) = 0.18
        // How far the meniscus lifts toward _EdgeColor, and how strongly it is applied.
        // Until 2026-08-03 the edge blended 85% of the way to PURE WHITE, which at this
        // scale drew a one-pixel white frame around the pool, the falling stream and every
        // single droplet (the author: "sıvıların etrafında çok küçük pixele sahip beyaz
        // çerçeve var"). A meniscus is the drink catching the light, so it now lifts the
        // drink's OWN colour: a rum rims amber, a curacao rims pale blue.
        _EdgeTint     ("Edge Tint",    Range(0, 1)) = 0.30
        _EdgeStrength ("Edge Strength",Range(0, 1)) = 0.55
        // How solid a FREE-FALLING drop is against the settled body. A stream in mid-air
        // has nothing behind it, where the drink in a glass is read through a translucent
        // wall — so drawing both at the body's alpha made the pour look like paint.
        _StreamAlpha  ("Stream Alpha", Range(0.1, 1)) = 0.55
        _Highlight    ("Top Highlight",Range(0, 1)) = 0.35
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

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            fixed4 _EdgeColor;
            float  _Threshold;
            float  _EdgeWidth;
            float  _EdgeTint;
            float  _EdgeStrength;
            float  _StreamAlpha;
            float  _Highlight;
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
            void dropFields (float2 uv, out float total, out float foam, out float stream)
            {
                total  = 0.0;
                foam   = 0.0;
                stream = 0.0;
                int n = (int)_DropCount;
                for (int i = 0; i < MAX_DROPS; i++)
                {
                    if (i >= n) break;
                    float4 d = _Drops[i];
                    if (d.w < 0.5) continue;
                    float2 du  = uv - d.xy;
                    float2 dpx = float2(du.x * _Size.x, du.y * _Size.y);   // to pixels -> circular
                    float  dist2 = dot(dpx, dpx);
                    float  r = max(d.z, 0.001);
                    float  t = saturate(1.0 - dist2 / (r * r));
                    float  c = t * t;   // squared -> soft shoulders that fuse when overlapping
                    total += c;
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

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 uv  = IN.texcoord;
                float2 ruv = rotUv(uv);                     // the pool's tilted frame
                float dropTotal, dropFoam, dropStream;
                dropFields(uv, dropTotal, dropFoam, dropStream);
                float field = poolField(ruv) + dropTotal;

                // Antialiased threshold edge from the field's screen-space rate of change.
                float aa = fwidth(field) + 1e-4;
                float a  = smoothstep(_Threshold - aa, _Threshold + aa, field);
                if (a <= 0.001) discard;

                // How much of this pixel is foam rather than beer. Scaled well past 1 so the
                // boundary is a short blend and not a long muddy gradient: a head has a definite
                // underside, and a soft fade across it was most of why the foam read as a smudge.
                float fm = saturate(dropFoam / max(dropTotal, 1e-4) * 2.2);

                // How much of this pixel is liquid still IN THE AIR. Where the stream owns the
                // field it wears its own colour; where the settled body owns it, the glass's
                // colour wins. The crossover is the field itself, so a pour mixes on the way in
                // — no frame where the whole drink changes colour at once.
                float st = saturate(dropStream / max(dropTotal, 1e-4));

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
                fixed3 lift = lerp(body.rgb, _EdgeColor.rgb, _EdgeTint);
                fixed4 col = body;
                col.rgb = lerp(body.rgb, lift, rim * _EdgeStrength * (1.0 - fm * 0.75));

                // A bright band riding just under the moving water line — the light on the
                // surface — plus a soft sheen through the body so it reads as wet volume.
                // Foam is matte and full of air: it takes almost none of that wet sheen, which is
                // most of what stops it reading as pale beer.
                float surf = surfaceY(ruv.x);
                float band = saturate(1.0 - abs(ruv.y - surf) * 26.0);
                float sheen = saturate((ruv.y - surf) * 5.0 + 0.5);
                col.rgb += (band * 0.5 + sheen * 0.18) * _Highlight * (1.0 - fm * 0.85);

                // Liquid in the air is thinner than liquid at rest. A pixel owned entirely by
                // free-falling drops draws at _StreamAlpha of its own alpha; as the stream
                // lands, the settled particles take over the field and it fills in on its own,
                // so there is no moment where the pour changes into something else.
                float bodyA = lerp(lerp(_Color.a, _StreamColor.a, st), _FoamColor.a, fm);
                col.a = a * bodyA * lerp(1.0, _StreamAlpha, st) * IN.color.a;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
