// SalokOutlineComposite.shader
// Step 2 of the tutorial outline (TutorialOutline.cs): a full-screen pass that draws the
// outline colour on pixels just OUTSIDE the white silhouette in the mask, blended over the
// picture. _OutlineWidth (pixels) and _OutlineColor pulse; TutorialOutline sets them.
// Not used on any material: the code draws with it.
// HOW TO EDIT: nothing to tune here; the look is in TutorialOutline's Inspector fields.
Shader "Hidden/Salok/OutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Name "Composite"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float _OutlineWidth;
            float4 _MaskSize; // xy = mask size in pixels

            float MaskAt(float2 px)
            {
                px = clamp(px, float2(0, 0), _MaskSize.xy - 1);
                return LOAD_TEXTURE2D_X(_BlitTexture, uint2(px)).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 px = input.texcoord * _MaskSize.xy;
                if (MaskAt(px) > 0.5) return half4(0, 0, 0, 0); // inside the object: leave it as it is

                // Look around in 12 directions, at the full width and at half of it (fills gaps
                // on thin outlines). Any white found = this pixel is on the outline.
                float found = 0;
                [unroll]
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 0.52359878; // 30 degrees
                    float2 dir = float2(cos(a), sin(a));
                    found = max(found, MaskAt(px + dir * _OutlineWidth));
                    found = max(found, MaskAt(px + dir * (_OutlineWidth * 0.5)));
                }
                return half4(_OutlineColor.rgb, _OutlineColor.a * found);
            }
            ENDHLSL
        }
    }
}
