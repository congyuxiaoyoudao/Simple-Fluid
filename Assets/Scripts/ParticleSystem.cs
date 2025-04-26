using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystem : MonoBehaviour
{
    [Header("Settings")]
    public int particleCount = 1000;                        // 粒子数量
    public float areaSize = 5f;                             //域大小
    public Vector3 gravity = new Vector3(0, 9.8f, 0);       //重力大小/方向
    public float particleSize = 0.1f;                       //粒子大小
    public Color particleColor = Color.white;               //例子颜色

    [Header("References")]
    public ComputeShader computeShader;                     //输入计算着色器
    public Mesh particleMesh;                               // 使用Quad或Sphere

    private ComputeBuffer _particleBuffer;
    private Material _material;                             //渲染粒子的材质

    /// <summary>
    /// 粒子数据 用于数据传输
    /// </summary>
    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
    }

    void Start()
    {
        InitializeBuffers();
        CreateMaterial();
    }

    void InitializeBuffers()
    {
        // 初始化粒子数据
        Particle[] particles = new Particle[particleCount];
        float spacing = areaSize * 2 / particleCount;
        for (int i = 0; i < particleCount; i++)
        {
            // 逐个排序（少数粒子便于Debug哪个粒子的问题）
            // TODO:平面生成 立体生成 网格生成
            //particles[i].position = new Vector3(
            //    -areaSize + spacing * i,
            //    Random.Range(0,areaSize),
            //    0
            //) + transform.position;
            particles[i].position = new Vector3(
               Random.Range(-areaSize, areaSize),
               Random.Range(0, areaSize),
               Random.Range(-areaSize, areaSize)
            ) + transform.position;

            // 初始速度为0
            particles[i].velocity = Vector3.zero;
        }

        // 创建ComputeBuffer
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Particle));
        _particleBuffer = new ComputeBuffer(particleCount, stride);
        _particleBuffer.SetData(particles);//SetData时会增加CPU与GPU之间的带宽
    }

    void CreateMaterial()
    {
        _material = new Material(Shader.Find("Particle/FluidParticle"));
        _material.SetBuffer("_ParticlePositions", _particleBuffer);
        _material.SetColor("_Color", particleColor);
        _material.SetFloat("_Size", particleSize);
    }

    void Update()
    {
        if (!IsValid()) return;


        // 更新Compute Shader参数
        computeShader.SetBuffer(0, "_Particles", _particleBuffer);//SetBuffer带宽消耗可以忽略不计
        computeShader.SetFloat("_DeltaTime", Time.deltaTime);
        computeShader.SetFloat("_Gravity", gravity.y);
        computeShader.SetVector("_AreaCenter", transform.position);
        computeShader.SetVector("_AreaSize", new Vector3(areaSize, areaSize, areaSize));

        // 调度Compute Shader
        int threadGroups = Mathf.CeilToInt(particleCount / 64f);
        Debug.Log("ThreadGroupsNum: " + threadGroups);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        // 程序化绘制小球
        Graphics.DrawMeshInstancedProcedural(
            particleMesh,
            0,
            _material,
            new Bounds(transform.position, Vector3.one * areaSize * 2),
            particleCount
        );
    }

    /// <summary>
    /// 显示内存管理
    /// </summary>
    void OnDestroy()
    {
        _particleBuffer?.Release();
        Destroy(_material);
    }

    /// <summary>
    /// 显示我们的域大小
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * areaSize * 2);
    }

    /// <summary>
    /// 显示是否可执行
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
}
