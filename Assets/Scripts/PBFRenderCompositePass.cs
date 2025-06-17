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
    
    public ComputeBuffer particleBuffer;
    public ComputeBuffer densityBuffer;
    public GraphicsBuffer instanceBuffer;
    public int particleCount;

    // material parameters
    private float _particleSize = 1.0f;
    private float _paryicleMinDensity;
    private float _reflectScalar;
    private float _indexOfRefraction;
    private float _turbidity;
    private Color _particleColor;
    private Color _absorptionColor;
    private Color _scatterColor;
    private DebugPassType _debugPassType;
    
    // Ctor.
    public PBFRenderCompositePass(string profilerTag)
    {
        m_ProfilerTag = profilerTag;
        m_ProfilingSampler = new (m_ProfilerTag);
    }

    // let feature set rt, do not need to manage rt by pass 
    public void Setup(float particleSize,
                      float particleMinDensity,
                      float reflectScaler,
                      float indexOfRefraction, 
                      float turbidity,
                      Color particleColor,
                      Color absorptionColor,
                      Color scatterColor,
                      DebugPassType debugPassType)
    {
        _particleSize = particleSize;
        _paryicleMinDensity = particleMinDensity;
        _reflectScalar = reflectScaler;
        _indexOfRefraction = indexOfRefraction;
        _turbidity = turbidity;
        _particleColor = particleColor;
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
        particleMaterial.SetBuffer("_Particles", particleBuffer);
        particleMaterial.SetBuffer("_Density", densityBuffer);
        particleMaterial.SetFloat("_DensityThreshold",_paryicleMinDensity);
        particleMaterial.SetColor("_ParticleColor", _particleColor);
        particleMaterial.SetColor("_AbsorptionColor", _absorptionColor);
        particleMaterial.SetColor("_ScatterColor", _scatterColor);
        particleMaterial.SetFloat("_Size", _particleSize);
        particleMaterial.SetFloat("_ReflectScalar", _reflectScalar);
        particleMaterial.SetFloat("_IOR", _indexOfRefraction);
        particleMaterial.SetFloat("_Turbidity", _turbidity);
        particleMaterial.SetInt("_DebugType", (int)_debugPassType);
        // SetupIndirectArgs();
        
        // direct to screen
        // also write to device depth here, or there will be a race between particles! 
        ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle,renderingData.cameraData.renderer.cameraDepthTargetHandle);
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