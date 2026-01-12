Shader "Custom/StencilMaskedMapObject"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        [IntRange] _StencilRef("Stencil Reference", Range(0,255)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)]_StencilComparison("Stencil Comparison", Int) = 3 // Equal
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue"="Geometry" }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComparison]
            Pass Keep
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

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                ZERO_INITIALIZE(Varyings, OUT); // Added for robust initialization
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

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
                UNITY_SETUP_INSTANCE_ID(IN); // Makes unity_StereoEyeIndex available if needed

                float4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float3 albedoColor = albedoTex.rgb * _BaseColor.rgb;
                float alpha = albedoTex.a * _BaseColor.a;

                SurfaceData surfaceData;
                ZERO_INITIALIZE(SurfaceData, surfaceData);
                surfaceData.albedo = albedoColor;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1.0h;
                surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
                surfaceData.alpha = alpha;

                InputData inputData;
                ZERO_INITIALIZE(InputData, inputData);
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    // inputData.shadowCoord = IN.shadowCoord; // Add shadowCoord to Varyings if you use this
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS) || defined(ADDITIONAL_LIGHTS_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                inputData.bakedGI = SAMPLE_GI(IN.positionWS, inputData.normalWS, float4(1,1,1,1));

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}