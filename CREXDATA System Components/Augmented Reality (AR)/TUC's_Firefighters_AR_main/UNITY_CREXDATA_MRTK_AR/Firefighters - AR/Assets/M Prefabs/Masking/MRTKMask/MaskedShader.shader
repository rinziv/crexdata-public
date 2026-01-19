Shader "Custom/StencilMaskedObject"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        Stencil
        {
            Ref 1
            Comp Equal
            Pass Keep
            ReadMask 255
            WriteMask 0
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float3 albedo = albedoTex.rgb * _Color.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.bakedGI = SAMPLE_GI(IN.positionWS, inputData.normalWS, 1.0);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1,1,1,1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = albedoTex.a * _Color.a;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.specular = 0.5;                  // default value
                surfaceData.normalTS = float3(0,0,1);        // default tangent space normal
                surfaceData.emission = float3(0,0,0);        // no emission
                surfaceData.occlusion = 1.0;                 // default occlusion
                surfaceData.clearCoatMask = 0.0;             // default clear coat
                surfaceData.clearCoatSmoothness = 0.0;       // default clear coat smoothness

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}
