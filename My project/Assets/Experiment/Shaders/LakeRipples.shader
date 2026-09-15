Shader "Experiment/Lake Ripples"
{
    Properties
    {
        _Color ("Water colour", Color) = (0.16, 0.48, 0.62, 1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.65
        _RippleScale ("Ripple spacing", Float) = 1.2
        _RippleStrength ("Ripple strength", Range(0,0.3)) = 0.085
        _RippleTime ("Animation time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        struct Input { float3 worldPos; };
        fixed4 _Color;
        half _Smoothness, _RippleScale, _RippleStrength;
        float _RippleTime;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz * _RippleScale;
            float a = sin(p.x * 1.7 + p.y * .65 + _RippleTime);
            float b = sin(p.y * 2.3 - p.x * .4 - _RippleTime * .8);
            float fine = sin(p.x * 3.1 + p.y * 2.8 + _RippleTime * 1.3);
            o.Albedo = _Color.rgb * (1 + (a * b + fine * .25) * .055);
            o.Normal = normalize(float3(a * _RippleStrength, b * _RippleStrength, 1));
            o.Metallic = .08;
            o.Smoothness = _Smoothness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
