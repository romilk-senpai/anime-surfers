Shader "AnimeSurfers/StylizedWater"
{
    Properties
    {
        [Header(Style)]
        _Stylize ("Stylize", Range(0, 1)) = 0.75
        _LightSteps ("Light Steps", Range(1, 6)) = 3

        [Header(Body)]
        _CrestColor ("Crest Color", Color) = (0.3, 0.82, 0.92, 1)
        _TroughColor ("Trough Color", Color) = (0.03, 0.3, 0.58, 1)
        _DeepColor ("Deep Color (realistic)", Color) = (0.01, 0.07, 0.15, 1)
        _Absorption ("Absorption per Meter (RGB)", Vector) = (0.55, 0.16, 0.1, 0)
        _ScatterColor ("Scatter Color", Color) = (0.04, 0.5, 0.48, 1)
        _ScatterStrength ("Scatter Strength", Range(0, 3)) = 1
        _RefractionStrength ("Refraction Strength", Range(0, 0.1)) = 0.03

        [Header(Surface)]
        [NoScaleOffset][Normal] _NormalMap ("Ripple Normal Map", 2D) = "bump" {}
        _NormalStrength ("Ripple Strength", Range(0, 2)) = 0.6
        _NormalTilingA ("Ripple Tiling A (m)", Float) = 7
        _NormalTilingB ("Ripple Tiling B (m)", Float) = 2.6
        _NormalSpeed ("Ripple Speed", Float) = 0.35
        _NormalFadeDistance ("Ripple Fade Distance", Float) = 70

        [Header(Reflection)]
        _HorizonColor ("Horizon Color", Color) = (0.62, 0.86, 1, 1)
        _ReflectionStrength ("Sky Reflection", Range(0, 1.5)) = 0.4
        _ReflectionRoughness ("Reflection Roughness", Range(0, 0.5)) = 0.04
        _SunRoughness ("Sun Roughness", Range(0.01, 0.5)) = 0.07
        _SunIntensity ("Sun Intensity", Range(0, 10)) = 3

        [Header(Foam)]
        [NoScaleOffset] _FoamTex ("Foam Texture", 2D) = "white" {}
        _FoamColor ("Foam Color", Color) = (0.95, 0.98, 1, 1)
        _FoamTiling ("Foam Tiling (m)", Float) = 3
        _WhitecapThreshold ("Whitecap Compression", Range(0, 1.2)) = 0.8
        _WhitecapSoftness ("Whitecap Softness", Range(0.01, 0.5)) = 0.12
        _IntersectionFoamDistance ("Intersection Foam Distance", Float) = 0.5
    }

    SubShader
    {
        // Rendered after opaques so the camera depth and opaque textures are available.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend Off
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            // Low graphics quality (Core.GraphicsApplier): the long waves' shading is interpolated from the vertices.
            #pragma multi_compile _ _WATER_LOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "WaterWaves.hlsl"

            // Waves this long are smooth enough across the mesh cells to shade from the vertices on low quality.
            #define WATER_LONG_WAVES 4

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);

            CBUFFER_START(UnityPerMaterial)
                half _Stylize;
                half _LightSteps;
                half4 _CrestColor;
                half4 _TroughColor;
                half4 _HorizonColor;
                half4 _DeepColor;
                float4 _Absorption;
                half4 _ScatterColor;
                half _ScatterStrength;
                float _RefractionStrength;
                half _NormalStrength;
                float _NormalTilingA;
                float _NormalTilingB;
                float _NormalSpeed;
                float _NormalFadeDistance;
                half _ReflectionStrength;
                half _ReflectionRoughness;
                half _SunRoughness;
                half _SunIntensity;
                half4 _FoamColor;
                float _FoamTiling;
                half _WhitecapThreshold;
                half _WhitecapSoftness;
                float _IntersectionFoamDistance;
            CBUFFER_END

            // Board interaction, pushed by Surf.WaterWake.
            #define WAKE_MAX_POINTS 48
            #define WAKE_GROUP_SEGMENTS 8
            #define WAKE_GROUPS 6                // ceil((WAKE_MAX_POINTS - 1) / WAKE_GROUP_SEGMENTS)
            #define SPLASH_MAX 8
            float4 _WakePoints[WAKE_MAX_POINTS]; // xy = xz, z = spawn time, w = strength
            float4 _WakeBounds;                  // xy = min xz, zw = max xz
            float4 _WakeGroupBounds[WAKE_GROUPS]; // per run of segments: xy = min xz, zw = max xz of the live points
            float4 _WakeGroupWidths[WAKE_GROUPS]; // x = widest age-widened wake of the run's live segments (0 = none)
            float4 _WakeParams;                  // width, spread, lifetime, edge width
            float4 _SplashPoints[SPLASH_MAX];    // xy = xz, z = spawn time, w = strength
            float4 _RippleParams;                // speed, frequency, lifetime, strength

            // Top-down foam/height buffer, pushed by Surf.WaterFXBuffer.
            TEXTURE2D(_WaterFXMap);
            SAMPLER(sampler_WaterFXMap);
            float4 _WaterFXParams;               // xy = min xz, z = 1 / size (0 = off), w = texel size in uv
            float _WaterFXNormalStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 gridXZ : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            #if defined(_WATER_LOW)
                float4 longWavesA : TEXCOORD5; // long waves' sums: xyz = tangent, w = height
                float3 longWavesB : TEXCOORD6; // xyz = binormal
            #endif
            };

            void EvaluateWake(float2 xz, inout half foam, inout float2 gradient)
            {
                if (any(xz < _WakeBounds.xy) || any(xz > _WakeBounds.zw))
                {
                    return;
                }

                float width = _WakeParams.x;
                float spread = _WakeParams.y;
                float lifetime = max(_WakeParams.z, 0.001);
                float edgeWidth = _WakeParams.w;

                // Find the segment whose (age-widened) wake this pixel is deepest inside, then shade once.
                // Shading per segment would draw the round segment caps as rings inside the wake.
                float bestRatio = 1e5;
                float bestDistance = 0;
                float bestAge = 0;
                float bestStrength = 0;
                float2 bestDir = 0;

                // Segments come in runs with conservative bounds: a run is skipped when even its nearest possible
                // segment, at its widest, can't beat the best ratio so far. Same result, far fewer segments.
                [loop]
                for (int group = 0; group < WAKE_GROUPS; group++)
                {
                    float4 box = _WakeGroupBounds[group];
                    float maxWidth = _WakeGroupWidths[group].x;
                    float2 outside = max(max(box.xy - xz, xz - box.zw), 0);
                    if (maxWidth <= 0 || length(outside) >= bestRatio * maxWidth)
                    {
                        continue;
                    }

                    int first = group * WAKE_GROUP_SEGMENTS;
                    int last = min(first + WAKE_GROUP_SEGMENTS, WAKE_MAX_POINTS - 1);

                    [loop]
                    for (int i = first; i < last; i++)
                    {
                        float4 a = _WakePoints[i];
                        float4 b = _WakePoints[i + 1];
                        float strength = min(a.w, b.w);
                        if (strength <= 0)
                        {
                            continue;
                        }

                        float2 ab = b.xy - a.xy;
                        float t = saturate(dot(xz - a.xy, ab) / max(dot(ab, ab), 1e-4));
                        float2 toPixel = xz - (a.xy + ab * t);
                        float d = length(toPixel);
                        float age = _WaterWaveTime - lerp(a.z, b.z, t);
                        float ratio = d / (width + age * spread);

                        if (ratio < bestRatio)
                        {
                            bestRatio = ratio;
                            bestDistance = d;
                            bestAge = age;
                            bestStrength = strength;
                            bestDir = toPixel / max(d, 1e-4);
                        }
                    }
                }

                if (bestStrength <= 0)
                {
                    return;
                }

                float life = saturate(1.0 - bestAge / lifetime);
                float wakeHalfWidth = width + bestAge * spread;

                // Churned water right behind the board that settles quickly...
                float coreLife = saturate(1.0 - bestAge / (lifetime * 0.3));
                float core = (1.0 - smoothstep(width * 0.2, width * 1.2, bestDistance)) * coreLife;
                // ...plus foam lines along the spreading wake edges.
                float edgeDistance = abs(bestDistance - wakeHalfWidth);
                float edge = (1.0 - smoothstep(edgeWidth * 0.5, edgeWidth * 2.0 + bestAge * 0.05, edgeDistance)) * life * 0.8;
                foam = max(foam, max(core, edge) * bestStrength);

                // Small ripples riding just inside the wake edge.
                float ripple = cos(edgeDistance * 14.0) * exp(-edgeDistance * 3.0) * life * bestStrength;
                gradient += bestDir * ripple * 0.2;
            }

            void EvaluateSplashes(float2 xz, inout half foam, inout float2 gradient)
            {
                float speed = _RippleParams.x;
                float frequency = _RippleParams.y;
                float lifetime = max(_RippleParams.z, 0.001);
                float rippleStrength = _RippleParams.w;

                [unroll]
                for (int i = 0; i < SPLASH_MAX; i++)
                {
                    float4 s = _SplashPoints[i];
                    float age = _WaterWaveTime - s.z;
                    if (s.w <= 0 || age < 0 || age > lifetime)
                    {
                        continue;
                    }

                    float2 delta = xz - s.xy;
                    float r = length(delta);
                    float front = age * speed;
                    float life = 1.0 - age / lifetime;

                    // A train of rings trailing the expanding front.
                    float behind = front - r;
                    float envelope = saturate(behind * 4.0) * saturate(1.0 - behind / 1.5) * life * s.w;
                    float2 dir = delta / max(r, 1e-4);
                    gradient += dir * cos(behind * frequency) * envelope * rippleStrength;

                    // Foam ring at the front; light touches still show, strength mostly scales the ripples.
                    float ring = (1.0 - smoothstep(0.0, 0.12, abs(behind))) * life * saturate(s.w * 1.5 + 0.35);
                    foam = max(foam, ring * 0.6);
                }
            }

            void EvaluateWaterFX(float2 xz, inout half foam, inout float2 gradient)
            {
                if (_WaterFXParams.z <= 0)
                {
                    return;
                }

                float2 uv = (xz - _WaterFXParams.xy) * _WaterFXParams.z;
                if (any(uv < 0) || any(uv > 1))
                {
                    return;
                }

                // Fade out toward the buffer edges so the moving window never shows a seam.
                float2 edgeDistance = min(uv, 1.0 - uv);
                half edge = saturate(min(edgeDistance.x, edgeDistance.y) * 8.0);

                // Forward differences off the center sample: 3 taps instead of 5, doubled to match the central
                // difference's two-texel span.
                float2 texel = float2(_WaterFXParams.w, 0);
                half4 fx = SAMPLE_TEXTURE2D(_WaterFXMap, sampler_WaterFXMap, uv);
                float dx = (SAMPLE_TEXTURE2D(_WaterFXMap, sampler_WaterFXMap, uv + texel.xy).g - fx.g) * 2.0;
                float dz = (SAMPLE_TEXTURE2D(_WaterFXMap, sampler_WaterFXMap, uv + texel.yx).g - fx.g) * 2.0;

                foam = max(foam, fx.r * edge);
                gradient += float2(dx, dz) * (_WaterFXNormalStrength * edge);
            }

            // Two ripple normal map layers scrolling in different directions, blended (whiteout).
            float3 SampleRipples(float2 xz, float distanceFade)
            {
                float t = _WaterWaveTime * _NormalSpeed;
                float2 uvA = xz / _NormalTilingA + float2(0.6, 0.8) * t / _NormalTilingA * 2.0;
                float2 uvB = xz / _NormalTilingB + float2(-0.8, 0.45) * t / _NormalTilingB;
                float strength = _NormalStrength * distanceFade;
                float3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA), strength);
                float3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), strength * 0.7);
                return normalize(float3(a.xy + b.xy, a.z * b.z));
            }

            half FoamPattern(float2 xz)
            {
                float t = _WaterWaveTime * 0.05;
                half a = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, xz / _FoamTiling + float2(t, t * 0.6)).r;
                half b = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, xz / (_FoamTiling * 2.3) - float2(t * 0.7, t)).r;
                return saturate(a * 0.6 + b * 0.6);
            }

            // Textured foam: low amounts only show the brightest foam lines, high amounts fill in.
            half FoamMask(half amount, half pattern)
            {
                return saturate((amount * 1.4 - (1.0 - pattern)) * 3.0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 offset = EvaluateWaterWavesLod(positionWS.xz);

                output.gridXZ = positionWS.xz;
            #if defined(_WATER_LOW)
                float3 longOffset = 0;
                float3 longTangent = 0;
                float3 longBinormal = 0;
                AddWaterWaves(positionWS.xz, 0, WATER_LONG_WAVES, longOffset, longTangent, longBinormal);
                output.longWavesA = float4(longTangent, longOffset.y);
                output.longWavesB = longBinormal;
            #endif
                // Only the geometry flattens on coarse cells and toward the far water; shading keeps the full waves
                // (see frag).
                positionWS += offset * WaterEdgeFade(positionWS.xz);
                positionWS.y += _WaterBaseHeight;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz;

                // Recompute the waves per pixel at the undisplaced grid position for crisp normals.
                float3 offset, tangent, binormal;
            #if defined(_WATER_LOW)
                // Only the height of the offset is used below.
                offset = float3(0, input.longWavesA.w, 0);
                tangent = float3(1, 0, 0) + input.longWavesA.xyz;
                binormal = float3(0, 0, 1) + input.longWavesB;
                AddWaterWaves(input.gridXZ, WATER_LONG_WAVES, WATER_MAX_WAVES, offset, tangent, binormal);
            #else
                EvaluateWaterWaves(input.gridXZ, offset, tangent, binormal);
            #endif
                float3 waveNormal = normalize(cross(binormal, tangent));
                float3 waveTangent = normalize(tangent);
                float3 waveBinormal = normalize(cross(waveTangent, waveNormal));

                half interactionFoam = 0;
                float2 interactionGradient = 0;
                EvaluateWake(xz, interactionFoam, interactionGradient);
                EvaluateSplashes(xz, interactionFoam, interactionGradient);
                EvaluateWaterFX(xz, interactionFoam, interactionGradient);

                float distanceFade = lerp(1.0, 0.35, saturate(input.eyeDepth / _NormalFadeDistance));
                float3 ripples = SampleRipples(xz, distanceFade);
                float3 normalWS = normalize(waveTangent * ripples.x + waveBinormal * ripples.y + waveNormal * ripples.z);
                normalWS = normalize(normalWS + float3(-interactionGradient.x, 0.0, -interactionGradient.y));

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();
                // URP filters the shadow map on every pixel; skip it past the shadow distance, where the water covers
                // much of the screen, and fade out toward it.
                half shadowFade = GetMainLightShadowFade(input.positionWS);
                half realtimeShadow = 1.0;
                UNITY_BRANCH
                if (shadowFade < 1.0)
                {
                    realtimeShadow = lerp(MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS)), 1.0,
                        shadowFade);
                }

                half shadow = lerp(0.6, 1.0, realtimeShadow);
                float3 lightDir = mainLight.direction;
                half3 ambient = SampleSH(float3(0, 1, 0));

                // Refraction + absorption: what's below the surface fades into the deep water color.
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawThickness = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - input.eyeDepth;
                float2 refractedUV = screenUV + normalWS.xz * _RefractionStrength;
                float refractedThickness = LinearEyeDepth(SampleSceneDepth(refractedUV), _ZBufferParams) - input.eyeDepth;
                if (refractedThickness < 0)
                {
                    // Refracted sample hit something in front of the water; fall back to the straight view.
                    refractedUV = screenUV;
                    refractedThickness = rawThickness;
                }

                float3 transmittance = exp(-max(refractedThickness, 0) * _Absorption.xyz);
                half crest = saturate(offset.y / max(_WaterAmplitudeSum, 0.001) * 0.5 + 0.5);

                // Body color: stylized turquoise-to-blue by wave height with banded light,
                // blended with the physically based deep color by _Stylize.
                half ndl = saturate(dot(normalWS, lightDir) * 0.5 + 0.5);
                half banded = floor(ndl * _LightSteps + 0.5) / _LightSteps;
                half3 stylizedBody = lerp(_TroughColor.rgb, _CrestColor.rgb, smoothstep(0.15, 0.95, crest))
                                     * (mainLight.color * lerp(0.75, 1.05, banded) * shadow + ambient * 0.25);
                half3 realisticBody = _DeepColor.rgb * (mainLight.color * (saturate(lightDir.y) * 0.6 + 0.2) * shadow + ambient);
                half3 waterBody = lerp(realisticBody, stylizedBody, _Stylize);
                half3 refracted = SampleSceneColor(refractedUV) * transmittance + waterBody * (1.0 - transmittance);

                // Light passing through the thin upper part of waves (crests glow turquoise against the sun).
                half backlight = pow(saturate(dot(viewDirWS, -normalize(lightDir + waveNormal * 0.4))), 4.0);
                half3 scatter = _ScatterColor.rgb * mainLight.color * (backlight * 1.5 + 0.15) * crest * crest
                                * _ScatterStrength * shadow;

                // Sky reflection with Schlick Fresnel (F0 of water ~0.02); stylized mode leans on a flat horizon tint.
                half nv = saturate(dot(normalWS, viewDirWS));
                half fresnel = 0.02 + 0.98 * pow(1.0 - nv, 5.0);
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                reflectDir.y = abs(reflectDir.y); // ripples can point reflections below the horizon
                half3 sky = GlossyEnvironmentReflection(reflectDir, _ReflectionRoughness, 1.0);
                half3 reflection = lerp(sky, _HorizonColor.rgb, _Stylize * 0.6);

                half3 color = lerp(refracted + scatter, reflection, fresnel * _ReflectionStrength);

                // Sun glints (GGX); stylized mode turns them into crisp sparkles.
                float3 halfDir = normalize(lightDir + viewDirWS);
                half nh = saturate(dot(normalWS, halfDir));
                half nl = saturate(dot(normalWS, lightDir));
                half a2 = _SunRoughness * _SunRoughness * _SunRoughness * _SunRoughness;
                half dTerm = nh * nh * (a2 - 1.0) + 1.0;
                half ggx = a2 / (PI * dTerm * dTerm);
                half sunFresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(viewDirWS, halfDir)), 5.0);
                half glint = min(ggx * sunFresnel * nl * _SunIntensity, 40.0);
                glint = lerp(glint, step(0.6, glint) * 1.5, _Stylize);
                color += mainLight.color * glint * realtimeShadow;

                // Foam: whitecaps where Gerstner waves compress, contact foam, and the board's wake.
                float jacobian = tangent.x * binormal.z - tangent.z * binormal.x;
                half whitecap = saturate((_WhitecapThreshold - jacobian) / _WhitecapSoftness);
                half intersection = 1.0 - saturate(rawThickness / max(_IntersectionFoamDistance, 0.001));
                half pattern = FoamPattern(xz);
                half foam = max(max(FoamMask(whitecap * 0.8, pattern), FoamMask(intersection, pattern)),
                                FoamMask(interactionFoam, pattern));
                half3 foamLit = _FoamColor.rgb * (mainLight.color * (saturate(dot(waveNormal, lightDir)) * 0.7 + 0.3) * shadow + ambient);
                color = lerp(color, foamLit, foam);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
