Shader "Custom/StencilMaskedTransparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (Texture)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0
        _MetallicGlossMap ("Metallic (Texture)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Bump Scale", Float) = 1
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1
        _OcclusionMap ("Occlusion (Texture)", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0)
        _EmissionMap ("Emission (Texture)", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        Stencil
        {
            Ref 1
            Comp Equal // Render only where stencil buffer is 1
        }

        Blend SrcAlpha OneMinusSrcAlpha // Enable transparency
        ZWrite Off                      // Prevent writing to depth buffer (to avoid sorting issues)
        Cull Back                        // Normal culling mode

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:blend
        #pragma target 3.0  // Allow more texture interpolators
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        float4 _Color;
        sampler2D _MetallicGlossMap;
        sampler2D _BumpMap;
        float _BumpScale;
        sampler2D _OcclusionMap;
        float _OcclusionStrength;
        sampler2D _EmissionMap;
        float4 _EmissionColor;
        float _Glossiness;
        float _Metallic;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo + Transparency
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;  // Use texture alpha for transparency

            // Metallic & Smoothness
            fixed4 metallicTex = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Metallic = _Metallic * metallicTex.r;
            o.Smoothness = _Glossiness * metallicTex.a;

            // Normal Map
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex)) * _BumpScale;

            // Occlusion
            o.Occlusion = tex2D(_OcclusionMap, IN.uv_MainTex).r * _OcclusionStrength;

            // Emission
            o.Emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
