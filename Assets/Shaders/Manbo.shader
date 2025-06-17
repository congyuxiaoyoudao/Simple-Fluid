Shader "Toon/ManBo"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _Color ("Base Color Tint", Color) = (1,1,1,1)
        _ShadeColor ("Shade Color", Color) = (0.5, 0.5, 0.5, 1)
        _Cutoff ("Toon Threshold", Range(0,1)) = 0.5
        _UOffset("U Offset", float) = 0.0
        _VOffset("V Offset", float) = 0.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType"="Opaque" }

        Pass
        {
            Name "ToonManbo"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment FS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ShadeColor;
                float _Cutoff;
                float _UOffset;
                float _VOffset;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings VS(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv + float2(_UOffset,_VOffset);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                return OUT;
            }

            half4 FS(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight(IN.shadowCoord);
                float NdotL = saturate(dot(IN.normalWS, mainLight.direction));

                float stepShade = step(_Cutoff, NdotL);

                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _Color;
                float3 toonColor = lerp(albedo.rgb * _ShadeColor.rgb, albedo.rgb, stepShade);

                // Apply shadow attenuation
                toonColor *= mainLight.shadowAttenuation;
                return float4(toonColor, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(worldPos);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

    }

    FallBack "Hidden/InternalErrorShader"
}
