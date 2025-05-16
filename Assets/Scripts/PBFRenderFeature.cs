using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

static class ShaderPropertyId
{
    public static readonly int FluidDepth = Shader.PropertyToID("_FluidDepthTexture"); // Read from previous pass
    public static readonly int FluidNormal = Shader.PropertyToID("_FluidNormalTexture"); // Write to
    public static readonly int ScreenParams = Shader.PropertyToID("_ScreenParams"); // Screen size
    public static readonly int CameraToWorld = Shader.PropertyToID("_CameraToWorld"); // Camera matrix for view->world transform
    public static readonly int InverseProjection = Shader.PropertyToID("_InverseProjection"); // For unprojecting depth
}

[CreateAssetMenu(menuName = "Rendering/PBF/PBF Particle Render Feature")]
public class PBFRenderFeature : ScriptableRendererFeature
{
    // public feature settings
    [System.Serializable]
    public class PBFRenderPassSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        
        [Header("Common")]
        public Material particleMaterial;
        public Mesh particleMesh;
        
        [Header("NormalPass")]
        public ComputeShader normalComputeShader;
    }

    [SerializeField] public PBFRenderPassSettings settings = new PBFRenderPassSettings();

    // Pass instances
    private PBFRenderDpethPass m_PBFRenderPass;
    private PBFReconstructNormalPass m_PBFReconstructNormalPass;

    // Create pass and set some settings
    public override void Create()
    {
        // create depth pass
        m_PBFRenderPass = new PBFRenderDpethPass("PBF Depth Render Pass");
        m_PBFRenderPass.renderPassEvent = settings.renderPassEvent;
        m_PBFRenderPass.particleMesh = settings.particleMesh;
        m_PBFRenderPass.particleMaterial = settings.particleMaterial;

        // Normal Pass
        m_PBFReconstructNormalPass = new PBFReconstructNormalPass("PBF Normal Reconstruct Pass");
        m_PBFReconstructNormalPass.renderPassEvent = settings.renderPassEvent;
        m_PBFReconstructNormalPass.SetComputeShader(settings.normalComputeShader);
    }

    // Add pass to render queue
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera)
            return;
        // ensure that material has been set
        if (settings.particleMaterial == null)
        {
            Debug.LogWarning("PBF Particle Render Feature: Missing Particle Material.", this);
            return;
        }
        
        var particleSystem = GameObject.FindObjectOfType<ParticleSystem>();
        if (particleSystem != null)
        {
            m_PBFRenderPass.particleBuffer = particleSystem.GetParticleBuffer();
            m_PBFRenderPass.particleCount = particleSystem.GetParticleCount();
        }

        
        renderer.EnqueuePass(m_PBFRenderPass);
        // renderer.EnqueuePass(m_PBFReconstructNormalPass);
    }
    

    protected override void Dispose(bool disposing)
    {
        
    }
}