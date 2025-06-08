using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystem : MonoBehaviour
{
    [Header("Settings")]
    public int particleCount = 1000;                        // Á£×ÓÊýÁ¿
    public float areaSize = 5f;                             //Óò´óÐ¡
    public Vector3 gravity = new Vector3(0, 9.8f, 0);       //ÖØÁ¦´óÐ¡/·½Ïò
    public float particleMass = 1.0f;                       //Á£×ÓÖÊÁ¿
    public float particleRadius = 1.0f;                     //Á£×Ó°ë¾¶
    public float particleSize = 0.1f;                       //Á£×Ó´óÐ¡
    public Color particleColor = Color.white;               //Á£×ÓÑÕÉ«

    public float targetDensity = 1;                         //Ä¿±êÃÜ¶È
    public float pressureMultiplier = 1;                    //Ñ¹Á¦ÏµÊý
    public float viscosityStrength = 1;                     //Õ³¶ÈÏµÊý

    [Header("References")]
    public ComputeShader computeShader;                     // ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½É«ï¿½ï¿½
    public Material debugMaterial;
    public Mesh particleMesh;                               // Ê¹ï¿½ï¿½Quadï¿½ï¿½Sphere

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
    private Material _material;                             // ï¿½ï¿½È¾ï¿½ï¿½ï¿½ÓµÄ²ï¿½ï¿½ï¿½

    private int[] hashKey;
    private int[] hash4bitsKey;
    private int[] tempData;
    private int[] temp1Data;
    private int[] temp2Data;
    private int[] cellStartData;


    // ²ÎÊýÌáÇ°ÉùÃ÷
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
    /// ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½Ý´ï¿½ï¿½ï¿½
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

    // »ñÈ¡ËùÓÐÐèÒªµÄKernel
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
        // ï¿½ï¿½Ê¼ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
        Particle[] particles = new Particle[particleCount];
        float spacing = areaSize / Mathf.Ceil(Mathf.Sqrt(particleCount));
        // ï¿½ï¿½ï¿½ï¿½Ã¿ï¿½ï¿½Ã¿ï¿½Ðµï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
        int particlesPerRow = Mathf.CeilToInt(Mathf.Sqrt(particleCount));
        for (int i = 0; i < particleCount; i++)
        {
            // ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½Ó±ï¿½ï¿½ï¿½Debugï¿½Ä¸ï¿½ï¿½ï¿½ï¿½Óµï¿½ï¿½ï¿½ï¿½â£©
            // TODO:Æ½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
            //particles[i].position = new Vector3(
            //    -areaSize + spacing * i,
            //    Random.Range(0,areaSize),
            //    0
            //) + transform.position;

            // y=0Æ½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
            //particles[i].position = new Vector3(
            //   Random.Range(-areaSize, areaSize),
            //   //Random.Range(0, areaSize),
            //   0,
            //   Random.Range(-areaSize, areaSize)
            //) + transform.position;

            // y=0 Æ½ï¿½ï¿½ï¿½Ï¹ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
            int x = i % particlesPerRow;
            int z = i / particlesPerRow;

            particles[i].position = new Vector3(
                -areaSize + x * spacing,
                0, // Yï¿½ï¿½Ì¶ï¿½Îª0
                -areaSize + z * spacing
            ) + transform.position;

            // ï¿½ï¿½Ê¼ï¿½Ù¶ï¿½Îª0
            particles[i].velocity = Vector3.zero;
        }

        // ´´½¨ComputeBuffer
        _particleBuffer = new ComputeBuffer(particleCount, particleStructStride);
        _particleBuffer.SetData(particles);//SetDataÊ±»áÔö¼ÓCPUÓëGPUÖ®¼äµÄ´ø¿í
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

    // ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½Ê¼ï¿½ï¿½ï¿½ï¿½Ó²ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
    void SetupIndirectArgs()
    {
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = particleMesh.GetIndexCount(0);    // ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
        args[1] = (uint)particleCount;             // Êµï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½Ê¼Îªï¿½ï¿½ï¿½Öµï¿½ï¿½
        args[2] = particleMesh.GetIndexStart(0);    // ï¿½ï¿½Ê¼ï¿½ï¿½ï¿½ï¿½
        args[3] = particleMesh.GetBaseVertex(0);    // ï¿½ï¿½×¼ï¿½ï¿½ï¿½ï¿½
        args[4] = 0;                                // ï¿½ï¿½Ê¼Êµï¿½ï¿½ID

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

        // ï¿½ï¿½ï¿½ò»¯»ï¿½ï¿½ï¿½Ð¡ï¿½ï¿½
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
        ///         Ö´ÐÐÇ°³õÊ¼Öµ£¨Start£©               ///
        ///         ParticlePosition                    ///
        ///         ParticleDensity = 1                 ///
        ///         ParticleIndex = i                   ///
        ///         Ö´ÐÐÇ°³õÊ¼Öµ£¨Update£©              ///
        ///         ParticlePosition£¨Calculate£©       ///
        ///         ParticleDensity £¨Calculate£©       ///
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


        //// ¸ù¾ÝParticleµÄPositionÖµ³õÊ¼»¯HashTableµÄÖµ
        //computeShader.SetBuffer(calculateHashTableKernelID, particlesID, _particleBuffer);//SetBuffer´ø¿íÏûºÄ¿ÉÒÔºöÂÔ²»¼Æ
        //computeShader.SetBuffer(calculateHashTableKernelID, hashTableID, _hashTableBuffer);
        //computeShader.Dispatch(calculateHashTableKernelID, threadGroups, 1, 1);



        //for (int i = 1; i <= 3; i++)
        //{
        //    // ³õÊ¼»¯BlockDataµÄÖµÎª 0
        //    computeShader.SetBuffer(initBlockDataKernelID, blockDataID, _blockDataBuffer);
        //    computeShader.Dispatch(initBlockDataKernelID, threadGroup2, 1, 1);

        //    // ¼ÆËãLocalPrefixSum
        //    computeShader.SetInt("_Digit", i - 1);
        //    computeShader.SetBuffer(calculateLocalPrefixSumKernelID, hashTableID, _hashTableBuffer);
        //    computeShader.SetBuffer(calculateLocalPrefixSumKernelID, blockDataID, _blockDataBuffer);
        //    computeShader.SetBuffer(calculateLocalPrefixSumKernelID, localPrefixSumDataID, _localPrefixSumBuffer);
        //    computeShader.SetBuffer(calculateLocalPrefixSumKernelID, temp2DataID, _temp2Buffer);
        //    computeShader.Dispatch(calculateLocalPrefixSumKernelID, threadGroups, 1, 1);

        //    // »ñÈ¡BlockDataÊý¾Ý ±ãÓÚDebug
        //    //temp2Data = new int[threadGroup2];
        //    //_blockDataBuffer.GetData(temp2Data);

        //    // ¼ÆËãGlobalPrefixSum
        //    computeShader.SetBuffer(calculateGlobalPrefixSumKernelID, blockDataID, _blockDataBuffer);
        //    computeShader.SetBuffer(calculateGlobalPrefixSumKernelID, blockPrefixSumDataID, _blockPrefixSumBuffer);
        //    computeShader.SetBuffer(calculateGlobalPrefixSumKernelID, blockPrefixSumOutputID, _blockPrefixSumOutputBuffer);
        //    computeShader.Dispatch(calculateGlobalPrefixSumKernelID, threadGroup2, 1, 1);

        //    computeShader.SetBuffer(calculateGlobalPrefixSum2KernelID, blockDataID, _blockDataBuffer);
        //    computeShader.SetBuffer(calculateGlobalPrefixSum2KernelID, blockPrefixSumDataID, _blockPrefixSumBuffer);
        //    computeShader.SetBuffer(calculateGlobalPrefixSum2KernelID, blockPrefixSumOutputID, _blockPrefixSumOutputBuffer);
        //    computeShader.Dispatch(calculateGlobalPrefixSum2KernelID, threadGroup2, 1, 1);

        //    // »ñÈ¡´ýÅÅÐòµÄHashÊý¾Ý ÓÃÓÚDebug
        //    //hashKey = new int[particleCount];
        //    //hash4bitsKey = new int[particleCount];
        //    //_hashTableBuffer.GetData(hashKey);
        //    //for (int j = 0; j < particleCount; j++)
        //    //{
        //    //    hash4bitsKey[j] = get4Bits(hashKey[j], i - 1);
        //    //}
        //    //Debug.Log("Sort hash" + i + ":   " + string.Join(", ", hashKey));
        //    //Debug.Log("Sort 4bits hash" + i + ":   " + string.Join(", ", hash4bitsKey));

        //    // ¼ÆËãGlobalPosition
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

        //    // µ÷ÊÔGlobalPositionÊý¾ÝµÄ×¼È·ÐÔ
        //    //tempData = new int[particleCount];
        //    //_tempBuffer.GetData(tempData);
        //    //DebugSort(tempData, 8192, i);

        //    // ¸ù¾ÝGlobalPositionÖØÓ³Éä
        //    computeShader.SetBuffer(changeParticlesIndexKernelID, "_GlobalPosition", _globalPositionBuffer);
        //    computeShader.SetBuffer(changeParticlesIndexKernelID, "_ParticleIndexTemp", _particleIndexTempBuffer);

        //    computeShader.SetBuffer(changeParticlesIndexKernelID, "_HashTableTemp", _hashTableTempBuffer);
        //    computeShader.SetBuffer(changeParticlesIndexKernelID, hashTableID, _hashTableBuffer);
        //    computeShader.SetBuffer(changeParticlesIndexKernelID, particleIndexID, _particleIndexBuffer);
        //    computeShader.Dispatch(changeParticlesIndexKernelID, threadGroups, 1, 1);

        //    // µ÷ÊÔÅÅÐòºóµÄHashÖµ
        //    //hashKey = new int[particleCount];
        //    //_hashTableBuffer.GetData(hashKey);
        //    //for (int j = 0; j < particleCount; j++)
        //    //{
        //    //    hash4bitsKey[j] = get4Bits(hashKey[j], i - 1);
        //    //}
        //    //Debug.Log("Sorted hash" + i + ":   " + string.Join(", ", hashKey));
        //    //Debug.Log("Sorted 4bits hash" + i + ":   " + string.Join(", ", hash4bitsKey));
        //}

        //// ³õÊ¼»¯Hash±íË÷Òý
        //computeShader.SetBuffer(initStartAndEndIndexKernelID, cellStartID, _cellStartBuffer);
        //computeShader.SetBuffer(initStartAndEndIndexKernelID, cellEndID, _cellEndBuffer);
        //computeShader.Dispatch(initStartAndEndIndexKernelID, threadGroups, 1, 1);

        //// ¼ÆËãHash±íË÷Òý
        //computeShader.SetBuffer(calculateStartAndEndIndexKernelID, hashTableID, _hashTableBuffer);
        //computeShader.SetBuffer(calculateStartAndEndIndexKernelID, cellStartID, _cellStartBuffer);
        //computeShader.SetBuffer(calculateStartAndEndIndexKernelID, cellEndID, _cellEndBuffer);
        //computeShader.Dispatch(calculateStartAndEndIndexKernelID, threadGroups, 1, 1);


        // DebugË÷ÒýÖµ
        //cellStartData = new int[particleCount];
        //_cellStartBuffer.GetData(cellStartData);
        //Debug.Log("CellStartBuffer" + ":   " + string.Join(", ", cellStartData));

        //// Debug×îºóµÄHashÖµ
        //hashKey = new int[particleCount];
        //_hashTableBuffer.GetData(hashKey);
        //Debug.Log("Sorted hash: " + string.Join(", ", hashKey));

        //// DebugÅÅÐòºóµÄÁ£×ÓÏÂ±ê
        //int[] debugParticleIndexData = new int[particleCount];
        //_particleIndexBuffer.GetData(debugParticleIndexData);
        //Debug.Log("ParticleIndex: " + string.Join(", ", debugParticleIndexData));

        // ¼ÆËãDensity
        computeShader.SetBuffer(calculateDensityKernelID, particlesID, _particleBuffer);//SetBuffer´ø¿íÏûºÄ¿ÉÒÔºöÂÔ²»¼Æ
        computeShader.SetBuffer(calculateDensityKernelID, particleIndexID, _particleIndexBuffer);
        computeShader.SetBuffer(calculateDensityKernelID, densityID, _densityBuffer);//SetBuffer´ø¿íÏûºÄ¿ÉÒÔºöÂÔ²»¼Æ
        computeShader.SetBuffer(calculateDensityKernelID, predictPositionID, _predictPosBuffer);//SetBuffer´ø¿íÏûºÄ¿ÉÒÔºöÂÔ²»¼Æ
        computeShader.SetBuffer(calculateDensityKernelID, hashTableID, _hashTableBuffer);
        computeShader.SetBuffer(calculateDensityKernelID, cellStartID, _cellStartBuffer);
        computeShader.SetBuffer(calculateDensityKernelID, cellEndID, _cellEndBuffer);
        computeShader.SetBuffer(calculateDensityKernelID, temp1DataID, _temp1Buffer);

        Debug.Log("ThreadGroupsNum: " + threadGroups);
        computeShader.SetFloat("_ThreadGroupCount", threadGroups);
        computeShader.Dispatch(calculateDensityKernelID, threadGroups, 1, 1);

        //// DebugË÷ÒýÖµ
        //temp1Data = new int[particleCount];
        //_temp1Buffer.GetData(temp1Data);
        //Debug.Log("PositionXInt CellStartIndex" + ":   " + string.Join(", ", temp1Data));

        


        // ¼ÆËãËÙ¶È
        computeShader.SetBuffer(calculateVelocityKernelID, particlesID, _particleBuffer);//SetBuffer´ø¿íÏûºÄ¿ÉÒÔºöÂÔ²»¼Æ
        computeShader.SetBuffer(calculateVelocityKernelID, densityID, _densityBuffer);//SetBuffer´ø¿íÏûºÄ¿ÉÒÔºöÂÔ²»¼Æ
        computeShader.SetBuffer(calculateVelocityKernelID, predictPositionID, _predictPosBuffer);
        computeShader.SetBuffer(calculateVelocityKernelID, particleIndexID, _particleIndexBuffer);
        computeShader.SetBuffer(calculateVelocityKernelID, cellStartID, _cellStartBuffer);
        computeShader.SetBuffer(calculateVelocityKernelID, cellEndID, _cellEndBuffer);
        computeShader.Dispatch(calculateVelocityKernelID, threadGroups, 1, 1);

        // ¼ÆËãÎ»ÖÃ
        computeShader.SetBuffer(calculatePositionKernelID, particlesID, _particleBuffer);//SetBuffer´ø¿íÏûºÄ¿ÉÒÔºöÂÔ²»¼Æ
        computeShader.Dispatch(calculatePositionKernelID, threadGroups, 1, 1);

        // ³õÊ¼»¯Á£×ÓË÷ÒýÖµ
        computeShader.SetBuffer(initParticleIndexKernelID, particleIndexID, _particleIndexBuffer);
        computeShader.Dispatch(initParticleIndexKernelID, threadGroups, 1, 1);


    }

    /// <summary>
    /// ï¿½ï¿½Ê¾ï¿½Ú´ï¿½ï¿½ï¿½ï¿½
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
    /// ï¿½ï¿½Ê¾ï¿½ï¿½ï¿½Çµï¿½ï¿½ï¿½ï¿½Ð¡
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * areaSize * 2);
    }

    /// <summary>
    /// ï¿½ï¿½Ê¾ï¿½Ç·ï¿½ï¿½Ö´ï¿½ï¿½
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
            // ¼ì²éÔªËØÊÇ·ñ >= maxValue »òÒÑ´æÔÚÖØ¸´
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
            Debug.Log(time + " ÓÐÖØ¸´Êý¾Ý.");
            Debug.Log("Temp Data ÓÐÖØ¸´Êý¾Ý: " + string.Join(", ", tempData));
            //Debug.Log("HashKey ÓÐÖØ¸´Êý¾Ý: " + string.Join(", ", hashKey));
            //Debug.Log("BlockPrefixSum ÓÐÖØ¸´Êý¾Ý: " + string.Join(", ", temp1Data));
            //Debug.Log("LocalPrefixSum ÓÐÖØ¸´Êý¾Ý: " + string.Join(", ", temp2Data));
        }
    }



}
