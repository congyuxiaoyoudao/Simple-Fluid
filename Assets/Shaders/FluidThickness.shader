Shader "PBF/FluidThickness"
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
            Name "PBF_Thickness_Pass"

            Blend One One

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
                float4 _Color;
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
                float4 particlePosVS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            static float2 TransformUV(float2 uv)
            {
                float2 transformedUV = uv * 2.0 - 1.0;
                return transformedUV;
            }

            static float3 ComputeSphereNormal(float2 uv)
            {
                float3 N = float3(0.0f,0.0f,0.0f);
                N.xy = uv;
                N.z = sqrt(1-uv*uv);
                return N;
            }
            
            Varyings VS (Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;

                // Get particle position from the Compute Buffer using vertex ID
                // Using the user's specified buffer name: _Particle
                float3 particleWorldPos = _ParticlePositions[instanceID].position;
                float4 viewPos = mul(UNITY_MATRIX_V,float4(particleWorldPos,1.0));

                output.particlePosVS = viewPos;
                // As billboard, the quad always faces camera
                // TODO: Scaler should be a function relative to camera's aspect and fovy and screen's size,
                viewPos.xyz += input.vertex.xyz * _Size;
                float3 vertexWorldPos = mul(UNITY_MATRIX_I_V,viewPos);

                output.positionCS = mul(UNITY_MATRIX_VP, float4(vertexWorldPos,1));
                output.uv = input.uv;
                return output;
            }

            float4 FS (Varyings input) : SV_Target
            {
                
                // Trransform uv origin to quad center
                float2 quadUV = TransformUV(input.uv);
                
                // discard the outer area of circle
                float radius = length(quadUV);
                clip(1 - radius);

                // Compute sphere normal
                float3 sphereNormalVS = ComputeSphereNormal(quadUV);
                
                // TODO: add a scalar to scale thickness
                float thickness = 0.1 * sphereNormalVS.z;
                
                return float4(thickness.xxx,1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError" // Simplified fallback
}