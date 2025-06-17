Shader "PBF/FluidComposite"
{
    Properties
    {
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
            StructuredBuffer<Particle> _Particles;
            StructuredBuffer<float> _Density;

            // These constant buffer are set in cs
            CBUFFER_START(UnityPerMaterial)
                float _DensityThreshold;
                float4 _ParticleColor;
                float4 _AbsorptionColor;
                float4 _ScatterColor;
                float _ReflectScalar; 
                float _IOR;
                float _Turbidity;
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
            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
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
                float density: TEXCOORD2;
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
                float3 particleWorldPos = _Particles[instanceID].position;
                float4 viewPos = mul(UNITY_MATRIX_V,float4(particleWorldPos,1.0));

                // As billboard, the quad always faces camera
                // TODO: Scaler should be a function relative to camera's aspect and fovy and screen's size,
                viewPos.xyz += input.vertex.xyz * _Size;

                output.positionVS = viewPos;
                output.positionCS = mul(UNITY_MATRIX_P, viewPos);
                output.uv = input.uv;
                output.density = _Density[instanceID];
                return output;
            }

            float Fresnel_schlick(float cosTheta, float3 F0)
            {
                // Schlick's approximation for Fresnel reflectance
                return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
            }
            
            float3 VelocityTemperatureGradient(float t)
            {
                // t = [0,1], 0 = cold (blue), 1 = warm (yellow-red)
                float3 cold = float3(0.2, 0.4, 1.0);
                float3 warm = float3(1.0, 0.8, 0.4);
                return lerp(cold, warm, saturate(t));
            }

            float LinearDepthToUnityDepth(float linearDepth)
			{
				float depth01 = (linearDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y);
				return (1 - (depth01 * _ZBufferParams.y)) / (depth01 * _ZBufferParams.x);
			}
            
            float ComputeClipSpaceDepth(float4 posVS)
            {
                float4 posCS = mul(UNITY_MATRIX_P,posVS);
                float linearDepth = -(posVS.z);
                float unityDepth = LinearDepthToUnityDepth(linearDepth);
                float depthCS = posCS.z/ posCS.w; // Perspective divide to get depth in clip space
                #if SHADER_API_OPENGL || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3	 
					depthCS = 0.5* depthCS + 0.5;
				#endif
				return depthCS;    
            }
            
            float4 FS (Varyings input, out float Depth: SV_Depth) : SV_Target
            {
                // Discard particles which has density below a threshold
                clip(input.density - _DensityThreshold * 1000.0f);
                
                // Trransform uv origin to quad center
                float2 quadUV = TransformUV(input.uv);
                
                // discard the outer area of circle
                float radius = length(quadUV);
                clip(1 - radius);

                // Get Screen uv
                float2 pixelPos = input.positionCS.xy;
                float2 screenUV = pixelPos / _ScreenParams.xy;

                // Get fluid depth
                float fluidDepth = SAMPLE_TEXTURE2D(_FluidDepthTexture, sampler_FluidDepthTexture, screenUV).r;
                if(_DebugType == 1)
                    return float4(fluidDepth.xxx,1.0);

                // Get smoothed depth
                float smoothedDepth = SAMPLE_TEXTURE2D(_FluidSmoothedDepthTexture, sampler_FluidSmoothedDepthTexture, screenUV).r;
                if(_DebugType == 2)
                    return float4(smoothedDepth.xxx,1.0);
                
                // Get normal
                float3 normalVS = SAMPLE_TEXTURE2D(_FluidNormalTexture,sampler_FluidNormalTexture,screenUV);
                if(_DebugType == 3)
                    return float4(normalVS,1.0);
                float3 normalWS = TransformViewToWorldNormal(normalVS, true);

                // Get viewspace position
                float3 sphereNormalVS = ComputeSphereNormal(quadUV);
                float4 posVS = float4(input.positionVS.xyz + sphereNormalVS * 0.5,1.0);
                Depth = ComputeClipSpaceDepth(posVS);

                // Refract
                float thickness = SAMPLE_TEXTURE2D(_FluidThicknessTexture,sampler_FluidThicknessTexture,screenUV).r;
                if(_DebugType == 4)
                    return float4(thickness.xxx,1.0);
                float3 absorptionFactor = exp(-(_AbsorptionColor) * thickness);
                
                float eta = 1.0 / _IOR;
                
                float3 I = normalize(posVS);
                float3 I_world = normalize(mul(UNITY_MATRIX_I_V, I));
                float3 N = normalWS;
                float3 R = refract(I_world, N, eta);
                
                float2 refractOffset = normalVS.xy * R.xy * _ReflectScalar * saturate(thickness);
                float2 refractCoord = screenUV + refractOffset;
                // float2 refractCoord = screenUV;
                // float3 refractColor = SAMPLE_TEXTURE2D(_SceneColorTexture,sampler_SceneColorTexture,refractCoord).xyz * absorptionFactor;
                
                float3 refractColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture,sampler_CameraOpaqueTexture,refractCoord).xyz * absorptionFactor;
                
                // Reflect
                float3 O = reflect(I_world, N);
                float3 reflectColor = texCUBE(_SkyboxTexture, O).rgb;

                float cosTheta = saturate(dot(-I_world, N));
                float F0 = 0.02f; // non-electrical medium's F0 
                float fresnel = saturate(Fresnel_schlick(cosTheta, F0));
                // return fresnel;
                
                // base particle shading
                Light mainLight = GetMainLight();
                float3 lightDirVS = mul((float3x3)UNITY_MATRIX_V, mainLight.direction);
                if(_DebugType == 0)
                {
                    float NL01 = dot(sphereNormalVS,lightDirVS) * 0.5 + 0.5;
                    float3 particleShadingColor = lerp(_ParticleColor * 0.1,_ParticleColor,NL01);
                    return float4(particleShadingColor,1.0);
                }

                // composite final fluid luminance
                float3 transmittance = lerp(refractColor,_ScatterColor,_Turbidity * 0.5);
                float3 luminance = lerp(transmittance, reflectColor*0.5, fresnel);
                float sceneRawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture,screenUV).r;
                float sceneLinearDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float3 sceneColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture,sampler_CameraOpaqueTexture,screenUV).rgb;
                if(sceneLinearDepth <= fluidDepth)
                    luminance = sceneColor;
                float alpha = pow(saturate(thickness),3.0);
                return float4(luminance,alpha);

            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError" // Simplified fallback
}