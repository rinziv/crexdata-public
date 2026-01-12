Shader "Custom/MapMask" // Renamed from StencilMaskedMapObject for clarity if this is its primary role
{
    Properties
    {
        [IntRange] _StencilWriteValue ("Stencil Value to Write", Range(0, 255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry-10" // Render before most opaque objects to set up stencil
            "RenderType" = "Opaque" // Pipeline treats it as opaque, but ColorMask 0 makes it invisible
        }

        Stencil
        {
            Ref [_StencilWriteValue]
            Comp Always
            Pass Replace
            Fail Keep
            ZFail Keep
        }

        Pass
        {
            Name "InvisibleStencilWriter"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite Off
            ColorMask 0
            // Cull Off // Optional

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                // UNITY_VERTEX_INPUT_INSTANCE_ID // Not needed if frag doesn't use instance ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                ZERO_INITIALIZE(Varyings, OUT); // Good practice to zero-initialize output struct
                // UNITY_TRANSFER_INSTANCE_ID(IN, OUT); // Not needed if frag doesn't use instance ID
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // UNITY_SETUP_INSTANCE_ID(IN); // Not needed as instance ID is not used in this frag shader
                return 0; // Output color is irrelevant due to ColorMask 0
            }
            ENDHLSL
        }
    }
    Fallback Off
}