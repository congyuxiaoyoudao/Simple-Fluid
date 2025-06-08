Shader "PBF/FluidDepth"
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
            Name "PBF_Depth_Pass"

            Cull Off

            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment FS
            #pragma target 4.5

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
                float _Size;
            CBUFFER_END

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
                float4 positionWS : TEXCOORD2; // World position for depth calculation
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
                output.positionWS = float4(particleWorldPos, 1.0); // Store world position for depth calculation
                return output;
            }
            
			float LinearDepthToUnityDepth(float linearDepth)
			{
				float depth01 = (linearDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y);
				return (1 - (depth01 * _ZBufferParams.y)) / (depth01 * _ZBufferParams.x);
			}
            
            float FS (Varyings input, out float Depth: SV_Depth) : SV_Target
            {
                // FSOutput output;
                
                // Trransform uv origin to quad center
                float2 quadUV = TransformUV(input.uv);
                
                // discard the outer area of circle
                float radius = length(quadUV);
                clip(1 - radius);

                // Compute sphere normal
                float3 sphereNormalVS = ComputeSphereNormal(quadUV);
                
                // base shading
                // Light mainLight = GetMainLight();
                // float3 lightDirVS = mul((float3x3)UNITY_MATRIX_V, mainLight.direction);
                // float3 diffuse = _Color.xyz * max(0,dot(lightDirVS, sphereNormalVS));
                // output.color=float4(diffuse,1.0);
                
                // compute depth
                float4 fragPosVS = float4(input.positionVS.xyz + sphereNormalVS * 0.5, 1.0);
                float linearDepth = -(fragPosVS.z);
                
                float4 posCS = mul(UNITY_MATRIX_P,fragPosVS);
                float depth = posCS.z/ posCS.w; // Perspective divide to get depth in clip space
				Depth = depth;
                // #if defined(UNITY_REVERSED_Z)
                //     Depth = 1.0f - Depth; //d3d, metal to do it
                // #endif
                return linearDepth;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError" // Simplified fallback
}