// Flight Beyond the Stars - spinning planet globe. Drawn on a square sprite: the globe fills the middle 85%,
// the rest is atmosphere glow. _MapTex is an equirectangular (2:1) map; it is wrapped onto a sphere and turned
// slowly (_Spin), lit from the star (_LightDir: x,y toward the star on screen, z toward the viewer).
Shader "FBTS/Planet"
{
    Properties
    {
        // Not called _MainTex: a SpriteRenderer overwrites _MainTex with its own sprite.
        _MapTex ("Planet map (equirectangular)", 2D) = "white" {}
        _LightDir ("Direction to the star", Vector) = (1, 0, 0.45, 0)
        _Atmo ("Atmosphere colour", Color) = (0.5, 0.7, 1, 0.6)
        _Spin ("Spin speed (turns per second)", Float) = 0.005
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            TEXTURE2D(_MapTex);
            SAMPLER(sampler_MapTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MapTex_ST;
                float4 _LightDir;
                float4 _Atmo;
                float _Spin;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                const float R = 0.85;
                float3 L = normalize(_LightDir.xyz);

                // Thin glow outside the globe (and under its soft edge), brighter toward the star
                float d = saturate((r - R) / (1.0 - R));
                float2 dir = p / max(r, 1e-4);
                float facing = saturate(dot(dir, normalize(L.xy + 1e-4)) * 0.5 + 0.5);
                float haloA = pow(1.0 - d, 3.0) * _Atmo.a * (0.15 + 0.6 * facing);

                float edge = 1.0 - smoothstep(R - 0.02, R, r);
                half3 col = _Atmo.rgb;
                if (edge > 0.0)
                {
                    float2 q = p / R;
                    float z = sqrt(saturate(1.0 - dot(q, q)));
                    float3 nrm = float3(q.x, q.y, z);
                    float lon = atan2(q.x, z) / 6.2831853 + _Time.y * _Spin;
                    float lat = asin(clamp(q.y, -1.0, 1.0)) / 3.14159265;
                    half3 surf = SAMPLE_TEXTURE2D_LOD(_MapTex, sampler_MapTex, float2(frac(lon + 0.5), lat + 0.5), 0).rgb;
                    float light = smoothstep(-0.15, 0.35, dot(nrm, L));   // soft day/night line
                    surf *= 0.06 + 0.94 * light;
                    surf += _Atmo.rgb * pow(1.0 - z, 3.0) * _Atmo.a * (0.25 + 0.75 * light);   // atmosphere at the limb
                    col = lerp(_Atmo.rgb, surf, edge);
                }
                float alpha = edge + (1.0 - edge) * haloA;
                return half4(col, alpha * i.color.a);
            }
            ENDHLSL
        }
    }
}
