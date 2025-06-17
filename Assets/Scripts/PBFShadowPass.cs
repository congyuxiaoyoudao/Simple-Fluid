using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PBFShadowPass : ScriptableRenderPass
{
    private string m_ProfilerTag;
    private ProfilingSampler m_ProfilingSampler;
    public Mesh particleMesh;
    public Material particleMaterial; 
    
    public ComputeBuffer particleBuffer;
    public GraphicsBuffer instanceBuffer;
    public int particleCount;

    // material parameters
    private float _particleSize = 1.0f;
    
    public void SetupIndirectArgs()
    {
        if (instanceBuffer != null)
        {
            instanceBuffer.Release();
            instanceBuffer = null;
        }
        if (particleMesh == null || particleCount <= 0)
        {
            Debug.LogWarning($"[{m_ProfilerTag}] Cannot setup indirect args: particleMesh is null or particleCount is <= 0 ({particleCount}).");
            return;
        }
    
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = particleMesh.GetIndexCount(0);    // 网格的索引数量 (Submesh 0)
        args[1] = (uint)particleCount;             // *** 实例数量 (粒子数量) ***
        args[2] = particleMesh.GetIndexStart(0);    // 网格的起始索引
        args[3] = particleMesh.GetBaseVertex(0);    // 网格的基础顶点索引
        args[4] = 0;                                // 起始实例 ID
    
        instanceBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1, 
            args.Length * sizeof(uint)
        );
        instanceBuffer.SetData(args);
    }
    
    // Ctor.
    public PBFShadowPass(string profilerTag)
    {
        m_ProfilerTag = profilerTag;
        m_ProfilingSampler = new (m_ProfilerTag);
    }

    // let feature set rt, do not need to manage rt by pass 
    public void Setup(float particleSize)
    {
        _particleSize = particleSize;
    }
    
    // initialize resources
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        // descriptor.depthBufferBits = 0;
        // descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_UNorm;
        // descriptor.sRGB = false;
        //
        // RenderingUtils.ReAllocateIfNeeded(ref _depthTexture, descriptor, name: "_FluidDepthTexture");

        // Set material buffers
        particleMaterial.SetBuffer("_ParticlePositions", particleBuffer);
        particleMaterial.SetFloat("_Size", _particleSize);
        // SetupIndirectArgs();
        
        // ConfigureTarget(_shadowMapTexture);
        // ConfigureClear(ClearFlag.All, Color.clear); 
    }


    // Dispatch pass
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // Only draw shadows when main light cast shadows
        if (!renderingData.shadowData.supportsMainLightShadows)
        {
            return;
        }
        
        // Get command buffer
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            cmd.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                particleMaterial,
                1,
                instanceBuffer
            );
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }
    
    public override void OnCameraCleanup(CommandBuffer cmd)
    {

    }
}