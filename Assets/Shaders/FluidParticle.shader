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

            StructuredBuffer<Particle> _ParticlePositions; // ����λ��
            float _Size;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                // �ӻ�������ȡ����λ��
                float3 worldPos = _ParticlePositions[instanceID].position;
                
                // ������С���ڹ۲�ռ����ţ�
                float4 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0));
                viewPos.xyz += v.vertex.xyz * _Size; // ����ƫ��ʵ�ִ�С
                
                o.vertex = mul(UNITY_MATRIX_P, viewPos);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
