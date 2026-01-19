Shader "Custom/StencilMask"
{
    SubShader
    {
        Tags {"Queue"="Geometry-1" "RenderType"="Opaque"}
        ColorMask 0
        ZWrite Off
        
        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass {}
    }
}
