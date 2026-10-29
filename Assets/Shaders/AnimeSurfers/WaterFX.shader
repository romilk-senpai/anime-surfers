Shader "AnimeSurfers/WaterFX"
{
    // Foam stamps for the water interaction buffer (Surf.WaterFXBuffer), after Boat Attack's WaterFX pass.
    // The only pass has a custom LightMode, so cameras never draw these particles; the buffer draws them top-down.
    Properties
    {
        _MainTex ("Foam Sprite (alpha)", 2D) = "white" {}
        _FoamStrength ("Foam", Range(0, 2)) = 1
        _HeightStrength ("Height Bump", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "WaterFX"
            Tags { "LightMode" = "WaterFX" }

            // R = foam, G = height bump; overlapping stamps accumulate.
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _FoamStrength;
                half _HeightStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
                return half4(mask * _FoamStrength, mask * _HeightStrength, 0, 0);
            }
            ENDHLSL
        }
    }
}
