using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

static class ShaderPropertyId
{
    public static readonly int FluidDepth = Shader.PropertyToID("_FluidDepthTexture"); // Read from previous pass
    public static readonly int FluidIntermediateDepth = Shader.PropertyToID("_FluidIntermediateDepthTexture"); // Compute Shader Output Depth Texture
    public static readonly int FluidSmoothedDepth = Shader.PropertyToID("_FluidSmoothedDepthTexture"); // Smoothed Depth Texture
    public static readonly int FluidNormal = Shader.PropertyToID("_FluidNormalTexture"); // Write to
    public static readonly int FluidThickness = Shader.PropertyToID("_FluidThicknessTexture"); // Read from previous pass
    public static readonly int SceneColor = Shader.PropertyToID("_SceneColorTexture");
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
        public Mesh particleMesh;

        [Header("DepthPass")]
        public Material particleDepthMaterial;

        [Header("DepthSmoothingPass")] 
        public ComputeShader depthSmoothingComputeShader;
        
        [Header("NormalPass")]
        public ComputeShader normalComputeShader;
        
        [Header("ThicknessPass")]
        public Material particleThicknessMaterial;
        
        [Header("CompositePass")]
        public Material particleCompositeMaterial;
    }

    [SerializeField] public PBFRenderPassSettings settings = new PBFRenderPassSettings();

    // Pass instances
    private PBFRenderDpethPass m_PBFDepthPass;
    private PBFDepthSmoothingPass m_PBFDepthSmoothingPass;
    private PBFReconstructNormalPass m_PBFReconstructNormalPass;
    private PBFRenderThicknessPass m_PBFThicknessPass;
    private PBFRenderCompositePass m_PBFCompositePass;
    
    // Render Targets
    private RTHandle m_fluidDepthTexture;
    private RTHandle m_fluidIntermediateDepthTexture;
    private RTHandle m_fluidSmoothedDepthTexture;
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
        
        // create depth smoothing pass
        m_PBFDepthSmoothingPass = new PBFDepthSmoothingPass("PBF Depth Smoothing Pass");
        m_PBFDepthSmoothingPass.renderPassEvent = settings.renderPassEvent;
        m_PBFDepthSmoothingPass.SetComputeShader(settings.depthSmoothingComputeShader);

        // Normal Pass
        m_PBFReconstructNormalPass = new PBFReconstructNormalPass("PBF Normal Reconstruct Pass");
        m_PBFReconstructNormalPass.renderPassEvent = settings.renderPassEvent;
        m_PBFReconstructNormalPass.SetComputeShader(settings.normalComputeShader);
        
        // Thickness Pass
        m_PBFThicknessPass = new PBFRenderThicknessPass("PBF Thickness Pass");
        m_PBFThicknessPass.renderPassEvent = settings.renderPassEvent;
        m_PBFThicknessPass.particleMesh = settings.particleMesh;
        m_PBFThicknessPass.particleMaterial = settings.particleThicknessMaterial;
        
        // Composite Pass
        m_PBFCompositePass = new PBFRenderCompositePass("PBF Composite Pass");
        m_PBFCompositePass.renderPassEvent = settings.renderPassEvent;
        m_PBFCompositePass.particleMesh = settings.particleMesh;
        m_PBFCompositePass.particleMaterial = settings.particleCompositeMaterial;
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
            
            m_PBFCompositePass.particleBuffer = particleSystem.GetParticleBuffer();
            m_PBFCompositePass.particleCount = particleSystem.GetParticleCount();
        }

        
        renderer.EnqueuePass(m_PBFDepthPass);
        renderer.EnqueuePass(m_PBFDepthSmoothingPass);
        renderer.EnqueuePass(m_PBFReconstructNormalPass);
        renderer.EnqueuePass(m_PBFThicknessPass);
        renderer.EnqueuePass(m_PBFCompositePass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
     
        // depth pass
        descriptor.depthBufferBits = 0;
        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        descriptor.sRGB = false;
        descriptor.enableRandomWrite = true; // Needed for compute shader output (UAV)
        descriptor.msaaSamples = 1;
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidDepthTexture, descriptor, name: "_FluidDepthTexture");
        m_PBFDepthPass.Setup(m_fluidDepthTexture);
        
        // depth smoothing pass
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidIntermediateDepthTexture, descriptor, name: "_FluidIntermediateDepthTexture");
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidSmoothedDepthTexture, descriptor, name: "_FluidSmoothedDepthTexture");
        m_PBFDepthSmoothingPass.Setup(m_fluidDepthTexture, m_fluidIntermediateDepthTexture, m_fluidSmoothedDepthTexture);
        
        // normal pass
        descriptor.colorFormat = RenderTextureFormat.ARGB32; // Or RGBAHalf for higher precision normals
        descriptor.sRGB = false;
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidNormalTexture, descriptor, name: "_FluidNormalTexture");
        m_PBFReconstructNormalPass.Setup(m_fluidSmoothedDepthTexture, m_fluidNormalTexture);

        // thickness pass
        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidThicknessTexture, descriptor, name: "_FluidThicknessTexture");
        m_PBFThicknessPass.Setup(m_fluidThicknessTexture);
        
        // final composite pass
        m_PBFCompositePass.Setup(m_fluidDepthTexture, m_fluidThicknessTexture, renderingData.cameraData.renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            m_fluidDepthTexture?.Release();
            m_fluidSmoothedDepthTexture?.Release();
            m_fluidNormalTexture?.Release();
            m_fluidThicknessTexture?.Release();
        }
    }
}