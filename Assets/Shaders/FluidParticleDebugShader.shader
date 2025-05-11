Shader "Particle/FluidParticleDebugShader"
{
    Properties
    {
        _Threshold("Threshold",float) = 0.2
        _Color("positive density Color",color) = (1,1,1,1)
        _Color2("zero density Color",color) = (0.5,0.5,0.5,0.5)
        _Color3("negative density Color",color) = (0,0,0,0)
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

            StructuredBuffer<Particle> _Particles; // ½öÐèÎ»ÖÃ

            float4 _Color;
            float4 _Color2;
            float4 _Color3;
            float _Threshold;

            // debugC#
            float _ParticleMass;
            float _ParticleCount;
            float _ParticleRadius;
            float _TargetDensity;
            float _PressureMultiplier;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };



            static float Pow8(float value)
            {
                return value * value * value * value * value * value * value * value;
            }

            static float Pow9(float value)
            {
                return value * value * value * value * value * value * value * value * value;
            }

            static float ConvertDensityToPressure(float density)
            {
                float densityError = density - _TargetDensity;
                float pressure = densityError * _PressureMultiplier;
                return pressure;
            }

            static float Function2D(float dst, float radius)
            {
                float volumn = PI * Pow8(radius) / 4;
                float value = max(0, radius * radius - dst * dst);
                return value * value * value / volumn;
            }

            static float CalculateDensity2D(float3 position)
            {
                float density = 0;
                float mass = _ParticleMass;
                for (int i = 0; i < _ParticleCount; i++)
                {
                    float dst = length(_Particles[i].position.xz - position.xz);
                    float influence = Function2D(dst,_ParticleRadius);
                    density += mass * influence;
                }
                return density;
            }

            static float Function3D(float dst, float radius)
            {
                float volumn = 64 * PI * Pow9(radius) / 315;
                float value = max(0, radius * radius - dst * dst);
                return value * value * value / volumn;
            }

            static float SharpingKernel2D(float dst, float radius)
            {
                float volumn = PI * Pow4(radius) / 2;
                float value = max(0, radius - dst);
                return value * value * value / volumn;
            }

            static float SharpingKernelDerivative2D(float dst, float radius)
            {
                if (dst > radius)
                    return 0;
                float f = radius - dst;
                float scale = -6 / (PI * Pow4(radius));
                return scale * f * f;
            }

            static float CalculateDensity3D(float3 position)
            {
                float density = 0;
                float mass = _ParticleMass;
                for (int i = 0; i < _ParticleCount; i++)
                {
                    float dst = length(_Particles[i].position.xz - position.xz);
                    float influence = Function3D(dst,_ParticleRadius);
                    density += mass * influence;
                }
                return density;
            }

            static float CalculateDensitySharp2D(float3 position)
            {
                float density = 0;
                float mass = _ParticleMass;
                for (int i = 0; i < _ParticleCount; i++)
                {
                    float dst = length(_Particles[i].position.xz - position.xz);
                    float influence = SharpingKernel2D(dst,_ParticleRadius);
                    density += mass * influence;
                }
                return density;
            }

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;

                o.vertex = TransformObjectToHClip(v.vertex);
                o.worldPos = TransformObjectToWorld(v.vertex);

                //o.density = CalculateDensity(worldPos);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 worldPos = i.worldPos;
                float density = CalculateDensitySharp2D(worldPos);
                float pressure = ConvertDensityToPressure(density);

                float positiveRange = step(_Threshold,pressure);
                float equalRange = step(-_Threshold,pressure) * step(pressure,_Threshold);
                float negativeRange = step(pressure,-_Threshold);
                float3 color = positiveRange * _Color + equalRange *_Color2+negativeRange*_Color3;
                color = density;
                return float4(color,1);
                //return _Color;
            }
            ENDHLSL
        }
    }
}
