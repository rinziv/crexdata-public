Shader "Custom/StencilMask"
{
    SubShader
    {
        Tags { "Queue" = "Geometry-1" }

        Stencil
        {
            Ref 1        // Set stencil value to 1 inside the mask
            Comp Always  // Always write to the stencil buffer
            Pass Replace // Replace stencil buffer value with Ref (1)
        }

        ColorMask 0  // Don't render the object (invisible)
        ZWrite Off   // Don't write to the depth buffer

        Pass {} // Empty pass
    }
}
