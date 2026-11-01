Shader "AnimeSurfers/PickupHalo"
{
    // Additive soft halo for pickups. Drawn on a quad that always faces the camera from the object's origin, so the
    // pickup's spin does not show; sized by the object's scale and pushed back so it sits behind the pickup. Pulses
    // gently, out of phase between pickups.
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.2, 0.9, 0.3, 1)
        _Falloff ("Falloff", Range(0.5, 6)) = 2
        _Ring ("Ring", Range(0, 1)) = 0.25
        _PulseSpeed ("Pulse Speed", Float) = 3
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.12
        _PushBack ("Push Back", Float) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "PickupHalo"

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Falloff;
                half _Ring;
                float _PulseSpeed;
                half _PulseAmount;
                float _PushBack;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half pulse : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float size = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));

                float phase = dot(center, float3(0.37, 0.11, 0.53));
                float wave = sin(_Time.y * _PulseSpeed + phase);
                size *= 1 + wave * _PulseAmount;

                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 toCamera = normalize(GetCameraPositionWS() - center);
                float3 positionWS = center + (right * input.positionOS.x + up * input.positionOS.y) * size
                    - toCamera * _PushBack;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.pulse = 0.85 + 0.15 * wave;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half r = length(input.uv * 2 - 1);
                half glow = pow(saturate(1 - r), _Falloff);
                half ring = saturate(1 - abs(r - 0.62) * 9) * _Ring;
                half alpha = saturate((glow + ring) * input.pulse) * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
