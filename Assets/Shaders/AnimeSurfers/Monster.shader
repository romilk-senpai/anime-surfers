Shader "AnimeSurfers/AnimatedBurningEdgesMonster"
{
    Properties
    {
        _Color ("Base Color", Color) = (1, 0.3, 0.1, 1)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _BurnIntensity ("Burn Intensity", Range(0, 1)) = 0.7
        _BurnSpeed ("Burn Speed", Float) = 1.5
        _DisplacementStrength ("Displacement Strength", Float) = 0.05
        _EdgeGlow ("Edge Glow Intensity", Range(0, 2)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma vertex vert
        #pragma target 3.0

        struct Input
        {
            float2 uv_NoiseTex;
            float3 worldPos;
            float3 viewDir; // For fresnel effect
        };

        fixed4 _Color;
        sampler2D _NoiseTex;
        float _BurnIntensity;
        float _BurnSpeed;
        float _DisplacementStrength;
        float _EdgeGlow;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_NoiseTex = v.texcoord + _Time * _BurnSpeed;

            float displacement =  _SinTime.y * frac(sin(dot(v.vertex.xy, float2(12.9898,78.233))) * 43758.5453) * _DisplacementStrength;
            v.vertex.xyz += v.normal * displacement;
        }

        void surf(Input IN, inout SurfaceOutputStandard o) 
        {
            float fresnel = pow(1.0 - saturate(dot(normalize(IN.viewDir), normalize(o.Normal))), _EdgeGlow);

            // Scrolling noise texture to animate burn effect
            fixed burnValue = tex2D(_NoiseTex, IN.uv_NoiseTex).r;
            burnValue = smoothstep(1.0 - _BurnIntensity, 1.0, burnValue);

            // Combine fresnel and burn effect for outer edges
            float edgeBurn = fresnel * burnValue;

            // Apply color and burn effect
            o.Albedo = lerp(_Color.rgb * 0.5, _Color.rgb, edgeBurn);
            o.Alpha = 1 - edgeBurn * _Color.a;
            o.Emission = _Color.rgb * edgeBurn * 2.0; // Glowing effect on edges
        }
        ENDCG
    }
    FallBack "Transparent"
}
