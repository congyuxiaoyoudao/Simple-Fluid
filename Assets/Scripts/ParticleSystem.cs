using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystem : MonoBehaviour
{
    [Header("Settings")]
    public int particleCount = 1000;                        // ��������
    public float areaSize = 5f;                             //���С
    public Vector3 gravity = new Vector3(0, 9.8f, 0);       //������С/����
    public float particleSize = 0.1f;                       //���Ӵ�С
    public Color particleColor = Color.white;               //������ɫ

    [Header("References")]
    public ComputeShader computeShader;                     //���������ɫ��
    public Mesh particleMesh;                               // ʹ��Quad��Sphere

    private ComputeBuffer _particleBuffer;
    private Material _material;                             //��Ⱦ���ӵĲ���

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
        CreateMaterial();
    }

    void InitializeBuffers()
    {
        // ��ʼ����������
        Particle[] particles = new Particle[particleCount];
        float spacing = areaSize * 2 / particleCount;
        for (int i = 0; i < particleCount; i++)
        {
            // ��������������ӱ���Debug�ĸ����ӵ����⣩
            // TODO:ƽ������ �������� ��������
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

            // ��ʼ�ٶ�Ϊ0
            particles[i].velocity = Vector3.zero;
        }

        // ����ComputeBuffer
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Particle));
        _particleBuffer = new ComputeBuffer(particleCount, stride);
        _particleBuffer.SetData(particles);//SetDataʱ������CPU��GPU֮��Ĵ���
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


        // ����Compute Shader����
        computeShader.SetBuffer(0, "_Particles", _particleBuffer);//SetBuffer�������Ŀ��Ժ��Բ���
        computeShader.SetFloat("_DeltaTime", Time.deltaTime);
        computeShader.SetFloat("_Gravity", gravity.y);
        computeShader.SetVector("_AreaCenter", transform.position);
        computeShader.SetVector("_AreaSize", new Vector3(areaSize, areaSize, areaSize));

        // ����Compute Shader
        int threadGroups = Mathf.CeilToInt(particleCount / 64f);
        Debug.Log("ThreadGroupsNum: " + threadGroups);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        // ���򻯻���С��
        Graphics.DrawMeshInstancedProcedural(
            particleMesh,
            0,
            _material,
            new Bounds(transform.position, Vector3.one * areaSize * 2),
            particleCount
        );
    }

    /// <summary>
    /// ��ʾ�ڴ����
    /// </summary>
    void OnDestroy()
    {
        _particleBuffer?.Release();
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
}
