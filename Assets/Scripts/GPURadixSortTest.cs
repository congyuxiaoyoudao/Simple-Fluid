using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPURadixSortTest : MonoBehaviour
{
    [Header("Settings")]
    public ComputeShader computeShader;
    public int dataCount = 128; // Ö§³Ö128»ò256

    [Header("Debug")]
    public int[] initialData;
    public int[] initial4bitsData;
    public int[] sortedData;
    public int[] sorted4bitsData;
    public int[] blockData;
    public int[] blockPrefixSumData;
    public int[] debugData;
    public int[] cellStartData;
    public int[] cellEndData;

    private ComputeBuffer _inputBuffer;
    private ComputeBuffer _blockBuffer;
    private ComputeBuffer _blockBuffer2;
    private ComputeBuffer _blockPrefixSumBuffer;
    private ComputeBuffer _cellStartBuffer;
    private ComputeBuffer _cellEndBuffer;
    private ComputeBuffer _debugBuffer;

    private ComputeBuffer _sortBuffer;
    private int _sortKernel;

    int get4Bits(int value, int iteration)
    {
        int shift = iteration * 4;
        return (value >> shift) & 0xF;
    }

    void Start()
    {
        ValidateDataSize();
        InitializeBuffers();
        GenerateTestData();
        ExecuteGPUSort();
        ReadBackData();
    }

    void ValidateDataSize()
    {
        if (dataCount != 128 && dataCount != 256)
        {
            Debug.LogError("Only support 128 or 256 elements!");
            dataCount = 128;
        }
    }

    void InitializeBuffers()
    {
        _inputBuffer = new ComputeBuffer(dataCount, sizeof(int));
        _sortBuffer = new ComputeBuffer(dataCount, sizeof(int));
        _blockBuffer = new ComputeBuffer(Mathf.CeilToInt(dataCount / 64) * 16, sizeof(int));
        _blockBuffer2 = new ComputeBuffer(Mathf.CeilToInt(dataCount / 64) * 16, sizeof(int));
        _blockPrefixSumBuffer = new ComputeBuffer(Mathf.CeilToInt(dataCount / 64) * 16, sizeof(int));
        _debugBuffer = new ComputeBuffer(dataCount, sizeof(int));
        _sortKernel = computeShader.FindKernel("ExecuteRadixSort");
        _cellStartBuffer = new ComputeBuffer(dataCount, sizeof(int));
        _cellEndBuffer = new ComputeBuffer(dataCount, sizeof(int));
    }

    void GenerateTestData()
    {
        initialData = new int[dataCount];
        initial4bitsData = new int[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            initialData[i] = Random.Range(0, 256);
            initial4bitsData[i] = get4Bits(initialData[i], 0);
        }
        _inputBuffer.SetData(initialData);
        blockData = new int[Mathf.CeilToInt(dataCount / 64) * 16];
        for (int i = 0; i < Mathf.CeilToInt(dataCount / 64) * 16; i++)
        {
            blockData[i] = 0;
        }
        _blockBuffer.SetData(blockData);
        _blockBuffer2.SetData(blockData);
        cellStartData = new int[dataCount];
        cellEndData = new int[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            cellStartData[i] = int.MaxValue;
            cellEndData[i] = int.MaxValue;
        }
        _cellStartBuffer.SetData(cellStartData);
        _cellEndBuffer.SetData(cellEndData);
    }

    void ExecuteGPUSort()
    {
        int threadGroups = Mathf.CeilToInt(dataCount / 64);
        int threadGroup2 = Mathf.CeilToInt(threadGroups * 16);
        computeShader.SetInt("_ThreadGroupCount", Mathf.CeilToInt(dataCount / 64));
        computeShader.SetInt("_ParticleCount", dataCount);
        //computeShader.SetInt("_Digit", 1);
        //computeShader.SetBuffer(_sortKernel, "_HashTable", _inputBuffer);
        //computeShader.SetBuffer(_sortKernel, "_BlockData", _blockBuffer);
        //computeShader.SetBuffer(_sortKernel, "_DebugData", _debugBuffer);


        //computeShader.Dispatch(_sortKernel, threadGroups, 1, 1);


        //computeShader.SetBuffer(_sortKernel + 1, "_BlockData", _blockBuffer);
        //computeShader.SetBuffer(_sortKernel + 1, "_BlockPrefixSumData", _blockPrefixSumBuffer);
        //computeShader.Dispatch(_sortKernel + 1, threadGroup2, 1, 1);

        //computeShader.SetBuffer(_sortKernel + 2, "_HashTable", _inputBuffer);
        //computeShader.SetBuffer(_sortKernel + 2, "_DebugData", _debugBuffer);
        //computeShader.SetBuffer(_sortKernel + 2, "_SortData", _sortBuffer);
        //computeShader.SetBuffer(_sortKernel + 2, "_BlockPrefixSumData", _blockPrefixSumBuffer);
        //computeShader.Dispatch(_sortKernel + 2, threadGroups, 1, 1);
        for (int i = 1; i <= 3; i++)
        {
            computeShader.SetInt("_Digit", i-1);
            computeShader.SetBuffer(_sortKernel, "_HashTable", _inputBuffer);
            computeShader.SetBuffer(_sortKernel, "_BlockData", _blockBuffer);
            computeShader.SetBuffer(_sortKernel, "_DebugData", _debugBuffer);
            computeShader.Dispatch(_sortKernel, threadGroups, 1, 1);

            computeShader.SetBuffer(_sortKernel + 1, "_BlockData", _blockBuffer);
            computeShader.SetBuffer(_sortKernel + 1, "_BlockPrefixSumData", _blockPrefixSumBuffer);
            computeShader.Dispatch(_sortKernel + 1, threadGroup2, 1, 1);

            computeShader.SetBuffer(_sortKernel + 2, "_HashTable", _inputBuffer);
            computeShader.SetBuffer(_sortKernel + 2, "_DebugData", _debugBuffer);
            computeShader.SetBuffer(_sortKernel + 2, "_SortData", _sortBuffer);
            computeShader.SetBuffer(_sortKernel + 2, "_BlockPrefixSumData", _blockPrefixSumBuffer);
            computeShader.Dispatch(_sortKernel + 2, threadGroups, 1, 1);


            //computeShader.SetBuffer(_sortKernel + 3, "_SortData", _sortBuffer);
            //computeShader.SetBuffer(_sortKernel + 3, "_HashTable", _inputBuffer);
            //computeShader.Dispatch(_sortKernel + 3, threadGroups, 1, 1);
            computeShader.SetBuffer(_sortKernel+ 3, "_HashTable", _inputBuffer);
            computeShader.SetBuffer(_sortKernel + 3, "_CellStart", _cellStartBuffer);
            computeShader.SetBuffer(_sortKernel + 3, "_CellEnd", _cellEndBuffer);
            computeShader.Dispatch(_sortKernel + 3, threadGroups, 1, 1);
        }
        //computeShader.SetInt("_ThreadGroupCount", Mathf.CeilToInt(dataCount / 64));
        //computeShader.SetInt("_Digit", 2);
        //computeShader.SetBuffer(_sortKernel, "_HashTable", _inputBuffer);
        //computeShader.SetBuffer(_sortKernel, "_BlockData", _blockBuffer);
        //computeShader.SetBuffer(_sortKernel, "_DebugData", _debugBuffer);


        //computeShader.Dispatch(_sortKernel, threadGroups, 1, 1);


        //computeShader.SetBuffer(_sortKernel + 1, "_BlockData", _blockBuffer);
        //computeShader.SetBuffer(_sortKernel + 1, "_BlockPrefixSumData", _blockPrefixSumBuffer);
        //computeShader.Dispatch(_sortKernel + 1, threadGroup2, 1, 1);

        //computeShader.SetBuffer(_sortKernel + 2, "_HashTable", _inputBuffer);
        //computeShader.SetBuffer(_sortKernel + 2, "_DebugData", _debugBuffer);
        //computeShader.SetBuffer(_sortKernel + 2, "_SortData", _sortBuffer);
        //computeShader.SetBuffer(_sortKernel + 2, "_BlockPrefixSumData", _blockPrefixSumBuffer);
        //computeShader.Dispatch(_sortKernel + 2, threadGroups, 1, 1);
    }

    void ReadBackData()
    {
        //initialData = new int[dataCount];
        //_inputBuffer.GetData(initialData);
        sortedData = new int[dataCount];
        _sortBuffer.GetData(sortedData);
        sorted4bitsData = new int[dataCount];
        for (int i = 0; i < sorted4bitsData.Length; i++) {
            sorted4bitsData[i] = get4Bits(sortedData[i], 0);
        }
        debugData = new int[dataCount];
        _debugBuffer.GetData(debugData);
        blockData = new int[Mathf.CeilToInt(dataCount / 64) * 16];
        _blockBuffer.GetData(blockData);
        blockPrefixSumData = new int[Mathf.CeilToInt(dataCount / 64) * 16];
        _blockPrefixSumBuffer.GetData(blockPrefixSumData);
        cellStartData = new int[dataCount];
        _cellStartBuffer.GetData(cellStartData);
        cellEndData = new int[dataCount];
        _cellEndBuffer.GetData(cellEndData);

        Debug.Log("Initial Data: " + string.Join(", ", initialData));
        Debug.Log("Initial Data 4 bits: " + string.Join(", ", initial4bitsData));
        Debug.Log("Sorted Data: " + string.Join(", ", sortedData));
        Debug.Log("Sorted Data 4 bits: " + string.Join(", ", sorted4bitsData));
        Debug.Log("LocalPrefixSum Data: " + string.Join(", ", debugData));
        Debug.Log("Block Data: " + string.Join(", ", blockData));
        Debug.Log("BlockPrefixSum Data: " + string.Join(", ", blockPrefixSumData));
        Debug.Log("Cell Start Index: " + string.Join(", ", cellStartData));
        Debug.Log("Cell End Index: " + string.Join(", ", cellEndData));
    }

    void OnDestroy()
    {
        _inputBuffer?.Release();
        
        _blockBuffer?.Release();
        _blockBuffer2?.Release();
        _blockPrefixSumBuffer?.Release();
        _debugBuffer?.Release();
        _sortBuffer?.Release();
        _cellStartBuffer?.Release();
        _cellEndBuffer?.Release();
    }
}
