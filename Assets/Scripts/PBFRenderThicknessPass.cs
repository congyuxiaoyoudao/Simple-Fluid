using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PBFRenderThicknessPass : ScriptableRenderPass
{
    private string m_ProfilerTag;
    private ProfilingSampler m_ProfilingSampler;
    public Mesh particleMesh;
    public Material particleMaterial; 

    // Render target configures
    private RTHandle _thicknessTexture;
    
    public ComputeBuffer particleBuffer;
    public GraphicsBuffer instanceBuffer;
    public int particleCount;

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
    public PBFRenderThicknessPass(string profilerTag)
    {
        m_ProfilerTag = profilerTag;
        m_ProfilingSampler = new (m_ProfilerTag);
    }

    // let feature set rt, do not need to manage rt by pass 
    public void Setup(RTHandle thicknessTexture)
    {
        _thicknessTexture = thicknessTexture;
    }
    
    // initialize resources
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {

        // Set material buffers
        particleMaterial.SetBuffer("_ParticlePositions", particleBuffer);
        particleMaterial.SetColor("_Color", Color.cyan);
        particleMaterial.SetFloat("_Size", 0.2f);
        SetupIndirectArgs();
        
        ConfigureTarget(_thicknessTexture);
        ConfigureClear(ClearFlag.All, Color.clear); 
    }


    // Dispatch pass
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        
        // Get command buffer
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            cmd.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                particleMaterial,
                0,
                instanceBuffer
            );

            cmd.SetGlobalTexture(ShaderPropertyId.FluidThickness, _thicknessTexture);
            // Blit(cmd,_depthTexture, renderingData.cameraData.renderer.cameraColorTargetHandle);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }
    
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
    }
}