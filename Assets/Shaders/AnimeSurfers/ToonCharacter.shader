Shader "AnimeSurfers/ToonCharacter"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Cutout", Float) = 0
        _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Cel Shading)]
        _ShadowColor ("Shadow Color", Color) = (0.72, 0.62, 0.78, 1)
        [Toggle(_SHADOW_MAP)] _UseShadowMap ("Shadow Color From Map", Float) = 0
        [NoScaleOffset] _ShadowMap ("Shadow Color Map (times Shadow Color)", 2D) = "white" {}
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.03
        _ReceiveShadows ("Received Shadow Strength", Range(0, 1)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.35

        [Header(Face)]
        [Toggle(_FACE_SHADING)] _FaceShading ("Face Shading (needs ToonFaceDirection)", Float) = 0
        _FaceFlatten ("Face Flatten", Range(0, 0.3)) = 0.03
        [HideInInspector] _FaceCenter ("Face Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaceForward ("Face Forward", Vector) = (0, 0, 1, 0)
        [HideInInspector] _FaceRight ("Face Right", Vector) = (1, 0, 0, 0)
        [HideInInspector] _FaceUp ("Face Up", Vector) = (0, 1, 0, 0)

        [Header(Specular)]
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0
        _SpecularSize ("Specular Size", Range(0, 0.5)) = 0.06
        _SpecularSoftness ("Specular Softness", Range(0.001, 0.2)) = 0.01

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.35
        _RimWidth ("Rim Width", Range(0, 1)) = 0.3
        _RimSoftness ("Rim Softness", Range(0.001, 0.5)) = 0.05
        _RimLitSideOnly ("Rim Only On Lit Side", Range(0, 1)) = 0.7

        [Header(Night Lights)]
        [Toggle(_EMISSION)] _Emission ("Emission (scaled by SkyCycle)", Float) = 0
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.08, 0.05, 0.08, 1)
        _OutlineWidth ("Outline Width (pixels)", Range(0, 8)) = 1.5
        _OutlineReferenceDistance ("Outline Full Width Distance", Float) = 6
        _OutlineBaseMix ("Outline Base Color Mix", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);
        TEXTURE2D(_ShadowMap);

        // Global set by SkyCycle: 0 by day, 1 at night.
        half _NightLights;

        // Shared by every pass so the material stays SRP Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _ReceiveShadows;
            half _AmbientStrength;
            half _FaceFlatten;
            float4 _FaceCenter;
            float4 _FaceForward;
            float4 _FaceRight;
            float4 _FaceUp;
            half4 _SpecularColor;
            half _SpecularStrength;
            half _SpecularSize;
            half _SpecularSoftness;
            half4 _RimColor;
            half _RimStrength;
            half _RimWidth;
            half _RimSoftness;
            half _RimLitSideOnly;
            half4 _EmissionColor;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineReferenceDistance;
            half _OutlineBaseMix;
        CBUFFER_END

        half4 SampleBase(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(color.a - _Cutoff);
            #endif
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _FACE_SHADING
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _SHADOW_MAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // Genshin-style face lighting without an SDF map: the face is shaded as a vertical cylinder around
            // the head (driven by ToonFaceDirection) and only the light's horizontal angle counts, so one clean
            // shadow line sweeps across the face instead of the mesh normals' blotches.
            half FaceLighting(float3 positionWS, float3 lightDir)
            {
                float3 up = _FaceUp.xyz;
                float3 toPosition = positionWS - _FaceCenter.xyz;
                float2 n = float2(dot(toPosition, _FaceRight.xyz), dot(toPosition, _FaceForward.xyz) + _FaceFlatten);
                n = n / max(length(n), 1e-4);

                float3 horizontal = lightDir - up * dot(lightDir, up);
                float horizontalLength = length(horizontal);
                float2 l = float2(dot(horizontal, _FaceRight.xyz), dot(horizontal, _FaceForward.xyz)) / max(horizontalLength, 1e-4);

                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, dot(n, l) * 0.5 + 0.5);
                // Light from straight above: keep the face lit rather than flickering.
                return lerp(1.0, lit, saturate(horizontalLength * 4.0));
            }

            // Two-tone cel shading with a tinted shadow color and a hard-edged highlight.
            half3 ShadeLight(half3 albedo, half3 shadowColor, float3 positionWS, float3 normalWS, float3 viewDirWS, Light light,
                out half litAmount)
            {
                #if defined(_FACE_SHADING)
                    half lit = FaceLighting(positionWS, light.direction);
                #else
                    half halfLambert = dot(normalWS, light.direction) * 0.5 + 0.5;
                    half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, halfLambert);
                #endif
                lit *= lerp(1.0, light.shadowAttenuation, _ReceiveShadows);
                litAmount = lit;

                half3 color = albedo * lerp(shadowColor, 1.0, lit);

                float3 halfDir = normalize(light.direction + viewDirWS);
                half nh = saturate(dot(normalWS, halfDir));
                half specular = smoothstep(1.0 - _SpecularSize - _SpecularSoftness, 1.0 - _SpecularSize + _SpecularSoftness, nh);
                color += _SpecularColor.rgb * specular * lit * _SpecularStrength;

                return color * light.color * light.distanceAttenuation;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 base = SampleBase(input.uv);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                half3 shadowColor = _ShadowColor.rgb;
                #if defined(_SHADOW_MAP)
                    // Per-texel shadow tints, so palette-textured meshes can share one material.
                    shadowColor *= SAMPLE_TEXTURE2D(_ShadowMap, sampler_BaseMap, input.uv).rgb;
                #endif

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half mainLit;
                half3 color = ShadeLight(base.rgb, shadowColor, input.positionWS, normalWS, viewDirWS, mainLight, mainLit);

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half unused;
                    color += ShadeLight(base.rgb, shadowColor, input.positionWS, normalWS, viewDirWS, light, unused);
                LIGHT_LOOP_END
                #endif

                color += base.rgb * SampleSH(normalWS) * _AmbientStrength;

                // Fresnel rim, optionally limited to the lit side like an anime back light.
                half rim = 1.0 - saturate(dot(normalWS, viewDirWS));
                half rimMask = smoothstep(1.0 - _RimWidth - _RimSoftness, 1.0 - _RimWidth + _RimSoftness, rim);
                rimMask *= lerp(1.0, mainLit, _RimLitSideOnly);
                color += _RimColor.rgb * rimMask * _RimStrength * mainLight.color;

                #if defined(_EMISSION)
                    // Lamps and lit windows, faded in as night falls. Shares the base map's point sampler.
                    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb * _EmissionColor.rgb * _NightLights;
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Inverted hull outline, constant pixel width up close and thinning with distance.
        // Drawn by the renderer's ToonOutlineFeature after all opaques: as a second forward pass
        // (SRPDefaultUnlit) it would switch shaders on every object and break SRP batching.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "ToonOutline" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;

                float distanceScale = saturate(_OutlineReferenceDistance / max(positionCS.w, 0.001));
                float2 offset = normalize(normalCS + 1e-5) * (_OutlineWidth * distanceScale * 2.0 / _ScreenParams.y);
                offset.x *= _ScreenParams.y / _ScreenParams.x;
                positionCS.xy += offset * positionCS.w;

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 base = SampleBase(input.uv);
                half3 color = lerp(_OutlineColor.rgb, base.rgb * _OutlineColor.rgb * 2.0, _OutlineBaseMix);
                color *= saturate(GetMainLight().color);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                SampleBase(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                SampleBase(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                SampleBase(input.uv);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
