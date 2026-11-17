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

        // The big wave's lip (Surf.BigWaveLip) is drawn with this shader too: the lip variant, both sides.
        [Header(Big Wave Lip)]
        [Toggle(_BIG_WAVE_LIP)] _BigWaveLipVariant ("Big Wave Lip", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        // The water is opaque (One, Zero); the lip blends over it (SrcAlpha, OneMinusSrcAlpha) and draws after it.
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Toggle] _ZWrite ("ZWrite", Float) = 1
        _LipOpacity ("Lip Opacity (root, tip, fade start 0..1 along the curl)", Vector) = (1, 1, 0.75, 0)
        _LipAbsorption ("Lip Absorption per Meter (RGB)", Vector) = (2, 0.45, 0.6, 0)
        _LipTransmission ("Lip Transmission", Range(0, 3)) = 0.6
        // Shared with the water: inside the barrel, under the lip, both are shaded darker.
        _TubeShade ("Tube Shade", Range(0, 1)) = 0.8
    }

    SubShader
    {
        // Rendered after opaques so the camera depth and opaque textures are available.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            // Low graphics quality (Core.GraphicsApplier): the long waves' shading is interpolated from the vertices.
            #pragma multi_compile _ _WATER_LOW
            #pragma shader_feature_local _BIG_WAVE_LIP

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
                half4 _LipOpacity;
                float4 _LipAbsorption;
                half _LipTransmission;
                half _TubeShade;
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
                float3 normalOS : NORMAL;
                float4 uv : TEXCOORD0;           // the lip's: xy as lipUV, z = meters along the curl, w = lipKind
            #if defined(_BIG_WAVE_LIP)
                float3 uv1 : TEXCOORD1;          // x = the lip shell's thickness (m), y = how far short of fully pitched,
                                                 // z = how far pitched
            #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 gridXZ : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            #if defined(_BIG_WAVE_LIP)
                float4 lipNormalWS : TEXCOORD5;  // w = the shell's thickness (m)
                float4 lipUV : TEXCOORD6;        // x = across the sheet, root 0 to tip 1; y = along, back 0 to front 1;
                                                 // z = along the ridge (m), w = along the curl (m)
                float lipKind : TEXCOORD7;       // 0 = the lip's inner surface, 1 = its outer one (between: the tip's cap)
                float2 lipPitch : TEXCOORD8;     // x = 0 where the lip is fully pitched, toward 1 where it's only starting
                                                 // to; y = how far it has pitched, 0..1
            #elif defined(_WATER_LOW)
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

            // UV of world xz at a tiling (m) plus a scroll (in repeats), measured from the wave origin and wrapped: world
            // positions and the clock grow without bound over a run, and large UVs lose precision on mobile GPUs.
            float2 WaterUV(float2 xz, float tiling, float2 scroll)
            {
                return (xz - _WaterOrigin.xy) / tiling + frac(_WaterOrigin.xy / tiling + scroll);
            }

            // Two ripple normal map layers scrolling in different directions, blended (whiteout).
            float3 SampleRipples(float2 xz, float distanceFade)
            {
                float t = _WaterWaveTime * _NormalSpeed;
                float2 uvA = WaterUV(xz, _NormalTilingA, float2(0.6, 0.8) * t / _NormalTilingA * 2.0);
                float2 uvB = WaterUV(xz, _NormalTilingB, float2(-0.8, 0.45) * t / _NormalTilingB);
                float strength = _NormalStrength * distanceFade;
                float3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA), strength);
                float3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), strength * 0.7);
                return normalize(float3(a.xy + b.xy, a.z * b.z));
            }

            half FoamPattern(float2 xz)
            {
                float t = _WaterWaveTime * 0.05;
                half a = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, WaterUV(xz, _FoamTiling, float2(t, t * 0.6))).r;
                half b = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, WaterUV(xz, _FoamTiling * 2.3, -float2(t * 0.7, t))).r;
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

            #if defined(_BIG_WAVE_LIP)
                float3 lipNormal;
                float3 positionWS = BigWaveLipPosition(input.positionOS.xyz, input.normalOS, lipNormal);
                output.lipNormalWS = float4(lipNormal, input.uv1.x);
                float3 offset = 0;
                output.lipUV = float4(input.uv.xy, _BigWaveLip.x + input.positionOS.z, input.uv.z);
                output.lipKind = input.uv.w;
                output.lipPitch = input.uv1.yz;
                // The shell's root is buried in the crest: it rides the small waves like the water around it, so their
                // troughs never uncover it, easing off up the curl.
                float rootWeight = 1.0 - smoothstep(0.0, 0.35, input.uv.x);
                float3 smallTangent = 0;
                float3 smallBinormal = 0;
                positionWS += EvaluateSmallWavesLod(positionWS.xz, smallTangent, smallBinormal) * rootWeight *
                    WaterEdgeFade(positionWS.xz);
                output.gridXZ = positionWS.xz;
            #else
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 offset = EvaluateWaterWavesLod(positionWS.xz);
                output.gridXZ = positionWS.xz;
            #endif
            #if defined(_WATER_LOW) && !defined(_BIG_WAVE_LIP)
                float3 longOffset = 0;
                float3 longTangent = 0;
                float3 longBinormal = 0;
                AddWaterWaves(positionWS.xz, 0, WATER_LONG_WAVES, BigWaveCalm(positionWS.xz), longOffset, longTangent,
                    longBinormal);
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
                // The sea haze lies low: the big wave stands up out of it, so it shows on the horizon while the water
                // (and everything on it) is still hidden.
            #if defined(_BIG_WAVE_LIP)
                float hazeHeight = positionWS.y - _WaterBaseHeight;
            #else
                float hazeHeight = BigWaveHazeHeight(positionWS.xz);
            #endif
                output.fogFactor = ComputeFogFactorZ0ToFar(UNITY_Z_0_FAR_FROM_CLIPSPACE(output.positionCS.z) *
                    BigWaveHazeScale(hazeHeight));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz;

                // Recompute the waves per pixel at the undisplaced grid position for crisp normals, minus the ones too
                // short for the pixel.
                float2 gridDx = ddx(input.gridXZ);
                float2 gridDy = ddy(input.gridXZ);
                float3 offset, tangent, binormal;
                half bigWave = 0;
            #if defined(_BIG_WAVE_LIP)
                offset = 0;
                tangent = float3(1, 0, 0);
                binormal = float3(0, 0, 1);
                float removedVariance = 0;
                // Each surface's normal already points out of the shell, the side it's seen from. (Flipping it toward
                // the viewer per pixel instead jumps the lighting along lines that move with the camera.)
                float3 lipNormal = normalize(input.lipNormalWS.xyz);
                float3 lipAlong = float3(_BigWaveLine.z, 0, _BigWaveLine.w);
                float3 waveNormal = lipNormal;
                float3 waveTangent = normalize(cross(lipNormal, lipAlong));
                float3 waveBinormal = normalize(cross(waveTangent, waveNormal));
            #elif defined(_WATER_LOW)
                // Only the height of the offset is used below.
                offset = float3(0, input.longWavesA.w, 0);
                tangent = float3(1, 0, 0) + input.longWavesA.xyz;
                binormal = float3(0, 0, 1) + input.longWavesB;
                float removedVariance = AddWaterWavesFiltered(input.gridXZ, gridDx, gridDy, WATER_LONG_WAVES,
                    WATER_MAX_WAVES, BigWaveCalm(input.gridXZ), offset, tangent, binormal);
            #else
                offset = 0;
                tangent = float3(1, 0, 0);
                binormal = float3(0, 0, 1);
                float removedVariance = AddWaterWavesFiltered(input.gridXZ, gridDx, gridDy, 0, WATER_MAX_WAVES,
                    BigWaveCalm(input.gridXZ), offset, tangent, binormal);
            #endif
            #if !defined(_BIG_WAVE_LIP)
                // The crest coloring below follows the small waves only; the big wave brings its own.
                float smallWavesHeight = offset.y;
                bigWave = AddBigWave(input.gridXZ, offset, tangent, binormal);
                offset.y = smallWavesHeight;
                float3 waveNormal = normalize(cross(binormal, tangent));
                float3 waveTangent = normalize(tangent);
                float3 waveBinormal = normalize(cross(waveTangent, waveNormal));
            #endif

                // On the big wave: glassy lines drawn up the face as the lip sucks the water up it (they run on into the
                // lip's own), and a smooth, darker trough in front of the face, drained of that water.
                half faceStreaks = 0.5;
                half faceLines = 0;
                half drained = 0;
            #if !defined(_BIG_WAVE_LIP)
                UNITY_BRANCH
                if (_BigWaveShape.x > 0.0)
                {
                    float2 faceRelative = input.gridXZ - _BigWaveLine.xy;
                    float faceAlong = dot(faceRelative, _BigWaveLine.zw);
                    float faceAcross = dot(faceRelative, BigWaveSide());
                    faceStreaks = FoamPattern(float2(faceAlong, faceAcross * 0.25 - _WaterWaveTime * 0.6));
                    faceLines = bigWave * (1.0 - smoothstep(_BigWaveProfile.y, _BigWaveProfile.y + 2.0, faceAcross));
                    float dEnds;
                    drained = smoothstep(_BigWaveProfile.x - 8.0, _BigWaveProfile.x - 2.0, faceAcross) *
                        (1.0 - smoothstep(_BigWaveProfile.x, _BigWaveProfile.x + 3.0, faceAcross)) *
                        BigWaveEnds(faceAlong, dEnds) * _BigWaveLip.y;
                }
            #endif

                half interactionFoam = 0;
                float2 interactionGradient = 0;
            #if !defined(_BIG_WAVE_LIP)
                // Projected straight down, so they belong to the water below the lip, not the lip.
                EvaluateWake(xz, interactionFoam, interactionGradient);
                EvaluateSplashes(xz, interactionFoam, interactionGradient);
                EvaluateWaterFX(xz, interactionFoam, interactionGradient);
            #endif

                float distanceFade = lerp(1.0, 0.35, saturate(input.eyeDepth / _NormalFadeDistance));
                float3 ripples = SampleRipples(xz, distanceFade * (1.0 - 0.6 * drained));
                float3 normalWS = normalize(waveTangent * ripples.x + waveBinormal * ripples.y + waveNormal * ripples.z);
                normalWS = normalize(normalWS + float3(-interactionGradient.x, 0.0, -interactionGradient.y));
                // The face's lines ridge it along the ridge, catching the light.
                normalWS = normalize(normalWS + float3(_BigWaveLine.z, 0.0, _BigWaveLine.w) * (faceStreaks - 0.5) * 0.25 *
                    faceLines);

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

                // Inside the barrel the lip hides most of the sky and sun: the water (and the lip's underside) is lit
                // dimmer, and reflects the water around it instead of the sky.
            #if defined(_BIG_WAVE_LIP)
                half tube = input.lipKind < 0.5 ? BigWaveTube(xz) * _TubeShade : 0.0;
            #else
                half tube = BigWaveTube(xz) * _TubeShade;
            #endif
                half bodyShadow = shadow * (1.0 - 0.5 * tube);
                half3 bodyAmbient = ambient * (1.0 - 0.6 * tube);

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
            #if defined(_BIG_WAVE_LIP)
                // Glassy lines flowing up and over the curl, stretched along it.
                half lipStreaks = FoamPattern(float2(input.lipUV.z, input.lipUV.w * 0.25 - _WaterWaveTime * 0.6));
                half lipU = input.lipUV.x;
                bool lipOuter = input.lipKind > 0.5;
                // Inside: bright where it leaves the crest, deep under the curl, lit through again toward the thin tip.
                // Outside, the top of the wave, it stays bright.
                crest = lipOuter
                    ? lerp(1.0, 0.75, lipU)
                    : lerp(lerp(1.0, 0.35, smoothstep(0.0, 0.5, lipU)), 0.8, smoothstep(0.55, 1.0, lipU));
                // The lip's own looks come in away from its root, so where it leaves the face it's shaded just like the
                // face's crest and the two meet without a line.
                half lipFromRoot = smoothstep(0.03, 0.3, lipU);
                crest = saturate(crest + (lipStreaks - 0.5) * 0.3 * lipFromRoot);
            #else
                // The big wave's face brightens toward its crest, like the photo-blue wall it is.
                crest = max(crest, smoothstep(0.05, 0.9, bigWave));
                crest = saturate(crest + (faceStreaks - 0.5) * 0.3 * faceLines) * (1.0 - 0.45 * drained);
            #endif

                // Body color: stylized turquoise-to-blue by wave height with banded light,
                // blended with the physically based deep color by _Stylize.
                half ndl = saturate(dot(normalWS, lightDir) * 0.5 + 0.5);
                half banded = floor(ndl * _LightSteps + 0.5) / _LightSteps;
            #if defined(_BIG_WAVE_LIP)
                // Stepped light draws straight band edges across the tight curl; it's only kept at the root, to match
                // the face it carries on from.
                banded = lerp(banded, ndl, lipFromRoot);
            #endif
                half3 stylizedBody = lerp(_TroughColor.rgb, _CrestColor.rgb, smoothstep(0.15, 0.95, crest))
                                     * (mainLight.color * lerp(0.75, 1.05, banded) * bodyShadow + bodyAmbient * 0.25);
                half3 realisticBody = _DeepColor.rgb * (mainLight.color * (saturate(lightDir.y) * 0.6 + 0.2) * bodyShadow +
                                      bodyAmbient);
                half3 waterBody = lerp(realisticBody, stylizedBody, _Stylize);
            #if defined(_BIG_WAVE_LIP)
                // Sun and sky shine through the sheet from its far side, tinted by the water they cross: red goes
                // first, so the thin lip glows green-turquoise and the thick root stays deep. Strongest looking toward
                // the sun. (The opaque texture has no water in it, so nothing behind is refracted.)
                half3 lipTransmittance = exp(-input.lipNormalWS.w * _LipAbsorption.xyz);
                half lipBacklit = saturate((0.3 - dot(lipNormal, lightDir)) / 1.3);
                lipBacklit *= 0.6 + pow(saturate(dot(-viewDirWS, lightDir)), 4.0);
                // Uneven through the glassy lines, like the water's own thickness varying. In the water's crest color,
                // which carries the time of day (Surf.SkyCycle), so the glow dims with the rest of the water at night.
                // Coming in from the root, where the lip carries on from the face (which has none of it).
                half3 refracted = waterBody + lipTransmittance * _CrestColor.rgb * _LipTransmission *
                                  lipFromRoot *
                                  lerp(0.6, 1.3, lipStreaks) *
                                  (mainLight.color * lipBacklit * shadow + SampleSH(-lipNormal) * 0.5);
            #else
                half3 refracted = SampleSceneColor(refractedUV) * transmittance + waterBody * (1.0 - transmittance);
            #endif

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
                reflection = lerp(reflection, _TroughColor.rgb * (mainLight.color * 0.4 + ambient * 0.3), tube);

                half reflectionStrength = _ReflectionStrength;
            #if defined(_BIG_WAVE_LIP)
                // As glossy as the face where the lip leaves it, less so up the curl, which seen edge-on would
                // otherwise mirror the sky like a sheet of glass.
                reflectionStrength *= lerp(1.0, 0.35, smoothstep(0.1, 0.5, input.lipUV.x));
            #endif
                half3 color = lerp(refracted + scatter, reflection, fresnel * reflectionStrength);

                // Sun glints (GGX), in float: half (true fp16 on mobile) can't resolve a lobe this sharp, rounds the
                // denominator to 0 and returns inf/NaN that bloom spreads. The slopes of the waves filtered out above
                // widen the lobe in their place, so far water shows a glitter path instead of sparkling noise.
                float3 halfDir = normalize(lightDir + viewDirWS);
                float nh = saturate(dot(normalWS, halfDir));
                half nl = saturate(dot(normalWS, lightDir));
                float sunRoughness = _SunRoughness;
                float baseA2 = sunRoughness * sunRoughness * sunRoughness * sunRoughness;
                float a2 = baseA2 + removedVariance;
                float dTerm = nh * nh * (a2 - 1.0) + 1.0;
                float ggx = a2 / (PI * dTerm * dTerm);
                half sunFresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(viewDirWS, halfDir)), 5.0);
                float glint = min(ggx * sunFresnel * nl * _SunIntensity, 40.0);
                // Stylized mode turns them into crisp sparkles, cut over about a pixel. Where the lobe was widened
                // they fade back to the smooth glint: a cutoff there would paint the whole glitter path solid.
                float sparkle = saturate((glint - 0.6) / max(fwidth(glint), 1e-3) + 0.5) * 1.5;
                glint = lerp(glint, sparkle, _Stylize * sqrt(baseA2 / a2));
                color += mainLight.color * glint * realtimeShadow * (1.0 - tube);

                // Foam: whitecaps where Gerstner waves compress, contact foam, and the board's wake.
                float jacobian = tangent.x * binormal.z - tangent.z * binormal.x;
                half whitecap = saturate((_WhitecapThreshold - jacobian) / _WhitecapSoftness) * (1.0 - 0.8 * drained);
                half intersection = 1.0 - saturate(rawThickness / max(_IntersectionFoamDistance, 0.001));
                half pattern = FoamPattern(xz);
                half foam = max(max(FoamMask(whitecap * 0.8, pattern), FoamMask(intersection, pattern)),
                                FoamMask(interactionFoam, pattern));
            #if defined(_BIG_WAVE_LIP)
                // The lip's foam is laid out on its own surface (meters along the ridge and along the curl), not
                // projected from above, which smears it into streaks where the curl stands up; it flows over the curl
                // with the water.
                half lipPattern = FoamPattern(float2(input.lipUV.z, input.lipUV.w - _WaterWaveTime * 0.8));
                // A second, coarser layer breaks the lace into clumps, so it's churned rather than even.
                half lipClumps = FoamPattern(float2(input.lipUV.z * 0.37 + 11.0, input.lipUV.w * 0.37 - _WaterWaveTime * 0.3));
                lipPattern = saturate(lipPattern * lerp(0.55, 1.35, lipClumps));
                // The end of the curl churning into lace (aerated water: holes in it all the way to the tip), the very
                // back end where the lip has already broken, a few of the streaks thickening toward the tip, and
                // whitewater blowing off the top of the wave.
                half lipFoam = max(smoothstep(0.55, 1.0, lipU) * 0.78, 1.0 - smoothstep(0.0, 0.12, input.lipUV.y));
                half topWater = lipOuter ? 0.6 * smoothstep(0.05, 0.15, lipU) * (1.0 - smoothstep(0.3, 0.6, lipU)) : 0.0;
                foam = max(foam, max(max(FoamMask(lipFoam, lipPattern), FoamMask(topWater, lipPattern)),
                                     FoamMask(lerp(0.05, 0.35, lipU), lipStreaks)));
                // Where the lip is only partly thrown (pitching out at the tube's mouth, peeling off ahead toward the
                // eye) it feathers into whitewater, out from its root.
                half lipFeather = saturate(input.lipPitch.x) * smoothstep(0.15, 0.7, lipU) * 0.9;
                foam = max(foam, FoamMask(lipFeather, lipPattern));
            #else
                foam = max(max(foam, FoamMask(BigWaveFoam(xz), pattern)),
                           FoamMask(0.18 * smoothstep(0.4, 1.0, faceLines) * _BigWaveLip.y, faceStreaks));
            #endif
                half foamDiffuse = saturate(dot(waveNormal, lightDir)) * 0.7 + 0.3;
            #if defined(_BIG_WAVE_LIP)
                // Whitewater scatters light through itself: the lip's foam stays bright even facing away from the sun
                // or under the curl, rather than going grey.
                foamDiffuse = saturate(dot(waveNormal, lightDir)) * 0.3 + 0.7;
                half3 foamLit = _FoamColor.rgb * (mainLight.color * foamDiffuse * lerp(shadow, 1.0, 0.5) + ambient);
            #else
                half3 foamLit = _FoamColor.rgb * (mainLight.color * foamDiffuse * bodyShadow + bodyAmbient);
            #endif
                color = lerp(color, foamLit, foam);

                color = MixFog(color, input.fogFactor);

                // The far plane cuts the water just below the horizon, where the sky shows its horizon color. Fade
                // into that color toward the far plane so no band shows there even with fog turned off. Height on
                // screen above the horizon goes as 1 / depth, so the fade runs on far / depth; it falls off
                // exponentially away from the far plane, so it only softens the horizon instead of hazing the water.
                half farFade = exp(-6.0 * (_ProjectionParams.z / max(input.eyeDepth, 0.001) - 1.0));
                color = lerp(color, _HorizonColor.rgb, farFade);
            #if defined(_BIG_WAVE_LIP)
                // Solid water: anything see-through on the shell shows the sky behind it, bands along the mesh rows
                // where its two surfaces overlap, and still writes depth, cutting out whatever is drawn behind it later
                // (a gap). The root meets the face by matching its shading instead (see lipFromRoot), and the tip is
                // cut out (not blended) into ragged fingers by the same lace as its foam.
                half fingers = 1.0 - smoothstep(lerp(0.9, 0.99, lipPattern), 1.0, lipU);
                clip(fingers - 0.5);
                // Only once it's thrown out into a curl does it show: barely pitched, it's a thin flat sheet sticking
                // out of the crest. It breaks out of the crest along a ragged edge as it pitches.
                clip(input.lipPitch.y - lerp(0.22, 0.38, lipPattern));
                half alpha = lerp(_LipOpacity.x, _LipOpacity.y, smoothstep(_LipOpacity.z, 1.0, lipU));
                return half4(color, lipOuter ? 1.0 : max(alpha, foam));
            #else
                return half4(color, 1.0);
            #endif
            }
            ENDHLSL
        }
    }
}
