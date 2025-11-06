Shader "Unlit/HeightColorAndChecker"
{
    Properties
    {
        _CheckerColor1 ("Checker Color 1", Color) = (1.0, 1.0, 1.0, 1.0)
        _CheckerColor2 ("Checker Color 2", Color) = (0.8, 0.8, 0.8, 1.0)
        _CheckerDensity ("Checker Density", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR; // Vertex color from C#
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                half4 color         : COLOR;     // Vertex color passed to frag
                float3 worldPos     : TEXCOORD0; // World position
            };

            // Properties
            half4 _CheckerColor1;
            half4 _CheckerColor2;
            float _CheckerDensity;

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Procedural checkerboard based on world position
                float2 checkerUV = input.worldPos.xz * _CheckerDensity;
                float checkerPattern = fmod(floor(checkerUV.x) + floor(checkerUV.y), 2);
                half4 checkerColor = lerp(_CheckerColor1, _CheckerColor2, checkerPattern);

                // Multiply the height-based vertex color with the checkerboard pattern
                return input.color * checkerColor;
            }
            ENDHLSL
        }
    }
}