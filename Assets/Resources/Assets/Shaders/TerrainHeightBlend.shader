Shader "Custom/URPTerrainHeightBlend"
{
    Properties
    {
        [Header(Height Blend Settings)]
        _BlendHeight0 ("Layer 0 Max Height (Y)", Float) = 3.0
        _BlendHeight1 ("Layer 1 Max Height (Y)", Float) = 8.0
        _BlendHeight2 ("Layer 2 Max Height (Y)", Float) = 15.0
        _BlendSmooth  ("Blend Smoothness", Range(0.0, 5.0)) = 1.0

        [Header(Layer 0 - Bottom)]
        [MainTexture] _Layer0 ("Texture", 2D) = "white" {}
        _Layer0Normal ("Normal", 2D) = "bump" {}
        _Layer0Scale ("Tiling", Float) = 10.0

        [Header(Layer 1 - Low-Mid)]
        _Layer1 ("Texture", 2D) = "white" {}
        _Layer1Normal ("Normal", 2D) = "bump" {}
        _Layer1Scale ("Tiling", Float) = 10.0

        [Header(Layer 2 - Mid-High)]
        _Layer2 ("Texture", 2D) = "white" {}
        _Layer2Normal ("Normal", 2D) = "bump" {}
        _Layer2Scale ("Tiling", Float) = 10.0

        [Header(Layer 3 - Top)]
        _Layer3 ("Texture", 2D) = "white" {}
        _Layer3Normal ("Normal", 2D) = "bump" {}
        _Layer3Scale ("Tiling", Float) = 10.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP lighting
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 tangentWS    : TEXCOORD3;
                float3 viewDirWS    : TEXCOORD4;
                float fogFactor     : TEXCOORD5;
            };

            TEXTURE2D(_Layer0);
            SAMPLER(sampler_Layer0);
            TEXTURE2D(_Layer0Normal);
            SAMPLER(sampler_Layer0Normal);

            TEXTURE2D(_Layer1);
            SAMPLER(sampler_Layer1);
            TEXTURE2D(_Layer1Normal);
            SAMPLER(sampler_Layer1Normal);

            TEXTURE2D(_Layer2);
            SAMPLER(sampler_Layer2);
            TEXTURE2D(_Layer2Normal);
            SAMPLER(sampler_Layer2Normal);

            TEXTURE2D(_Layer3);
            SAMPLER(sampler_Layer3);
            TEXTURE2D(_Layer3Normal);
            SAMPLER(sampler_Layer3Normal);

            CBUFFER_START(UnityPerMaterial)
                float _BlendHeight0;
                float _BlendHeight1;
                float _BlendHeight2;
                float _BlendSmooth;
                float _Layer0Scale;
                float _Layer1Scale;
                float _Layer2Scale;
                float _Layer3Scale;
            CBUFFER_END

            void BlendLayer(inout half4 color, inout half3 normalTS,
                float2 uv, float scale,
                Texture2D tex, SamplerState samp,
                Texture2D norm, SamplerState normSamp,
                half weight)
            {
                half4 c = SAMPLE_TEXTURE2D(tex, samp, uv * scale);
                half3 n = UnpackNormal(SAMPLE_TEXTURE2D(norm, normSamp, uv * scale));
                color += c * weight;
                normalTS += n * weight;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInput.positionCS;
                output.positionWS = posInput.positionWS;
                output.uv = input.uv;
                output.normalWS = normInput.normalWS;
                output.tangentWS = float4(normInput.tangentWS, input.tangentOS.w);
                output.viewDirWS = GetWorldSpaceViewDir(posInput.positionWS);
                output.fogFactor = ComputeFogFactor(posInput.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float h = input.positionWS.y;

                // Height-based weights (smoothstep between layers)
                float w0 = 1.0 - smoothstep(_BlendHeight0 - _BlendSmooth, _BlendHeight0 + _BlendSmooth, h);
                float w3 = smoothstep(_BlendHeight2 - _BlendSmooth, _BlendHeight2 + _BlendSmooth, h);
                float w1 = (1.0 - w0 - w3) * (1.0 - smoothstep(_BlendHeight1 - _BlendSmooth, _BlendHeight1 + _BlendSmooth, h));
                float w2 = (1.0 - w0 - w3) * smoothstep(_BlendHeight1 - _BlendSmooth, _BlendHeight1 + _BlendSmooth, h);

                // Normalize
                float total = w0 + w1 + w2 + w3;
                w0 /= total; w1 /= total; w2 /= total; w3 /= total;

                // Sample and blend
                half4 albedo = half4(0, 0, 0, 1);
                half3 normalTS = half3(0, 0, 1);

                BlendLayer(albedo, normalTS, input.uv, _Layer0Scale, _Layer0, sampler_Layer0, _Layer0Normal, sampler_Layer0Normal, w0);
                BlendLayer(albedo, normalTS, input.uv, _Layer1Scale, _Layer1, sampler_Layer1, _Layer1Normal, sampler_Layer1Normal, w1);
                BlendLayer(albedo, normalTS, input.uv, _Layer2Scale, _Layer2, sampler_Layer2, _Layer2Normal, sampler_Layer2Normal, w2);
                BlendLayer(albedo, normalTS, input.uv, _Layer3Scale, _Layer3, sampler_Layer3, _Layer3Normal, sampler_Layer3Normal, w3);

                normalTS = normalize(normalTS);

                // Tangent to world normal
                float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 TBN = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                // URP lighting
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = input.positionWS;
                lightingInput.normalWS = normalWS;
                lightingInput.viewDirectionWS = SafeNormalize(input.viewDirWS);
                lightingInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lightingInput.fogCoord = input.fogFactor;

                half4 color = UniversalFragmentBlinnPhong(lightingInput, albedo.rgb, half4(0,0,0,0), 0, 0);
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET { return 0; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
