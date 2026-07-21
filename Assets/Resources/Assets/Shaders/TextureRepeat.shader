Shader "Custom/URPTextureRepeat"
{
    Properties
    {
        [MainTexture] _BaseMap("Bottom Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _TextureScale("Texture Scale", Float) = 1.0

        [Header(Height Blend)]
        _TopMap("Top Texture", 2D) = "white" {}
        _BlendMin("Blend Min Height (Y)", Float) = 0.0
        _BlendMax("Blend Max Height (Y)", Float) = 10.0
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
                float3 positionWS   : TEXCOORD1;
            };

            Texture2D _BaseMap;
            SamplerState sampler_BaseMap;
            Texture2D _TopMap;
            SamplerState sampler_TopMap;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _TextureScale;
                float _BlendMin;
                float _BlendMax;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _TextureScale;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float height = input.positionWS.y;

                // Blend factor: 0 = bottom texture, 1 = top texture
                float t = smoothstep(_BlendMin, _BlendMax, height);

                half4 bottom = _BaseMap.Sample(sampler_BaseMap, input.uv);
                half4 top    = _TopMap.Sample(sampler_TopMap, input.uv);
                half4 color  = lerp(bottom, top, t) * _BaseColor;

                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
