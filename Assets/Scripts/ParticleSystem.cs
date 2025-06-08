using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ParticleSystem : MonoBehaviour
{
    [Header("Settings")]
    public int particleCount = 1000;                        // ��������
    public float areaSize = 5f;                             //���С
    public Vector3 gravity = new Vector3(0, 9.8f, 0);       //������С/����
    public float particleMass = 1.0f;                       //��������
    public float particleRadius = 1.0f;                     //���Ӱ뾶
    public float particleSize = 0.1f;                       //���Ӵ�С
    public Color particleColor = Color.white;               //������ɫ

    public float targetDensity = 1;                         //������ɫ
    public float pressureMultiplier = 1;                    //������ɫ
    public float viscosityStrength = 0;

    [Header("References")]
    public ComputeShader computeShader;                     // ���������ɫ��
    public Material debugMaterial;
    public Mesh particleMesh;                               // ʹ��Quad��Sphere

    [Header("Visibility Hash Table")]
    public GameObject hashTestGO;
    public bool onGizmos = false;

    private ComputeBuffer _particleBuffer;
    private ComputeBuffer _densityBuffer;
    private ComputeBuffer _predictPosBuffer;
    private ComputeBuffer _hashTableBuffer;
    private GraphicsBuffer _instanceBuffer;
    private Material _material;                             // ��Ⱦ���ӵĲ���

    /// <summary>
    /// �������� �������ݴ���
    /// </summary>
    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
    }

    void Start()
    {
        InitializeBuffers();
        SetupIndirectArgs();
        CreateMaterial();
    }

    void InitializeBuffers()
    {
        // ��ʼ����������
        Particle[] particles = new Particle[particleCount];
        float spacing = areaSize / Mathf.Ceil(Mathf.Sqrt(particleCount));
        // ����ÿ��ÿ�е�������
        int particlesPerRow = Mathf.CeilToInt(Mathf.Sqrt(particleCount));
        for (int i = 0; i < particleCount; i++)
        {
            // ��������������ӱ���Debug�ĸ����ӵ����⣩
            // TODO:ƽ������ �������� ��������
            //particles[i].position = new Vector3(
            //    -areaSize + spacing * i,
            //    Random.Range(0,areaSize),
            //    0
            //) + transform.position;

            // y=0ƽ�����������
            //particles[i].position = new Vector3(
            //   Random.Range(-areaSize, areaSize),
            //   //Random.Range(0, areaSize),
            //   0,
            //   Random.Range(-areaSize, areaSize)
            //) + transform.position;

            // y=0 ƽ���Ϲ�������
            int x = i % particlesPerRow;
            int z = i / particlesPerRow;

            particles[i].position = new Vector3(
                -areaSize + x * spacing,
                0, // Y��̶�Ϊ0
                -areaSize + z * spacing
            ) + transform.position;

            // ��ʼ�ٶ�Ϊ0
            particles[i].velocity = Vector3.zero;
        }

        // ����ComputeBuffer
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Particle));
        _particleBuffer = new ComputeBuffer(particleCount, stride);
        _particleBuffer.SetData(particles);//SetDataʱ������CPU��GPU֮��Ĵ���


        float[] density = new float[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            density[i] = 1;
        }

        int stride2 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(float));
        _densityBuffer = new ComputeBuffer(particleCount, stride2);
        _densityBuffer.SetData(density);

        int stride3 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3));
        _predictPosBuffer = new ComputeBuffer(particleCount, stride3);
        //_predictPosBuffer.SetData(density);

        int stride4 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(int));
        _hashTableBuffer = new ComputeBuffer(particleCount, stride4);

    }

    // ��������ʼ����Ӳ�������
    void SetupIndirectArgs()
    {
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = particleMesh.GetIndexCount(0);    // ��������
        args[1] = (uint)particleCount;             // ʵ����������ʼΪ���ֵ��
        args[2] = particleMesh.GetIndexStart(0);    // ��ʼ����
        args[3] = particleMesh.GetBaseVertex(0);    // ��׼����
        args[4] = 0;                                // ��ʼʵ��ID

        _instanceBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1,
            args.Length * sizeof(uint)
        );
        _instanceBuffer.SetData(args);
    }
    public ComputeBuffer GetParticleBuffer() => _particleBuffer;
    public int GetParticleCount() => particleCount;

    void CreateMaterial()
    {
        // _material = new Material(Shader.Find("Particle/FluidParticle"));
        _material = new Material(Shader.Find("PBF/FluidDepth"));
        // _material = new Material(Shader.Find("PBF/FluidThickness"));
        _material.SetBuffer("_ParticlePositions", _particleBuffer);
        _material.SetColor("_Color", particleColor);
        _material.SetFloat("_Size", particleSize);

        if (debugMaterial != null) {
            debugMaterial.SetBuffer("_Particle", _particleBuffer);
            debugMaterial.SetFloat("_ParticleCount", particleCount);
            debugMaterial.SetFloat("_ParticleMass", particleMass);
            debugMaterial.SetFloat("_ParticleRadius", particleRadius);
            debugMaterial.SetFloat("_TargetDensity", targetDensity);
            debugMaterial.SetFloat("_PressureMultiplier", pressureMultiplier);
        }
        else
        {
            Debug.LogWarning("Debug material is null.");
        }

    }

    void Update()
    {
        if (!IsValid()) return;


        // ����Compute Shader����
        computeShader.SetBuffer(0, "_Particles", _particleBuffer);//SetBuffer�������Ŀ��Ժ��Բ���
        computeShader.SetBuffer(0, "_Density", _densityBuffer);//SetBuffer�������Ŀ��Ժ��Բ���
        computeShader.SetBuffer(0, "_PredictPosition", _predictPosBuffer);//SetBuffer�������Ŀ��Ժ��Բ���
        computeShader.SetBuffer(0, "_HashTable", _hashTableBuffer);
        computeShader.SetFloat("_ParticleCount", particleCount);
        computeShader.SetFloat("_ParticleMass", particleMass);
        computeShader.SetFloat("_ParticleRadius", particleRadius);
        computeShader.SetFloat("_DeltaTime", Time.deltaTime);
        computeShader.SetFloat("_Gravity", gravity.y);
        computeShader.SetFloat("_ViscosityStrength",viscosityStrength);
        computeShader.SetVector("_AreaCenter", transform.position);
        computeShader.SetVector("_AreaSize", new Vector3(areaSize, areaSize, areaSize));

        computeShader.SetFloat("_TargetDensity", targetDensity);
        computeShader.SetFloat("_PressureMultiplier", pressureMultiplier);

        // ����Compute Shader
        int threadGroups = Mathf.CeilToInt(particleCount / 64f);
        Debug.Log("ThreadGroupsNum: " + threadGroups);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        computeShader.SetBuffer(1, "_Particles", _particleBuffer);//SetBuffer�������Ŀ��Ժ��Բ���
        computeShader.SetBuffer(1, "_Density", _densityBuffer);//SetBuffer�������Ŀ��Ժ��Բ���
        computeShader.SetBuffer(1, "_PredictPosition", _predictPosBuffer);
        computeShader.Dispatch(1, threadGroups, 1, 1);

        computeShader.SetBuffer(2, "_Particles", _particleBuffer);//SetBuffer�������Ŀ��Ժ��Բ���
        //computeShader.SetBuffer(1, "_Density", _densityBuffer);//SetBuffer�������Ŀ��Ժ��Բ���
        computeShader.Dispatch(2, threadGroups, 1, 1);

        // HashKeyDebug
        int[] hashKey = new int[particleCount];
        _hashTableBuffer.GetData(hashKey);
        //Debug.Log("HashKey: " + hashKey[0]+ hashKey[1]+ hashKey[2]);

        // ���򻯻���С��
        //Graphics.DrawMeshInstancedProcedural(
        //    particleMesh,
        //    0,
        //    _material,
        //    new Bounds(transform.position, Vector3.one * areaSize * 2),
        //    particleCount
        //);


        // Graphics.DrawMeshInstancedIndirect(
        //     particleMesh,
        //     0,
        //     _material,
        //     new Bounds(transform.position, Vector3.one * areaSize * 2),
        //     _instanceBuffer
        // );

        if (debugMaterial != null)
        {
            debugMaterial.SetBuffer("_Particles", _particleBuffer);
            debugMaterial.SetFloat("_ParticleCount", particleCount);
            debugMaterial.SetFloat("_ParticleMass", particleMass);
            debugMaterial.SetFloat("_ParticleRadius", particleRadius);
        }
        else
        {
            Debug.LogWarning("Debug material is null.");
        }
    }

    /// <summary>
    /// ��ʾ�ڴ����
    /// </summary>
    void OnDestroy()
    {
        _particleBuffer?.Release();
        _instanceBuffer?.Release();
        _densityBuffer?.Release();
        _predictPosBuffer?.Release();
        _hashTableBuffer?.Release();
        Destroy(_material);
    }

    /// <summary>
    /// ��ʾ���ǵ����С
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * areaSize * 2);
    }

    /// <summary>
    /// ��ʾ�Ƿ��ִ��
    /// </summary>
    /// <returns></returns>
    bool IsValid()
    {
        if (computeShader == null)
        {
            Debug.Log("ComputeShader is null.");
            return false;
        }
        if (particleMesh == null)
        {
            Debug.Log("Particle mesh is null.");
            return false;
        }
        if (_material == null)
        {
            Debug.Log("Can't find shader.");
            return false;
        }
        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.Log("Dont support compute shader.");
            return false;
        }
        return true;
    }


    //public void HashTableTestGizmos()
    //{
    //    if (hashTestGO != null && onGizmos == true)
    //    {
    //        Vector3 hashPosition = hashTestGO.transform.position;
    //        Vector3 cellCoord = hashPosition-transform.position;
    //        int cellHash = Mathf.FloorToInt(cellCoord.x)* 73856093 + Mathf.FloorToInt(cellCoord.y);
    //    }
        
    //    // Hash
    //    static float3 PositionToCellCoord(float3 position)
    //    {
    //        return float3(floor((position - _AreaCenter) / 1.1 * _ParticleRadius));
    //    }

    //    static int CellCoordToCellHash(float3 cellCoord)
    //    {
    //        const int p1 = 73856093;
    //        const int p2 = 19349663;
    //        const int p3 = 83492791;
    //        return (cellCoord.x * p1) + (cellCoord.y * p2) + (cellCoord.z * p3);
    //    }

    //    // TODO: Solve the hash collision
    //    static int CellHashToHashKey(int cellHash)
    //    {
    //        return cellHash % 8191;
    //    }
    //}
}
