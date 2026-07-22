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
        _EdgeColor    ("Edge Color",   Color) = (1.0, 1.0, 1.0, 1.0)
        _Threshold    ("Threshold",    Range(0.01, 4)) = 0.60
        _EdgeWidth    ("Edge Width",   Range(0.001, 1.0)) = 0.18
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

            #define MAX_DROPS 64

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
            fixed4 _EdgeColor;
            float  _Threshold;
            float  _EdgeWidth;
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
            float dropField (float2 uv)
            {
                float field = 0.0;
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
                    field += t * t;   // squared -> soft shoulders that fuse when overlapping
                }
                return field;
            }

            // The pooled liquid: a rectangle clipped to the glass interior, soft at the top so
            // falling drops melt into the surface as they land.
            float poolField (float2 uv)
            {
                float soft = max(_PoolEdgeSoft, 0.0001);
                float xIn = smoothstep(_PoolMinX - soft, _PoolMinX + soft, uv.x) *
                            (1.0 - smoothstep(_PoolMaxX - soft, _PoolMaxX + soft, uv.x));
                float yBelow = 1.0 - smoothstep(_PoolTopY - soft * 1.5, _PoolTopY + soft * 1.5, uv.y);
                float yAbove = smoothstep(_PoolBottomY - soft, _PoolBottomY + soft, uv.y);
                return xIn * yBelow * yAbove * _PoolStrength;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float field = poolField(uv) + dropField(uv);

                // Antialiased threshold edge from the field's screen-space rate of change.
                float aa = fwidth(field) + 1e-4;
                float a  = smoothstep(_Threshold - aa, _Threshold + aa, field);
                if (a <= 0.001) discard;

                // A bright rim right at the surface tension line.
                float rim = 1.0 - saturate((field - _Threshold) / max(_EdgeWidth, 1e-4));
                rim = pow(rim, 1.5);
                fixed4 col = lerp(_Color, _EdgeColor, rim * 0.85);

                // A soft sheen just under the pooled surface, so the body reads as wet volume.
                float sheen = saturate((uv.y - _PoolTopY) * 6.0 + 0.55) * _Highlight;
                col.rgb += sheen * 0.22;

                col.a = a * _Color.a * IN.color.a;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
