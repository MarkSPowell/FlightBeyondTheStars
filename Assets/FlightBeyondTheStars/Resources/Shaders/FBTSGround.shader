// Flight Beyond the Stars - planet surface ground. Drawn on each streamed terrain section's sprite.
// _MainTex (the sprite) is the section's colour map: one texel per km, from PlanetTerrain.SurfaceColor, so the
// ground keeps the same colours as the orbital map. _MaskTex has the same layout and says what the ground is:
//   R = sea (water, lava or sea ice), G = rock (highlands and mountains), B = polar snow, A = hill shading.
// Painted greyscale detail textures (mean grey 0.5) are tiled in world space on top, so sections join seamlessly:
// land, rock, snow and sea, with the sea texture drifting (water, lava) and lava glowing, plus soft hill shading
// lit from the upper left.
Shader "FBTS/Ground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Colour map (sprite)", 2D) = "white" {}
        [PerRendererData] _MaskTex ("Mask: sea, rock, snow, shading", 2D) = "black" {}
        _LandTex ("Land detail", 2D) = "grey" {}
        _RockTex ("Rock detail", 2D) = "grey" {}
        _SnowTex ("Snow detail", 2D) = "grey" {}
        _SeaTex ("Sea detail", 2D) = "grey" {}
        _Tile ("World units per detail tile", Float) = 7
        _Strength ("Detail strength", Range(0, 1)) = 0.85
        _SeaFlow ("Sea drift speed (0 = still, e.g. ice)", Float) = 0.15
        _SeaGlow ("Sea glow (lava)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One Zero
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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; float4 color : COLOR; };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);  SAMPLER(sampler_MaskTex);
            TEXTURE2D(_LandTex);  SAMPLER(sampler_LandTex);
            TEXTURE2D(_RockTex);  SAMPLER(sampler_RockTex);
            TEXTURE2D(_SnowTex);  SAMPLER(sampler_SnowTex);
            TEXTURE2D(_SeaTex);   SAMPLER(sampler_SeaTex);
            CBUFFER_START(UnityPerMaterial)
                float _Tile, _Strength, _SeaFlow, _SeaGlow;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.world = TransformObjectToWorld(i.positionOS.xyz).xy;
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            // Two scales of the same texture, so the repeat is hard to spot.
            float Detail(TEXTURE2D_PARAM(tex, smp), float2 w)
            {
                float a = SAMPLE_TEXTURE2D(tex, smp, w).r;
                float b = SAMPLE_TEXTURE2D(tex, smp, w * 0.37 + float2(0.31, 0.73)).r;
                return a * 0.7 + b * 0.3;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float4 m = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);
                float2 w = i.world / _Tile;

                float land = Detail(TEXTURE2D_ARGS(_LandTex, sampler_LandTex), w);
                float rock = Detail(TEXTURE2D_ARGS(_RockTex, sampler_RockTex), w * 1.15);
                float snow = Detail(TEXTURE2D_ARGS(_SnowTex, sampler_SnowTex), w);
                float t = _Time.y * _SeaFlow;
                float sea = 0.5 * (SAMPLE_TEXTURE2D(_SeaTex, sampler_SeaTex, w + float2(t, t * 0.6)).r +
                                   SAMPLE_TEXTURE2D(_SeaTex, sampler_SeaTex, w * 1.31 + float2(-t * 0.7, t * 0.4) + 0.5).r);

                float d = lerp(land, rock, saturate(m.g));
                d = lerp(d, snow, saturate(m.b));
                float seaMask = smoothstep(0.25, 0.75, m.r);
                d = lerp(d, sea, seaMask);

                // Detail around mean grey 0.5 brightens or darkens the colour map
                half3 col = baseCol * lerp(1.0, d * 2.0, _Strength);

                // Lava: bright parts of the sea glow
                float glow = _SeaGlow * seaMask * smoothstep(0.45, 0.9, sea);
                col += baseCol * glow * 1.6;

                // Hill shading (baked per km into the mask's alpha; 0.5 = flat)
                col *= lerp(0.65, 1.35, m.a);

                return half4(col * i.color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
