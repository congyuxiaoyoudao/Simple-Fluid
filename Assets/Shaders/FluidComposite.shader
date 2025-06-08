Shader "PBF/FluidComposite"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "PBF_Composite_Pass"

            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment FS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Particle
            {
                float3 position;
                float3 velocity;
            };
            
            // IMPORTANT: The name here MUST match the name used in ParticleSystem.cs SetBuffer: "_Particle"
            StructuredBuffer<Particle> _ParticlePositions;

            // These constant buffer are set in cs
            CBUFFER_START(UnityPerMaterial)
                float4 _AbsorptionColor;
                float4 _ScatterColor;
                float _IOR;
                float _Size;
                int _DebugType;
            CBUFFER_END

            TEXTURE2D(_FluidDepthTexture);
            SAMPLER(sampler_FluidDepthTexture);
            TEXTURE2D(_FluidSmoothedDepthTexture);
            SAMPLER(sampler_FluidSmoothedDepthTexture);
            TEXTURE2D(_FluidNormalTexture);
            SAMPLER(sampler_FluidNormalTexture);
            TEXTURE2D(_FluidThicknessTexture);
            SAMPLER(sampler_FluidThicknessTexture);
            TEXTURE2D(_SceneColorTexture);
            SAMPLER(sampler_SceneColorTexture);
            samplerCUBE _SkyboxTexture;
            
            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 positionVS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // struct FSOutput {
            //     float4 depth : SV_Target0;
            //     float4 color : SV_Target1;
            // };
            
            static float2 TransformUV(float2 uv)
            {
                float2 transformedUV = uv * 2.0 - 1.0;
                return transformedUV;
            }

            static float3 ComputeSphereNormal(float2 uv)
            {
                float3 N = float3(0.0f,0.0f,0.0f);
                N.xy = uv;
                N.z = sqrt(1-dot(uv,uv));
                return N;
            }
            
            Varyings VS (Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;

                // Get particle position from the Compute Buffer using vertex ID
                // Using the user's specified buffer name: _Particle
                float3 particleWorldPos = _ParticlePositions[instanceID].position;
                float4 viewPos = mul(UNITY_MATRIX_V,float4(particleWorldPos,1.0));

                // As billboard, the quad always faces camera
                // TODO: Scaler should be a function relative to camera's aspect and fovy and screen's size,
                viewPos.xyz += input.vertex.xyz * _Size;

                output.positionVS = viewPos;
                output.positionCS = mul(UNITY_MATRIX_P, viewPos);
                output.uv = input.uv;
                return output;
            }

            float Fresnel_schlick(float cosTheta, float3 F0)
            {
                // Schlick's approximation for Fresnel reflectance
                return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
            }
            
            float4 FS (Varyings input) : SV_Target
            {
                // TODO: discard particles which has density below a threshold
                // Trransform uv origin to quad center
                float2 quadUV = TransformUV(input.uv);
                
                // discard the outer area of circle
                float radius = length(quadUV);
                clip(1 - radius);

                // Get Screen uv
                float2 pixelPos = input.positionCS.xy;
                float2 screenUV = pixelPos / _ScreenParams.xy;

                // Get depth
                float depth = SAMPLE_TEXTURE2D(_FluidDepthTexture, sampler_FluidDepthTexture, screenUV).r;
                if(_DebugType == 0)
                    return float4(depth.xxx,1.0);

                // Get smoothed depth
                float smoothedDepth = SAMPLE_TEXTURE2D(_FluidSmoothedDepthTexture, sampler_FluidSmoothedDepthTexture, screenUV).r;
                if(_DebugType == 1)
                    return float4(smoothedDepth.xxx,1.0);
                
                // Get normal
                float3 normalVS = SAMPLE_TEXTURE2D(_FluidNormalTexture,sampler_FluidNormalTexture,screenUV);
                if(_DebugType == 2)
                    return float4(normalVS,1.0);
                float3 normalWS = TransformViewToWorldNormal(normalVS, true);

                // Get viewspace position
                float3 posVS = input.positionVS.xyz + normalVS;

                // Refract
                float thickness = SAMPLE_TEXTURE2D(_FluidThicknessTexture,sampler_FluidThicknessTexture,screenUV).r;
                if(_DebugType == 3)
                    return float4(thickness.xxx,1.0);
                float3 absorptionFactor = exp(-(_AbsorptionColor) * thickness);
                
                float eta = 1.0 / _IOR;
                
                float3 I = normalize(posVS);
                float3 I_world = normalize(mul(UNITY_MATRIX_I_V, I));
                float3 N = normalWS;
                float3 R = refract(I_world, N, eta);
                
                float refractScaler = R.y * 0.025;
                float2 refractCoord = screenUV + normalVS.xy * refractScaler;
                float3 refractColor = SAMPLE_TEXTURE2D(_SceneColorTexture,sampler_SceneColorTexture,refractCoord).xyz * absorptionFactor;
                refractColor = texCUBE(_SkyboxTexture, R).rgb * absorptionFactor;
                
                // Reflect
                float3 O = reflect(I_world, N);
                float3 skyColor = texCUBE(_SkyboxTexture, O).rgb;

                // TODO: compose reflect and refract with fresnel
                float cosTheta = saturate(dot(-I_world, N));
                float F0 = 0.02f; // non-electrical medium's F0 
                float fresnel = Fresnel_schlick(cosTheta, F0);
                // return fresnel;
                
                // base shading
                // TODO: This will NOT be used in the final version
                Light mainLight = GetMainLight();
                float3 lightDirVS = mul((float3x3)UNITY_MATRIX_V, mainLight.direction);
                
                float3 reflectColor = lerp(refractColor, skyColor, fresnel);
                float alpha = saturate(thickness);
                return float4(reflectColor,alpha);

            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError" // Simplified fallback
}