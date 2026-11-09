// Flight Beyond the Stars - star surface. Drawn on a square sprite: the disc fills the middle 60%,
// the rest is a flickering corona. Two layers of the plasma texture drift in different directions and are
// multiplied together, so the surface boils; colour comes from the star's tint (_Color).
Shader "FBTS/Sun"
{
    Properties
    {
        // Not called _MainTex: a SpriteRenderer overwrites _MainTex with its own sprite.
        _PlasmaTex ("Plasma texture", 2D) = "white" {}
        _Color ("Star colour", Color) = (1, 0.8, 0.5, 1)
        _Speed ("Boil speed", Float) = 0.02
        _Corona ("Corona strength", Float) = 0.9
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

            TEXTURE2D(_PlasmaTex);
            SAMPLER(sampler_PlasmaTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _PlasmaTex_ST;
                float4 _Color;
                float _Speed;
                float _Corona;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            half3 Colourise(float lum)
            {
                half3 dark = _Color.rgb * 0.45;
                half3 bright = (_Color.rgb * 0.8 + 0.2) * 1.5;
                return lerp(dark, bright, lum);   // lum may go a little above 1 for hot spots
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;          // -1..1 across the quad
                float r = length(p);
                const float R = 0.6;
                float t = _Time.y * _Speed;

                // Corona: everywhere outside the disc (and under its soft edge), flickering with angle and time
                float d = saturate((r - R) / (1.0 - R));
                float ang = atan2(p.y, p.x) / 6.2831853;
                float n = SAMPLE_TEXTURE2D_LOD(_PlasmaTex, sampler_PlasmaTex, float2(ang * 3.0 + t * 0.6, d * 0.4 - t * 1.5), 0).r;
                float glow = saturate(pow(1.0 - d, 2.6) * (0.55 + 0.7 * n) * _Corona);
                half3 corona = Colourise(0.6);

                // Disc: two drifting layers of plasma multiplied together, bulged like a sphere
                float edge = 1.0 - smoothstep(R - 0.025, R, r);
                half3 disc = corona;
                if (edge > 0.0)
                {
                    float2 q = p / R;
                    float z = sqrt(saturate(1.0 - dot(q, q)));
                    float2 sp = q / (1.0 + z);
                    float a = SAMPLE_TEXTURE2D_LOD(_PlasmaTex, sampler_PlasmaTex, sp * 0.55 + float2(t, t * 0.6), 0).r;
                    float b = SAMPLE_TEXTURE2D_LOD(_PlasmaTex, sampler_PlasmaTex, sp * 0.8 + float2(-t * 0.7, t * 0.45) + 0.37, 0).g;
                    float lum = clamp(0.45 + 0.75 * (a * b * 1.5 - 0.35), 0.0, 1.15);
                    lum *= 0.62 + 0.5 * z;              // limb darkening
                    disc = Colourise(lum);
                }
                half3 col = lerp(corona, disc, edge);
                float alpha = edge + (1.0 - edge) * glow;
                return half4(col, alpha * i.color.a);
            }
            ENDHLSL
        }
    }
}
