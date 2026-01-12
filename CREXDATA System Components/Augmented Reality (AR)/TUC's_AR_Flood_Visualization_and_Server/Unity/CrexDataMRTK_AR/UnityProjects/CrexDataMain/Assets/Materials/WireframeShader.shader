Shader "Custom/WireFrame"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1.0,1.0,1.0,1.0)
        _MainTex ("Texture", 2D) = "white" {}

        [Header(Wire Frame)]
        _WireThickness ("Wire Thickness", Range(0, 1)) = 0.1
        [HDR]_WireColor("Wire Color", Color) = (1.0,0.0,0.0,1.0)
        _WireDensity ("Wire Density", Range(1, 10)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" 
               "LightMode" = "UniversalForward" 
               "Queue" = "Transparent" 
               "RenderType" = "Transparent" }
        LOD 100

        Pass
        {
            Name "MainPass"
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom

            // make fog work
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2g
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float fogCoord : TEXCOORD1;
            };

            struct g2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
                float3 barycentric : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            half4 _BaseColor, _WireColor;
            half _WireThickness;
            float _WireDensity;

            v2g vert(appdata v)
            {
                v2g o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogCoord = ComputeFogFactor(o.vertex.z);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g i[3], inout TriangleStream<g2f> stream)
            {
                g2f o;

                float3 barycentric[3] = {
                    float3(1, 0, 0) * _WireDensity,
                    float3(0, 1, 0) * _WireDensity,
                    float3(0, 0, 1) * _WireDensity
                };

                for (int j = 0; j < 3; j++)
                {
                    o.vertex = i[j].vertex;
                    o.uv = i[j].uv;
                    o.fogCoord = i[j].fogCoord;
                    o.barycentric = barycentric[j];
                    stream.Append(o);
                }
            }

            half4 frag(g2f i) : SV_Target
            {
                float minBarycentric = min(i.barycentric.x, min(i.barycentric.y, i.barycentric.z));
                float wireAlpha = 1.0 - smoothstep(0.0, _WireThickness, minBarycentric);

                half4 wireColor = _WireColor;
                wireColor.a *= wireAlpha;

                return wireColor;
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
