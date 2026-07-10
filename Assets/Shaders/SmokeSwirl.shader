// LAST CALL — cozy noir animated backdrop (GDD 12.1): slow swirling bar smoke in
// deep purples with warm amber light and a faint magenta neon wash. Runs on a
// fullscreen RawImage, so it stays a plain canvas-compatible unlit shader.
Shader "LastCall/UI/SmokeSwirl"
{
    Properties
    {
        _ColorDeep ("Deep Purple", Color) = (0.055, 0.04, 0.10, 1)
        _ColorSmoke ("Smoke Purple", Color) = (0.16, 0.11, 0.24, 1)
        _ColorAmber ("Bar Light Amber", Color) = (0.55, 0.33, 0.12, 1)
        _ColorNeon ("Neon Magenta", Color) = (0.45, 0.12, 0.35, 1)
        _Speed ("Swirl Speed", Range(0.01, 0.5)) = 0.06
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorDeep, _ColorSmoke, _ColorAmber, _ColorNeon;
            float _Speed;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, amp = 0.55;
                for (int i = 0; i < 4; i++)
                {
                    v += amp * noise(p);
                    p = p * 2.03 + 17.0;
                    amp *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = i.uv * float2(1.78, 1.0) * 2.2; // roughly square cells on 16:9

                // Slow rotation around screen center plus domain-warped fbm = lazy smoke.
                float2 c = uv - float2(1.78 * 1.1, 1.1);
                float angle = t * 0.35;
                float s = sin(angle), co = cos(angle);
                c = float2(c.x * co - c.y * s, c.x * s + c.y * co);

                float2 warp = float2(fbm(c + t), fbm(c - t * 0.7 + 5.2));
                float smoke = fbm(c + 1.6 * warp);
                float wisps = fbm(c * 2.1 - warp * 1.2 - t * 0.5);

                fixed3 col = lerp(_ColorDeep.rgb, _ColorSmoke.rgb, smoothstep(0.25, 0.85, smoke));
                // A warm pool of bar light low on the screen.
                float lamp = pow(saturate(1.0 - distance(i.uv, float2(0.5, 0.12)) * 1.15), 2.0);
                col += _ColorAmber.rgb * lamp * (0.55 + 0.45 * wisps);
                // Faint neon wash drifting through the upper haze.
                col += _ColorNeon.rgb * smoothstep(0.62, 0.95, wisps) * 0.25 * i.uv.y;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
