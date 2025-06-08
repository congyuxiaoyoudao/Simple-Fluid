using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PBFRenderCompositePass : ScriptableRenderPass
{
    private string m_ProfilerTag;
    private ProfilingSampler m_ProfilingSampler;
    public Mesh particleMesh;
    public Material particleMaterial; 

    // Render target configures
    private RTHandle _normalTexture;
    private RTHandle _thicknessTexture;
    private RTHandle _sceneTexture;
    
    public ComputeBuffer particleBuffer;
    public GraphicsBuffer instanceBuffer;
    public int particleCount;

    // material parameters
    private float _particleSize = 1.0f;
    private float _indexOfRefraction;
    private Color _absorptionColor;
    private Color _scatterColor;
    private DebugPassType _debugPassType;
    
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
    public PBFRenderCompositePass(string profilerTag)
    {
        m_ProfilerTag = profilerTag;
        m_ProfilingSampler = new (m_ProfilerTag);
    }

    // let feature set rt, do not need to manage rt by pass 
    public void Setup(RTHandle normalTexture, 
                      RTHandle thicknessTexture,
                      RTHandle sceneTexture, 
                      float particleSize, 
                      float indexOfRefraction, 
                      Color absorptionColor,
                      Color scatterColor,
                      DebugPassType debugPassType)
    {
        _normalTexture = normalTexture;
        _thicknessTexture = thicknessTexture;
        _sceneTexture = sceneTexture;
        _particleSize = particleSize;
        _indexOfRefraction = indexOfRefraction;
        _absorptionColor = absorptionColor;
        _scatterColor = scatterColor;
        _debugPassType = debugPassType;
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
        particleMaterial.SetColor("_AbsorptionColor", _absorptionColor);
        particleMaterial.SetColor("_ScatterColor", _scatterColor);
        particleMaterial.SetFloat("_Size", _particleSize);
        particleMaterial.SetFloat("_IOR", _indexOfRefraction);
        particleMaterial.SetInt("_DebugType", (int)_debugPassType);
        // SetupIndirectArgs();
        
        // direct to screen
        ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
        // DO NOT clear the screen, or previous render pass(skybox) will be cleared
        ConfigureClear(ClearFlag.None, Color.clear); 
    }


    // Dispatch pass
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        
        // Get command buffer
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            Cubemap skyboxCubemap = null;
            var skyboxMat = RenderSettings.skybox;
            if (skyboxMat && skyboxMat.HasProperty("_Tex"))
            {
                skyboxCubemap = skyboxMat.GetTexture("_Tex") as Cubemap;
            }

            if (skyboxCubemap)
            {
                cmd.SetGlobalTexture(ShaderPropertyId.SkyboxColor, skyboxCubemap);
            }

            cmd.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                particleMaterial,
                0,
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