Shader "AnimeSurfers/PowerUpGlow"
{
    // Additive glow for power-up effects: a fresnel rim with faint scanlines rising through it, for the shield
    // bubble and the super jump ring. With Rim at 0 it is a flat glow tinted by vertex colour, for trails.
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.55, 0.36, 1, 1)
        _Fill ("Fill", Range(0, 1)) = 0.12
        _Rim ("Rim", Range(0, 2)) = 1
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _LineDensity ("Line Density", Float) = 6
        _LineSpeed ("Line Speed", Float) = 1.5
        _LineStrength ("Line Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PowerUpGlow"

            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fill;
                half _Rim;
                half _RimPower;
                float _LineDensity;
                float _LineSpeed;
                half _LineStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                // Trails come without normals; SafeNormalize keeps their rim term at zero instead of NaN.
                half facing = saturate(abs(dot(SafeNormalize(input.normalWS), view)));
                half rim = pow(1 - facing, _RimPower) * _Rim;
                half lines = step(0.5, frac(input.positionWS.y * _LineDensity - _Time.y * _LineSpeed)) * _LineStrength;
                half glow = (_Fill + rim) * (1 + lines);

                half4 color = _Color * input.color;
                return half4(color.rgb, saturate(glow) * color.a);
            }
            ENDHLSL
        }
    }
}
