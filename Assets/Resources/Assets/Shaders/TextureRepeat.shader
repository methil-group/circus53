Shader "Custom/URPTextureRepeat"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _TextureScale("Texture Scale", Float) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Texture2D _BaseMap;
            SamplerState sampler_BaseMap;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _TextureScale;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Transform object space position to clip space
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Apply the scale factor to the UV coordinates to repeat the texture
                output.uv = input.uv * _TextureScale;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample the texture with the repeated UVs
                half4 color = _BaseMap.Sample(sampler_BaseMap, input.uv) * _BaseColor;
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
