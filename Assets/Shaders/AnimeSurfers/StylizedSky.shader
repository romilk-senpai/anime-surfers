Shader "AnimeSurfers/StylizedSky"
{
    Properties
    {
        [Header(Gradient)]
        _ZenithColor ("Zenith Color", Color) = (0.16, 0.42, 0.88, 1)
        _SkyColor ("Sky Color", Color) = (0.45, 0.72, 0.98, 1)
        _HorizonHeight ("Horizon Band Height", Range(0.01, 0.5)) = 0.12
        _ZenithFalloff ("Zenith Falloff", Range(0.2, 4)) = 1.2

        [Header(Sun)]
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.82, 1)
        _SunSize ("Sun Size (rad)", Range(0.005, 0.15)) = 0.04
        _SunIntensity ("Sun Intensity", Range(0, 10)) = 2.5
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.35
        _SunGlowFalloff ("Sun Glow Falloff", Range(1, 64)) = 10

        [Header(Clouds)]
        _CloudColor ("Lit Color", Color) = (1, 1, 1, 1)
        _CloudShadowColor ("Shadow Color", Color) = (0.68, 0.76, 0.9, 1)
        _CloudCoverage ("Coverage", Range(0, 1)) = 0.45
        _CloudSoftness ("Edge Softness", Range(0.002, 0.2)) = 0.015
        _CloudScale ("Scale", Float) = 1.6
        _CloudSpeed ("Drift Speed", Float) = 0.01
        _CloudShadeOffset ("Shade Offset", Range(0, 0.5)) = 0.15
        _CloudHorizonFade ("Horizon Fade", Range(0.01, 0.5)) = 0.1

        [Header(Islands)]
        _IslandColor ("Island Color", Color) = (0.42, 0.58, 0.74, 1)
        _IslandHeight ("Height", Range(0, 0.15)) = 0.045
        _IslandCoverage ("Coverage", Range(0, 1)) = 0.45
        _IslandFrequency ("Frequency", Float) = 3

        [Header(Stars)]
        _StarIntensity ("Intensity", Range(0, 3)) = 0
        _StarDensity ("Density", Float) = 70
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _SkyColor;
                half _HorizonHeight;
                half _ZenithFalloff;
                half4 _SunColor;
                half _SunSize;
                half _SunIntensity;
                half _SunGlow;
                half _SunGlowFalloff;
                half4 _CloudColor;
                half4 _CloudShadowColor;
                half _CloudCoverage;
                half _CloudSoftness;
                float _CloudScale;
                float _CloudSpeed;
                float _CloudShadeOffset;
                half _CloudHorizonFade;
                half4 _IslandColor;
                half _IslandHeight;
                half _IslandCoverage;
                float _IslandFrequency;
                half _StarIntensity;
                float _StarDensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                // Rotating each octave hides the axis-aligned value noise grid.
                const float2x2 rotation = float2x2(0.8, -0.6, 0.6, 0.8);
                float sum = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amplitude;
                    p = mul(rotation, p) * 2.03 + 17.1;
                    amplitude *= 0.5;
                }

                return sum / 0.9375;
            }

            // Island height above the horizon for an azimuth sampled on a circle, so it wraps seamlessly.
            float IslandProfile(float2 circle, float seed)
            {
                float n = Fbm(circle * _IslandFrequency + seed);
                float threshold = 1.0 - _IslandCoverage;
                return saturate((n - threshold) / max(_IslandCoverage, 0.01)) * _IslandHeight;
            }

            half3 Stars(float3 direction)
            {
                float2 uv = direction.xz / (direction.y + 1.0) * _StarDensity;
                float2 cell = floor(uv);
                float h = Hash(cell);
                float2 center = float2(Hash(cell + 7.1), Hash(cell + 3.3)) * 0.6 + 0.2;
                float star = smoothstep(0.1, 0.0, length(frac(uv) - center)) * step(0.93, h);
                float twinkle = 0.65 + 0.35 * sin(_Time.y * 2.5 + h * 60.0);
                return star * twinkle * _StarIntensity * smoothstep(0.05, 0.3, direction.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float y = direction.y;
                half3 fogColor = unity_FogColor.rgb;

                // The horizon and everything below it use the fog color, so fully fogged water and objects
                // blend into the sky instead of leaving a band where the water mesh ends.
                half3 color = lerp(fogColor, _SkyColor.rgb, smoothstep(0.0, _HorizonHeight, y));
                color = lerp(color, _ZenithColor.rgb, pow(saturate(y), _ZenithFalloff));
                color += Stars(direction);

                // Crisp anime-style sun disc plus a soft glow, hidden below the horizon.
                float3 sunDirection = _MainLightPosition.xyz;
                float sunDot = dot(direction, sunDirection);
                float sunEdge = cos(_SunSize);
                half aboveHorizon = smoothstep(-0.01, 0.02, y);
                half disc = smoothstep(sunEdge - 0.0004, sunEdge + 0.0004, sunDot);
                half glow = pow(saturate(sunDot), _SunGlowFalloff) * _SunGlow * aboveHorizon;
                color += _SunColor.rgb * (disc * _SunIntensity * aboveHorizon + glow);

                // Toon clouds: noise projected on a dome, two flat tones split by whether the cloud thins out
                // toward the sun (lit edge) or not (shadowed body).
                float2 cloudUV = direction.xz / (max(y, 0.0) + 0.15) * _CloudScale
                                 + float2(1.0, 0.35) * _Time.y * _CloudSpeed;
                float density = Fbm(cloudUV);
                float threshold = 1.0 - _CloudCoverage;
                half cloudMask = smoothstep(threshold, threshold + _CloudSoftness, density)
                                 * smoothstep(0.0, _CloudHorizonFade, y);
                float sunwardDensity = Fbm(cloudUV + sunDirection.xz * _CloudShadeOffset);
                half light = 0.6 + (density - sunwardDensity) * 8.0 - (density - threshold) * 1.5;
                half lit = smoothstep(0.47, 0.53, light);
                half3 cloud = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, lit) + _SunColor.rgb * glow;
                color = lerp(color, cloud, cloudMask);

                // Distant island silhouettes in two layers; the far layer and the bases fade into the haze.
                float2 circle = normalize(direction.xz + 1e-5);
                float aa = max(fwidth(y), 1e-4);
                half3 haze = lerp(fogColor, _IslandColor.rgb, smoothstep(0.0, _IslandHeight * 0.5, y));

                float farHeight = IslandProfile(circle, 31.7) * 0.6;
                half farMask = smoothstep(farHeight + aa, farHeight - aa, y) * step(0.0001, farHeight);
                color = lerp(color, lerp(fogColor, haze, 0.45), farMask);

                float nearHeight = IslandProfile(circle, 0.0);
                half nearMask = smoothstep(nearHeight + aa, nearHeight - aa, y) * step(0.0001, nearHeight);
                color = lerp(color, haze, nearMask);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
