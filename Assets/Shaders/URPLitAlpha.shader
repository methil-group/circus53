Shader "Methil/URPLitAlpha"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0.0, 1.0)) = 1.0

        [NoScaleOffset][Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.5

        [HideInInspector] _Surface  ("Surface",  Float) = 1
        [HideInInspector] _Blend     ("Blend",     Float) = 0
        [HideInInspector] _AlphaClip ("Alpha Clip", Float) = 0
        [HideInInspector] _Cull      ("Cull",      Float) = 2
        [HideInInspector] _ZWrite    ("ZWrite",    Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex   LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Alpha;
                half  _BumpScale;
                half  _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);

                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), sign);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseMap.rgb * _BaseColor.rgb;
                half  alpha  = baseMap.a * _BaseColor.a * _Alpha;

                half3 normalWS = normalize(input.normalWS);
                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _BumpScale);
                float sgn = input.tangentWS.w;
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * sgn;
                normalWS = TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS.xyz, bitangent, input.normalWS));
                normalWS = normalize(normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS      = input.positionWS;
                inputData.normalWS        = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord     = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord        = ComputeFogFactor(input.positionCS.z);
                inputData.bakedGI         = SAMPLE_GI(input.uv, 0, normalWS);

                half4 color = UniversalFragmentBlinnPhong(inputData, albedo, half4(0,0,0,0), _Smoothness,
                    0, alpha, 1);

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
