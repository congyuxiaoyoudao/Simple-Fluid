Shader "Particle/FluidParticle"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle
            {
                float3 position;
                float3 velocity;
            };

            StructuredBuffer<Particle> _ParticlePositions; // 仅需位置
            float _Size;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                uint id :TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                //o.color = lerp(0,1,length(_ParticlePositions[instanceID].velocity));
                // 从缓冲区获取粒子位置
                float3 worldPos = _ParticlePositions[instanceID].position;
                
                // 调整大小（在观察空间缩放）
                //float4 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0));
                worldPos.xyz += v.vertex.xyz * _Size; 
                //viewPos.xyz += v.vertex.xyz * _Size; // 顶点偏移实现大小
                //float3 worldPos2 = mul(UNITY_MATRIX_I_V,viewPos);
                //o.color = step(0,dot(normalize(_ParticlePositions[instanceID].velocity),normalize(worldPos2 - worldPos)));
                o.normal = normalize(v.vertex.xyz);
                o.id = instanceID;
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos,1));
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {

                float3 color = float3(1,0,1) * step(0,dot(i.normal,_ParticlePositions[i.id].velocity));
                
                //color = 1;
                return float4(color,1);
                //return _Color;
            }
            ENDHLSL
        }
    }
}
