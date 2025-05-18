using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

static class ShaderPropertyId
{
    public static readonly int FluidDepth = Shader.PropertyToID("_FluidDepthTexture"); // Read from previous pass
    public static readonly int FluidNormal = Shader.PropertyToID("_FluidNormalTexture"); // Write to
    public static readonly int FluidThickness = Shader.PropertyToID("_FluidThicknessTexture"); // Read from previous pass
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
        public Material particleDepthMaterial;
        public Mesh particleMesh;
        
        [Header("NormalPass")]
        public ComputeShader normalComputeShader;
        
        [Header("ThicknessPass")]
        public Material particleThicknessMaterial;
        
    }

    [SerializeField] public PBFRenderPassSettings settings = new PBFRenderPassSettings();

    // Pass instances
    private PBFRenderDpethPass m_PBFDepthPass;
    private PBFReconstructNormalPass m_PBFReconstructNormalPass;
    private PBFRenderThicknessPass m_PBFThicknessPass;
    
    // Render Targets
    private RTHandle m_fluidDepthTexture;
    private RTHandle m_fluidNormalTexture;
    private RTHandle m_fluidThicknessTexture;

    // Create pass and set some settings
    public override void Create()
    {
        // create depth pass
        m_PBFDepthPass = new PBFRenderDpethPass("PBF Depth Render Pass");
        m_PBFDepthPass.renderPassEvent = settings.renderPassEvent;
        m_PBFDepthPass.particleMesh = settings.particleMesh;
        m_PBFDepthPass.particleMaterial = settings.particleDepthMaterial;

        // Normal Pass
        m_PBFReconstructNormalPass = new PBFReconstructNormalPass("PBF Normal Reconstruct Pass");
        m_PBFReconstructNormalPass.renderPassEvent = settings.renderPassEvent;
        m_PBFReconstructNormalPass.SetComputeShader(settings.normalComputeShader);
        
        // Thickness Pass
        m_PBFThicknessPass = new PBFRenderThicknessPass("PBF Thickness Pass");
        m_PBFThicknessPass.renderPassEvent = settings.renderPassEvent;
        m_PBFThicknessPass.particleMesh = settings.particleMesh;
        m_PBFThicknessPass.particleMaterial = settings.particleThicknessMaterial;
    }

    // Add pass to render queue
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera)
            return;
        // ensure that material has been set
        if (settings.particleDepthMaterial == null)
        {
            Debug.LogWarning("PBF Particle Render Feature: Missing Particle Material.", this);
            return;
        }
        
        var particleSystem = GameObject.FindObjectOfType<ParticleSystem>();
        if (particleSystem != null)
        {
            m_PBFDepthPass.particleBuffer = particleSystem.GetParticleBuffer();
            m_PBFDepthPass.particleCount = particleSystem.GetParticleCount();

            m_PBFThicknessPass.particleBuffer = particleSystem.GetParticleBuffer();
            m_PBFThicknessPass.particleCount = particleSystem.GetParticleCount();
        }

        
        renderer.EnqueuePass(m_PBFDepthPass);
        renderer.EnqueuePass(m_PBFReconstructNormalPass);
        renderer.EnqueuePass(m_PBFThicknessPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0;
        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        descriptor.sRGB = false;

        RenderingUtils.ReAllocateIfNeeded(ref m_fluidDepthTexture, descriptor, name: "_FluidDepthTexture");
        m_PBFDepthPass.Setup(m_fluidDepthTexture);
        
        descriptor.colorFormat = RenderTextureFormat.ARGB32; // Or RGBAHalf for higher precision normals
        descriptor.sRGB = false;
        descriptor.enableRandomWrite = true; // Needed for compute shader output (UAV)
        descriptor.msaaSamples = 1;
        // Allocate or reallocate the normal texture
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidNormalTexture, descriptor, name: "_FluidNormalTexture");
        m_PBFReconstructNormalPass.Setup(m_fluidDepthTexture, m_fluidNormalTexture);

        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidThicknessTexture, descriptor, name: "_FluidThicknessTexture");
        m_PBFThicknessPass.Setup(m_fluidThicknessTexture);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            m_fluidDepthTexture?.Release();
            m_fluidNormalTexture?.Release();
            m_fluidThicknessTexture?.Release();
        }
    }
}