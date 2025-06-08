using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystem : MonoBehaviour
{
    [Header("Settings")]
    public int particleCount = 1000;                        // 粒子数量
    public float areaSize = 5f;                             //域大小
    public Vector3 gravity = new Vector3(0, 9.8f, 0);       //重力大小/方向
    public float particleMass = 1.0f;                       //粒子质量
    public float particleRadius = 1.0f;                     //粒子半径
    public float particleSize = 0.1f;                       //粒子大小
    public Color particleColor = Color.white;               //粒子颜色

    public float targetDensity = 1;                         //目标密度
    public float pressureMultiplier = 1;                    //压力系数
    public float viscosityStrength = 1;                     //粘度系数

    [Header("References")]
    public ComputeShader computeShader;                     // 锟斤拷锟斤拷锟斤拷锟斤拷锟缴拷锟?
    public Material debugMaterial;
    public Mesh particleMesh;                               // 使锟斤拷Quad锟斤拷Sphere

    [Header("Visibility Hash Table")]
    public GameObject hashTestGO;
    public bool onGizmos = false;

    private ComputeBuffer _particleBuffer;
    private ComputeBuffer _particleIndexBuffer;
    private ComputeBuffer _particleIndexTempBuffer;
    private ComputeBuffer _densityBuffer;
    private ComputeBuffer _predictPosBuffer;
    private ComputeBuffer _hashTableBuffer;
    private ComputeBuffer _hashTableTempBuffer;
    private ComputeBuffer _localPrefixSumBuffer;
    private ComputeBuffer _blockDataBuffer;
    private ComputeBuffer _blockPrefixSumBuffer;
    private ComputeBuffer _blockPrefixSumOutputBuffer;
    private ComputeBuffer _globalPositionBuffer;

    private ComputeBuffer _cellStartBuffer;
    private ComputeBuffer _cellEndBuffer;

    private ComputeBuffer _tempBuffer;
    private ComputeBuffer _temp1Buffer;
    private ComputeBuffer _temp2Buffer;

    private GraphicsBuffer _instanceBuffer;
    private Material _material;                             // 锟斤拷染锟斤拷锟接的诧拷锟斤拷

    private int[] hashKey;
    private int[] hash4bitsKey;
    private int[] tempData;
    private int[] temp1Data;
    private int[] temp2Data;
    private int[] cellStartData;


    // 参数提前声明
    private static int calculateDensityKernelID = -1,
                    calculateHashTableKernelID = -1,
                    calculatePositionKernelID = -1,
                    calculateVelocityKernelID = -1,
                    calculateLocalPrefixSumKernelID = -1,
                    calculateGlobalPrefixSumKernelID = -1,
                    calculateGlobalPrefixSum2KernelID = -1,
                    executeRadixSortKernelID = -1,
                    changeParticlesIndexKernelID,
                    initStartAndEndIndexKernelID = -1,
                    initParticleIndexKernelID = -1,
                    initBlockDataKernelID = -1,
                    calculateStartAndEndIndexKernelID = -1;

    private static readonly int threadGroupCountID = Shader.PropertyToID("_ThreadGroupCount"),
        particlesID = Shader.PropertyToID("_Particles"),
        particleIndexID = Shader.PropertyToID("_ParticleIndex"),
        particleCountID = Shader.PropertyToID("_ParticleCount"),
        particleMassID = Shader.PropertyToID("_ParticleMass"),
        particleRadiusID = Shader.PropertyToID("_ParticleRadius"),
        gravityID = Shader.PropertyToID("_Gravity"),
        densityID = Shader.PropertyToID("_Density"),
        targetDensityID = Shader.PropertyToID("_TargetDensity"),
        predictPositionID = Shader.PropertyToID("_PredictPosition"),
        pressureMultiplierID = Shader.PropertyToID("_PressureMultiplier"),
        viscosityStrengthID = Shader.PropertyToID("_ViscosityStrength"),
        areaCenterID = Shader.PropertyToID("_AreaCenter"),
        areaSizeID = Shader.PropertyToID("_AreaSize"),
        deltaTimeID = Shader.PropertyToID("_DeltaTime"),
        hashTableID = Shader.PropertyToID("_HashTable"),
        localPrefixSumDataID = Shader.PropertyToID("_LocalPrefixSumData"),
        blockDataID = Shader.PropertyToID("_BlockData"),
        blockPrefixSumDataID = Shader.PropertyToID("_BlockPrefixSumData"),
        blockPrefixSumOutputID = Shader.PropertyToID("_BlockPrefixSumOutput"),
        cellStartID = Shader.PropertyToID("_CellStart"),
        cellEndID = Shader.PropertyToID("_CellEnd"),
        tempDataID = Shader.PropertyToID("_TempData"),
        temp1DataID = Shader.PropertyToID("_Temp1Data"),
        temp2DataID = Shader.PropertyToID("_Temp2Data")
        ;

    private static readonly int particleStructStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Particle)),
        floatStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(float)),
        vector3Stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)),
        intStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(int))
        ;


    /// <summary>
    /// 锟斤拷锟斤拷锟斤拷锟斤拷 锟斤拷锟斤拷锟斤拷锟捷达拷锟斤拷
    /// </summary>
    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
    }

    void Start()
    {
        FindAllKernels();
        InitializeBuffers();
        SetupIndirectArgs();
        CreateMaterial();
    }

    // 获取所有需要的Kernel
    void FindAllKernels()
    {
        calculateHashTableKernelID = computeShader.FindKernel("CalculateHashTable");
        calculateDensityKernelID = computeShader.FindKernel("CalculateDensity");
        calculateVelocityKernelID = computeShader.FindKernel("CalculateVelocity");
        calculatePositionKernelID = computeShader.FindKernel("CalculatePosition");
        calculateLocalPrefixSumKernelID = computeShader.FindKernel("CalculateLocalPrefixSum");
        calculateGlobalPrefixSumKernelID = computeShader.FindKernel("CalculateGlobalPrefixSum");
        calculateGlobalPrefixSum2KernelID = computeShader.FindKernel("CalculateGlobalPrefixSum2");
        changeParticlesIndexKernelID = computeShader.FindKernel("ChangeParticlesIndex");
        executeRadixSortKernelID = computeShader.FindKernel("ExecuteRadixSort");
        initStartAndEndIndexKernelID = computeShader.FindKernel("InitStartAndEndIndex");
        initParticleIndexKernelID = computeShader.FindKernel("InitParticleIndex");
        initBlockDataKernelID = computeShader.FindKernel("InitBlockData");
        calculateStartAndEndIndexKernelID = computeShader.FindKernel("CalculateStartAndEndIndex");
    }

    void InitializeBuffers()
    {
        // 锟斤拷始锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷
        Particle[] particles = new Particle[particleCount];

        DistributeInVolumeRandom(particles , particleMesh , particleCount);  //随机分布
        //DistributeInVolume(particles, particleMesh, particleCount);            //按体素分布

        //float spacing = areaSize / Mathf.Ceil(Mathf.Sqrt(particleCount));
        //// 锟斤拷锟斤拷每锟斤拷每锟叫碉拷锟斤拷锟斤拷锟斤拷
        //int particlesPerRow = Mathf.CeilToInt(Mathf.Sqrt(particleCount));
        //for (int i = 0; i < particleCount; i++)
        //{
        //    // 锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷颖锟斤拷锟紻ebug锟侥革拷锟斤拷锟接碉拷锟斤拷锟解）
        //    // TODO:平锟斤拷锟斤拷锟斤拷 锟斤拷锟斤拷锟斤拷锟斤拷 锟斤拷锟斤拷锟斤拷锟斤拷
        //    //particles[i].position = new Vector3(
        //    //    -areaSize + spacing * i,
        //    //    Random.Range(0,areaSize),
        //    //    0
        //    //) + transform.position;

        //    // y=0平锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷锟?
        //    //particles[i].position = new Vector3(
        //    //   Random.Range(-areaSize, areaSize),
        //    //   //Random.Range(0, areaSize),
        //    //   0,
        //    //   Random.Range(-areaSize, areaSize)
        //    //) + transform.position;

        //    // y=0 平锟斤拷锟较癸拷锟斤拷锟斤拷锟斤拷
        //    int x = i % particlesPerRow;
        //    int z = i / particlesPerRow;

        //    particles[i].position = new Vector3(
        //        -areaSize + x * spacing,
        //        0, // Y锟斤拷潭锟轿?
        //        -areaSize + z * spacing
        //    ) + transform.position;

        // 锟斤拷始锟劫讹拷为0
        //    particles[i].velocity = Vector3.zero;
        //}

        // 创建ComputeBuffer
        _particleBuffer = new ComputeBuffer(particleCount, particleStructStride);
        _particleBuffer.SetData(particles);//SetData时会增加CPU与GPU之间的带宽
        _particleIndexBuffer = new ComputeBuffer(particleCount, intStride);
        _particleIndexTempBuffer = new ComputeBuffer(particleCount, intStride);


        float[] density = new float[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            density[i] = 1;
        }

        _densityBuffer = new ComputeBuffer(particleCount, floatStride);
        _densityBuffer.SetData(density);

        _predictPosBuffer = new ComputeBuffer(particleCount, vector3Stride);
        
        //_predictPosBuffer.SetData(density);

        _hashTableBuffer = new ComputeBuffer(particleCount, intStride);
        _hashTableTempBuffer = new ComputeBuffer(particleCount, intStride);
        _localPrefixSumBuffer = new ComputeBuffer(particleCount, intStride);

        _globalPositionBuffer = new ComputeBuffer(particleCount, intStride);


        int threadGroups = Mathf.CeilToInt(particleCount / 64f);
        _blockDataBuffer = new ComputeBuffer(threadGroups * 16, intStride);
        _blockPrefixSumBuffer = new ComputeBuffer(threadGroups * 16, intStride);
        _blockPrefixSumOutputBuffer = new ComputeBuffer(threadGroups * 16, intStride);

        _cellStartBuffer = new ComputeBuffer(particleCount, intStride);
        _cellEndBuffer = new ComputeBuffer(particleCount, intStride);
        //int []cellData = new int[particleCount];
        //for (int i = 0; i < particleCount; i++)
        //{
        //    cellData[i] = int.MaxValue;
        //}
        //_cellStartBuffer.SetData(cellData);
        //_cellEndBuffer.SetData(cellData);

        int[] particleIndexData = new int[particleCount];
        for (int i = 0; i < particleCount; i++) {
            particleIndexData[i] = i;
        }
        _particleIndexBuffer.SetData(particleIndexData);

        // temp buffer
        _tempBuffer = new ComputeBuffer(particleCount, intStride);
        _temp1Buffer = new ComputeBuffer(particleCount, intStride);
        _temp2Buffer = new ComputeBuffer(particleCount, intStride);
    }

    // 锟斤拷锟斤拷锟斤拷锟斤拷始锟斤拷锟斤拷硬锟斤拷锟斤拷锟斤拷锟?
    void SetupIndirectArgs()
    {
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = particleMesh.GetIndexCount(0);    // 锟斤拷锟斤拷锟斤拷锟斤拷
        args[1] = (uint)particleCount;             // 实锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷始为锟斤拷锟街碉拷锟?
        args[2] = particleMesh.GetIndexStart(0);    // 锟斤拷始锟斤拷锟斤拷
        args[3] = particleMesh.GetBaseVertex(0);    // 锟斤拷准锟斤拷锟斤拷
        args[4] = 0;                                // 锟斤拷始实锟斤拷ID

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


        ExecuteComputeShader();

        //int threadGroups = Mathf.CeilToInt(particleCount / 64f);
        // HashKeyDebug
        
        //tempData = new int[particleCount];
        //_tempBuffer.GetData(tempData);
        //int[] debugData = new int[threadGroups * 16];
        //_localPrefixSumBuffer.GetData(debugData);
        //Debug.Log("HashKey: " + string.Join(", ", hashKey));
        //Debug.Log("Temp Data: " + string.Join(", ", tempData));
        //Debug.Log("IsValid: " + IsUniqueAndAllLessThan(tempData, 2048));
        //Debug.Log("PreSum: " + debugData[0] + " " + debugData[1] + " " + debugData[2] + " " + debugData[3] + " " + debugData[4] + " " + debugData[5] + " " + debugData[6] + " " + debugData[7]);

        // 锟斤拷锟津化伙拷锟斤拷小锟斤拷
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

    int get4Bits(int value, int iteration)
    {
        int shift = iteration * 4;
        return (value >> shift) & 0xF;
    }

    void ExecuteComputeShader()
    {
        int threadGroups = Mathf.CeilToInt(particleCount / 64f);  // 1024/64 = 16       2048/64 = 32    4096/64 = 64
        int threadGroup2 = Mathf.CeilToInt(threadGroups * 16);    //  16 * 16 = 256     32*16 = 512     64 * 16 = 1024

        ///////////////////////////////////////////////////
        ///         执行前初始值（Start）               ///
        ///         ParticlePosition                    ///
        ///         ParticleDensity = 1                 ///
        ///         ParticleIndex = i                   ///
        ///         执行前初始值（Update）              ///
        ///         ParticlePosition（Calculate）       ///
        ///         ParticleDensity （Calculate）       ///
        ///         ParticleIndex = i                   ///
        ///////////////////////////////////////////////////

        computeShader.SetFloat(particleCountID, particleCount);
        computeShader.SetFloat("_ParticleSize", particleSize);
        computeShader.SetFloat(particleMassID, particleMass);
        computeShader.SetFloat(particleRadiusID, particleRadius);
        computeShader.SetFloat(deltaTimeID, Time.deltaTime);
        computeShader.SetFloat(gravityID, gravity.y);
        computeShader.SetFloat(viscosityStrengthID, viscosityStrength);
        computeShader.SetVector(areaCenterID, transform.position);
        computeShader.SetVector(areaSizeID, new Vector3(areaSize, areaSize, areaSize));

        computeShader.SetFloat(targetDensityID, targetDensity);
        computeShader.SetFloat(pressureMultiplierID, pressureMultiplier);


        //// 根据Particle的Position值初始化HashTable的值
        //computeShader.SetBuffer(calculateHashTableKernelID, particlesID, _particleBuffer);//SetBuffer带宽消耗可以忽略不计
        //computeShader.SetBuffer(calculateHashTableKernelID, hashTableID, _hashTableBuffer);
        //computeShader.Dispatch(calculateHashTableKernelID, threadGroups, 1, 1);



        //for (int i = 1; i <= 3; i++)
        //{
        //    // 初始化BlockData的值为 0
        //    computeShader.SetBuffer(initBlockDataKernelID, blockDataID, _blockDataBuffer);
        //    computeShader.Dispatch(initBlockDataKernelID, threadGroup2, 1, 1);

        //    // 计算LocalPrefixSum
        //    computeShader.SetInt("_Digit", i - 1);
        //    computeShader.SetBuffer(calculateLocalPrefixSumKernelID, hashTableID, _hashTableBuffer);
        //    computeShader.SetBuffer(calculateLocalPrefixSumKernelID, blockDataID, _blockDataBuffer);
        //    computeShader.SetBuffer(calculateLocalPrefixSumKernelID, localPrefixSumDataID, _localPrefixSumBuffer);
        //    computeShader.SetBuffer(calculateLocalPrefixSumKernelID, temp2DataID, _temp2Buffer);
        //    computeShader.Dispatch(calculateLocalPrefixSumKernelID, threadGroups, 1, 1);

        //    // 获取BlockData数据 便于Debug
        //    //temp2Data = new int[threadGroup2];
        //    //_blockDataBuffer.GetData(temp2Data);

        //    // 计算GlobalPrefixSum
        //    computeShader.SetBuffer(calculateGlobalPrefixSumKernelID, blockDataID, _blockDataBuffer);
        //    computeShader.SetBuffer(calculateGlobalPrefixSumKernelID, blockPrefixSumDataID, _blockPrefixSumBuffer);
        //    computeShader.SetBuffer(calculateGlobalPrefixSumKernelID, blockPrefixSumOutputID, _blockPrefixSumOutputBuffer);
        //    computeShader.Dispatch(calculateGlobalPrefixSumKernelID, threadGroup2, 1, 1);

        //    computeShader.SetBuffer(calculateGlobalPrefixSum2KernelID, blockDataID, _blockDataBuffer);
        //    computeShader.SetBuffer(calculateGlobalPrefixSum2KernelID, blockPrefixSumDataID, _blockPrefixSumBuffer);
        //    computeShader.SetBuffer(calculateGlobalPrefixSum2KernelID, blockPrefixSumOutputID, _blockPrefixSumOutputBuffer);
        //    computeShader.Dispatch(calculateGlobalPrefixSum2KernelID, threadGroup2, 1, 1);

        //    // 获取待排序的Hash数据 用于Debug
        //    //hashKey = new int[particleCount];
        //    //hash4bitsKey = new int[particleCount];
        //    //_hashTableBuffer.GetData(hashKey);
        //    //for (int j = 0; j < particleCount; j++)
        //    //{
        //    //    hash4bitsKey[j] = get4Bits(hashKey[j], i - 1);
        //    //}
        //    //Debug.Log("Sort hash" + i + ":   " + string.Join(", ", hashKey));
        //    //Debug.Log("Sort 4bits hash" + i + ":   " + string.Join(", ", hash4bitsKey));

        //    // 计算GlobalPosition
        //    computeShader.SetBuffer(executeRadixSortKernelID, hashTableID, _hashTableBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, blockDataID, _blockDataBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, localPrefixSumDataID, _localPrefixSumBuffer);
        //    //computeShader.SetBuffer(executeRadixSortKernelID, particlesID, _particleBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, particleIndexID, _particleIndexBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, "_HashTableTemp", _hashTableTempBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, "_ParticleIndexTemp", _particleIndexTempBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, "_GlobalPosition", _globalPositionBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, blockPrefixSumDataID, _blockPrefixSumOutputBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, tempDataID, _tempBuffer);
        //    computeShader.SetBuffer(executeRadixSortKernelID, temp1DataID, _temp1Buffer);
        //    //computeShader.SetBuffer(executeRadixSortKernelID, temp2DataID, _temp2Buffer);
        //    computeShader.Dispatch(executeRadixSortKernelID, threadGroups, 1, 1);

        //    // 调试GlobalPosition数据的准确性
        //    //tempData = new int[particleCount];
        //    //_tempBuffer.GetData(tempData);
        //    //DebugSort(tempData, 8192, i);

        //    // 根据GlobalPosition重映射
        //    computeShader.SetBuffer(changeParticlesIndexKernelID, "_GlobalPosition", _globalPositionBuffer);
        //    computeShader.SetBuffer(changeParticlesIndexKernelID, "_ParticleIndexTemp", _particleIndexTempBuffer);

        //    computeShader.SetBuffer(changeParticlesIndexKernelID, "_HashTableTemp", _hashTableTempBuffer);
        //    computeShader.SetBuffer(changeParticlesIndexKernelID, hashTableID, _hashTableBuffer);
        //    computeShader.SetBuffer(changeParticlesIndexKernelID, particleIndexID, _particleIndexBuffer);
        //    computeShader.Dispatch(changeParticlesIndexKernelID, threadGroups, 1, 1);

        //    // 调试排序后的Hash值
        //    //hashKey = new int[particleCount];
        //    //_hashTableBuffer.GetData(hashKey);
        //    //for (int j = 0; j < particleCount; j++)
        //    //{
        //    //    hash4bitsKey[j] = get4Bits(hashKey[j], i - 1);
        //    //}
        //    //Debug.Log("Sorted hash" + i + ":   " + string.Join(", ", hashKey));
        //    //Debug.Log("Sorted 4bits hash" + i + ":   " + string.Join(", ", hash4bitsKey));
        //}

        //// 初始化Hash表索引
        //computeShader.SetBuffer(initStartAndEndIndexKernelID, cellStartID, _cellStartBuffer);
        //computeShader.SetBuffer(initStartAndEndIndexKernelID, cellEndID, _cellEndBuffer);
        //computeShader.Dispatch(initStartAndEndIndexKernelID, threadGroups, 1, 1);

        //// 计算Hash表索引
        //computeShader.SetBuffer(calculateStartAndEndIndexKernelID, hashTableID, _hashTableBuffer);
        //computeShader.SetBuffer(calculateStartAndEndIndexKernelID, cellStartID, _cellStartBuffer);
        //computeShader.SetBuffer(calculateStartAndEndIndexKernelID, cellEndID, _cellEndBuffer);
        //computeShader.Dispatch(calculateStartAndEndIndexKernelID, threadGroups, 1, 1);


        // Debug索引值
        //cellStartData = new int[particleCount];
        //_cellStartBuffer.GetData(cellStartData);
        //Debug.Log("CellStartBuffer" + ":   " + string.Join(", ", cellStartData));

        //// Debug最后的Hash值
        //hashKey = new int[particleCount];
        //_hashTableBuffer.GetData(hashKey);
        //Debug.Log("Sorted hash: " + string.Join(", ", hashKey));

        //// Debug排序后的粒子下标
        //int[] debugParticleIndexData = new int[particleCount];
        //_particleIndexBuffer.GetData(debugParticleIndexData);
        //Debug.Log("ParticleIndex: " + string.Join(", ", debugParticleIndexData));

        // 计算Density
        computeShader.SetBuffer(calculateDensityKernelID, particlesID, _particleBuffer);//SetBuffer带宽消耗可以忽略不计
        computeShader.SetBuffer(calculateDensityKernelID, particleIndexID, _particleIndexBuffer);
        computeShader.SetBuffer(calculateDensityKernelID, densityID, _densityBuffer);//SetBuffer带宽消耗可以忽略不计
        computeShader.SetBuffer(calculateDensityKernelID, predictPositionID, _predictPosBuffer);//SetBuffer带宽消耗可以忽略不计
        computeShader.SetBuffer(calculateDensityKernelID, hashTableID, _hashTableBuffer);
        computeShader.SetBuffer(calculateDensityKernelID, cellStartID, _cellStartBuffer);
        computeShader.SetBuffer(calculateDensityKernelID, cellEndID, _cellEndBuffer);
        computeShader.SetBuffer(calculateDensityKernelID, temp1DataID, _temp1Buffer);

        //Debug.Log("ThreadGroupsNum: " + threadGroups);
        computeShader.SetFloat("_ThreadGroupCount", threadGroups);
        computeShader.Dispatch(calculateDensityKernelID, threadGroups, 1, 1);

        //// Debug索引值
        //temp1Data = new int[particleCount];
        //_temp1Buffer.GetData(temp1Data);
        //Debug.Log("PositionXInt CellStartIndex" + ":   " + string.Join(", ", temp1Data));

        


        // 计算速度
        computeShader.SetBuffer(calculateVelocityKernelID, particlesID, _particleBuffer);//SetBuffer带宽消耗可以忽略不计
        computeShader.SetBuffer(calculateVelocityKernelID, densityID, _densityBuffer);//SetBuffer带宽消耗可以忽略不计
        computeShader.SetBuffer(calculateVelocityKernelID, predictPositionID, _predictPosBuffer);
        computeShader.SetBuffer(calculateVelocityKernelID, particleIndexID, _particleIndexBuffer);
        computeShader.SetBuffer(calculateVelocityKernelID, cellStartID, _cellStartBuffer);
        computeShader.SetBuffer(calculateVelocityKernelID, cellEndID, _cellEndBuffer);
        computeShader.Dispatch(calculateVelocityKernelID, threadGroups, 1, 1);

        // 计算位置
        computeShader.SetBuffer(calculatePositionKernelID, particlesID, _particleBuffer);//SetBuffer带宽消耗可以忽略不计
        computeShader.Dispatch(calculatePositionKernelID, threadGroups, 1, 1);

        // 初始化粒子索引值
        computeShader.SetBuffer(initParticleIndexKernelID, particleIndexID, _particleIndexBuffer);
        computeShader.Dispatch(initParticleIndexKernelID, threadGroups, 1, 1);


    }

    /// <summary>
    /// 锟斤拷示锟节达拷锟斤拷锟?
    /// </summary>
    void OnDestroy()
    {
        ReleaseComputeShaders();
        Destroy(_material);
    }

    void ReleaseComputeShaders()
    {
        _particleBuffer?.Release();
        _particleIndexBuffer?.Release();
        _particleIndexTempBuffer?.Release();
        _instanceBuffer?.Release();
        _densityBuffer?.Release();
        _predictPosBuffer?.Release();
        _hashTableBuffer?.Release();
        _hashTableTempBuffer?.Release();
        _localPrefixSumBuffer?.Release();
        _blockDataBuffer?.Release();
        _blockPrefixSumBuffer?.Release();
        _blockPrefixSumOutputBuffer?.Release();
        _globalPositionBuffer?.Release();
        _cellStartBuffer?.Release();
        _cellEndBuffer?.Release();
        _tempBuffer?.Release();
        _temp1Buffer?.Release();
        _temp2Buffer?.Release();
    }

    /// <summary>
    /// 锟斤拷示锟斤拷锟角碉拷锟斤拷锟叫?
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * areaSize * 2);
    }

    /// <summary>
    /// 锟斤拷示锟角凤拷锟街达拷锟?
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

    public static int IsUniqueAndAllLessThan(int[] array, int maxValue)
    {
        if (array == null) return -1;

        HashSet<int> seen = new HashSet<int>();
        foreach (int num in array)
        {
            // 检查元素是否 >= maxValue 或已存在重复
            if (num >= maxValue)
            {
                return 1;
            }
            if (!seen.Add(num))
            {
                return 2;
            }
        }
        return 0;
    }

    public void DebugSort(int[] array, int maxValue,int time) { 
        int t = IsUniqueAndAllLessThan(array, maxValue);
        if (t == -1)
        {
            Debug.Log("Array is null.");
        }
        else if (t == 0) {
            Debug.Log("Is Valid.");
            //Debug.Log("HashKey Valid: " + string.Join(", ", hash4bitsKey));
            Debug.Log("Temp Data: " + string.Join(", ", tempData));
            //Debug.Log("BlockPrefixSum Valid: " + string.Join(", ", temp1Data));
            //Debug.Log("LocalPrefixSum Valid: " + string.Join(", ", temp2Data));
        }
        else if (t == 1)
        {
            Debug.Log(time + " More Than MaxValue.");
            //Debug.Log("HashKey More Than MaxValue: " + string.Join(", ", hash4bitsKey));
            Debug.Log("Temp Data More Than MaxValue: " + string.Join(", ", tempData));
            //Debug.Log("BlockPrefixSum More Than MaxValue: " + string.Join(", ", temp1Data));
            //Debug.Log("LocalPrefixSum More Than MaxValue: " + string.Join(", ", temp2Data));
        }
        else
        {
            Debug.Log(time + " 有重复数据.");
            Debug.Log("Temp Data 有重复数据: " + string.Join(", ", tempData));
            //Debug.Log("HashKey 有重复数据: " + string.Join(", ", hashKey));
            //Debug.Log("BlockPrefixSum 有重复数据: " + string.Join(", ", temp1Data));
            //Debug.Log("LocalPrefixSum 有重复数据: " + string.Join(", ", temp2Data));
        }
    }

    /// <summary>
    /// 随机在mesh网格里生成粒子
    /// </summary>

    void DistributeInVolumeRandom(Particle[] particles, Mesh mesh, int particleCount)
    {
        // 1. 临时创建 MeshCollider
        GameObject tempObj = new GameObject("TempCollider");
        MeshCollider tempCollider = tempObj.AddComponent<MeshCollider>();
        tempCollider.sharedMesh = mesh;
        tempCollider.convex = false;

        // 2. 预计算有效位置
        Bounds bounds = mesh.bounds;
        List<Vector3> validPositions = new List<Vector3>();
        int preSamples = particleCount; // 预采样数量，可根据需要调整

        for (int i = 0; i < preSamples; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );
            if (IsPointInMesh(pos, tempCollider))
                validPositions.Add(pos);
        }

        // 3. 分配粒子
        for (int i = 0; i < Mathf.Min(particleCount, validPositions.Count); i++)
        {
            particles[i].position = validPositions[Random.Range(0, validPositions.Count)];
        }

        // 4. 清理临时对象
        GameObject.Destroy(tempObj);
    }

    void DistributeInVolume(Particle[] particles, Mesh mesh, int particleCount)
    {
        // 1. 临时创建 MeshCollider
        GameObject tempObj = new GameObject("TempCollider");
        MeshCollider tempCollider = tempObj.AddComponent<MeshCollider>();
        tempCollider.sharedMesh = mesh;
        tempCollider.convex = false; // 必须设为 false！

        // 2. 检查 Mesh 数据是否有效
        if (mesh.vertexCount == 0)
        {
            Debug.LogError("Mesh has no vertices!");
            GameObject.Destroy(tempObj);
            return;
        }

        // 3. 体素化参数
        Bounds bounds = mesh.bounds;
        Debug.Log($"Mesh bounds: {bounds}"); // 确认包围盒是否合理
        int gridResolution = 30;
        Vector3 voxelSize = bounds.size / gridResolution;

        // 4. 检测有效体素
        bool[,,] voxelGrid = new bool[gridResolution, gridResolution, gridResolution];
        for (int x = 0; x < gridResolution; x++)
        {
            for (int y = 0; y < gridResolution; y++)
            {
                for (int z = 0; z < gridResolution; z++)
                {
                    Vector3 voxelCenter = bounds.min + new Vector3(
                        (x + 0.5f) * voxelSize.x,
                        (y + 0.5f) * voxelSize.y,
                        (z + 0.5f) * voxelSize.z
                    );
                    voxelGrid[x, y, z] = IsPointInMesh(voxelCenter, tempCollider);
                }
            }
        }

        // 5. 收集有效位置
        List<Vector3> validPositions = new List<Vector3>();
        for (int x = 0; x < gridResolution; x++)
        {
            for (int y = 0; y < gridResolution; y++)
            {
                for (int z = 0; z < gridResolution; z++)
                {
                    if (voxelGrid[x, y, z])
                    {
                        Vector3 randomPos = bounds.min + new Vector3(
                            (x + Random.value) * voxelSize.x,
                            (y + Random.value) * voxelSize.y,
                            (z + Random.value) * voxelSize.z
                        );
                        validPositions.Add(randomPos);
                    }
                }
            }
        }

        Debug.Log($"Valid positions: {validPositions.Count}");

        // 6. 分配粒子（如果 validPositions 仍为空，则使用包围盒随机分布）
        if (validPositions.Count == 0)
        {
            Debug.LogWarning("No valid positions found! Falling back to bounding box.");
            for (int i = 0; i < particleCount; i++)
            {
                particles[i].position = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z)
                );
            }
        }
        else
        {
            for (int i = 0; i < Mathf.Min(particleCount, validPositions.Count); i++)
            {
                particles[i].position = validPositions[Random.Range(0, validPositions.Count)];
            }
        }

        // 7. 销毁临时对象
        GameObject.Destroy(tempObj);
    }

    bool IsPointInMesh(Vector3 point, MeshCollider collider)
    {
        // 1. 快速排除：检查是否在包围盒内
        if (!collider.bounds.Contains(point))
            return false;

        // 2. 从高处向下发射射线
        Vector3 rayStart = point + Vector3.up * 1000f;
        Ray ray = new Ray(rayStart, Vector3.down);

        // 3. 检测射线是否与模型相交
        if (collider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            // 4. 统计交点数量（奇数=内部，偶数=外部）
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
            int hitCount = Physics.RaycastAll(ray, Mathf.Infinity).Length;
            return hitCount % 2 == 1;
        }
        return false;
    }

}
