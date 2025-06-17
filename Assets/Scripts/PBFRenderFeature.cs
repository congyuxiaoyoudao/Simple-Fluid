using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

static class ShaderPropertyId
{
    public static readonly int FluidDepth = Shader.PropertyToID("_FluidDepthTexture"); // Read from previous pass
    public static readonly int FluidIntermediateDepth = Shader.PropertyToID("_FluidIntermediateDepthTexture"); // Compute Shader Output Depth Texture
    public static readonly int FluidSmoothedDepth = Shader.PropertyToID("_FluidSmoothedDepthTexture"); // Smoothed Depth Texture
    public static readonly int KERNEL_RADIUS = Shader.PropertyToID("_KernelRadius"); 
    public static readonly int INV_2SIGMA_D_SQ = Shader.PropertyToID("_INV_2SIGMA_D_SQ"); 
    public static readonly int INV_2SIGMA_R_SQ = Shader.PropertyToID("_INV_2SIGMA_R_SQ"); 
    public static readonly int FluidNormal = Shader.PropertyToID("_FluidNormalTexture"); // Write to
    public static readonly int FluidThickness = Shader.PropertyToID("_FluidThicknessTexture"); // Read from previous pass
    public static readonly int SkyboxColor = Shader.PropertyToID("_SkyboxTexture");
    public static readonly int ScreenParams = Shader.PropertyToID("_ScreenParams"); // Screen size
    public static readonly int CameraToWorld = Shader.PropertyToID("_CameraToWorld"); // Camera matrix for view->world transform
    public static readonly int InverseProjection = Shader.PropertyToID("_InverseProjection"); // For unprojecting depth
}

public enum DebugPassType
{
    FluidParticle,
    FluidDepth,
    FluidSmoothedDepth,
    FluidNormal,
    FluidThickness,
    FluidComposite
}

[CreateAssetMenu(menuName = "Rendering/PBF/PBF Particle Render Feature")]
public class PBFRenderFeature : ScriptableRendererFeature
{
    // public feature settings
    [System.Serializable]
    public class PBFRenderPassSettings
    {
        public RenderPassEvent renderShadowMapPassEvent = RenderPassEvent.AfterRenderingShadows;
        public RenderPassEvent renderPreparePassEvent = RenderPassEvent.AfterRenderingOpaques;
        public RenderPassEvent renderCompositePassEvent = RenderPassEvent.AfterRenderingSkybox;
      
        // TODO: add parameters here
        [Header("Common")]
        public Mesh particleMesh;
        [Range(0.1f, 10.0f)]
        public float particleSize = 1.0f;
        [Range(0.01f, 1.0f)]
        public float particleMinDensity = 0.1f;
        public DebugPassType debugPassType;
        public bool isSceneView = true;

        [Header("DepthPass")]
        public Material particleDepthMaterial;

        [Header("DepthSmoothingPass")] 
        public ComputeShader depthSmoothingComputeShader;
        [Range(3, 16)]
        public int kernelRadius = 8;
        public float sigmaD = 1.0f; // Spatial sigma
        public float sigmaR = 1.0f; // Range sigma
        
        [Header("NormalPass")]
        public ComputeShader normalComputeShader;
        
        [Header("ThicknessPass")]
        public Material particleThicknessMaterial;
        [Range(0.01f, 1.0f)]
        public float thicknessScalar = 1.0f;
        
        [Header("CompositePass")]
        public Material particleCompositeMaterial;
        public Color particleColor = Color.white;
        [Range(1.0f, 2.0f)]
        public float indexOfRefraction = 1.33f; // For water, typically around 1.33
        [Range(0.0f, 1.0f)]
        public float refractScalar = 1.0f;
        [Range(0.0f, 1.0f)]
        public float turbidity = 0.0f;
        public Color absorptionColor = Color.red;
        public Color scatterColor = Color.cyan;
    }

    [SerializeField] public PBFRenderPassSettings settings = new PBFRenderPassSettings();

    // Pass instances
    private PBFShadowPass m_PBFShadowPass;
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
    private RTHandle m_fluidShadowMapTexture;
    
    // InstanceBuffer
    private GraphicsBuffer m_instanceBuffer;
    
    private void SetupIndirectArgsBuffer(int particleCount)
    {
        ReleaseIndirectArgsBuffer(ref m_instanceBuffer);
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = settings.particleMesh.GetIndexCount(0); 
        args[1] = (uint)particleCount;
        args[2] = settings.particleMesh.GetIndexStart(0);
        args[3] = settings.particleMesh.GetBaseVertex(0);
        args[4] = 0;
    
        m_instanceBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1, 
            args.Length * sizeof(uint)
        );
        m_instanceBuffer.SetData(args);
    }
    private void ReleaseIndirectArgsBuffer(ref GraphicsBuffer buffer)
    {
        if (buffer != null)
        {
            buffer.Release();
            buffer = null;
        }
    }

    // Create pass and set some settings
    public override void Create()
    {
        // Instance buffer
        var particleSystem = GameObject.FindObjectOfType<ParticleSystem>();
        if (particleSystem != null)
        {
            SetupIndirectArgsBuffer(particleSystem.GetParticleCount());
        }

        // create shadow pass
        m_PBFShadowPass = new PBFShadowPass("PBF Shadow Pass");
        m_PBFShadowPass.renderPassEvent = settings.renderShadowMapPassEvent;
        m_PBFShadowPass.particleMesh = settings.particleMesh;
        m_PBFShadowPass.particleMaterial = settings.particleDepthMaterial;
        m_PBFShadowPass.instanceBuffer = m_instanceBuffer;
        
        // create depth pass
        m_PBFDepthPass = new PBFRenderDpethPass("PBF Depth Render Pass");
        m_PBFDepthPass.renderPassEvent = settings.renderPreparePassEvent;
        m_PBFDepthPass.particleMesh = settings.particleMesh;
        m_PBFDepthPass.particleMaterial = settings.particleDepthMaterial;
        m_PBFDepthPass.instanceBuffer = m_instanceBuffer;
        
        // create depth smoothing pass
        m_PBFDepthSmoothingPass = new PBFDepthSmoothingPass("PBF Depth Smoothing Pass");
        m_PBFDepthSmoothingPass.renderPassEvent = settings.renderPreparePassEvent;
        m_PBFDepthSmoothingPass.SetComputeShader(settings.depthSmoothingComputeShader);

        // Normal Pass
        m_PBFReconstructNormalPass = new PBFReconstructNormalPass("PBF Normal Reconstruct Pass");
        m_PBFReconstructNormalPass.renderPassEvent = settings.renderPreparePassEvent;
        m_PBFReconstructNormalPass.SetComputeShader(settings.normalComputeShader);
        
        // Thickness Pass
        m_PBFThicknessPass = new PBFRenderThicknessPass("PBF Thickness Pass");
        m_PBFThicknessPass.renderPassEvent = settings.renderPreparePassEvent;
        m_PBFThicknessPass.particleMesh = settings.particleMesh;
        m_PBFThicknessPass.particleMaterial = settings.particleThicknessMaterial;
        m_PBFThicknessPass.instanceBuffer = m_instanceBuffer;
        
        // Composite Pass
        m_PBFCompositePass = new PBFRenderCompositePass("PBF Composite Pass");
        m_PBFCompositePass.renderPassEvent = settings.renderCompositePassEvent;
        m_PBFCompositePass.particleMesh = settings.particleMesh;
        m_PBFCompositePass.particleMaterial = settings.particleCompositeMaterial;
        m_PBFCompositePass.instanceBuffer = m_instanceBuffer;
    }

    // Add pass to render queue
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        bool desiredCameraView = settings.isSceneView 
            ? renderingData.cameraData.isSceneViewCamera 
            : !renderingData.cameraData.isSceneViewCamera;

        if (!desiredCameraView)
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
            ComputeBuffer particleBuffer = particleSystem.GetParticleBuffer();
            m_PBFDepthPass.particleBuffer = particleBuffer;
            m_PBFThicknessPass.particleBuffer = particleBuffer;
            m_PBFCompositePass.particleBuffer = particleBuffer;

            ComputeBuffer densityBuffer = particleSystem.GetDensityBuffer();
            m_PBFCompositePass.densityBuffer = densityBuffer;
        }
        
        renderer.EnqueuePass(m_PBFShadowPass);
        renderer.EnqueuePass(m_PBFDepthPass);
        renderer.EnqueuePass(m_PBFDepthSmoothingPass);
        renderer.EnqueuePass(m_PBFReconstructNormalPass);
        renderer.EnqueuePass(m_PBFThicknessPass);
        renderer.EnqueuePass(m_PBFCompositePass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
     
        descriptor.depthBufferBits = 24;
        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        descriptor.sRGB = false;
        descriptor.msaaSamples = 1;
        
        // shadow pass
        m_PBFShadowPass.Setup(settings.particleSize);
        
        // depth pass
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidDepthTexture, descriptor, name: "_FluidDepthTexture");
        m_PBFDepthPass.Setup(m_fluidDepthTexture, settings.particleSize);
        
        // depth smoothing pass
        descriptor.depthBufferBits = 0;
        descriptor.enableRandomWrite = true; // Needed for compute shader output (UAV)
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidIntermediateDepthTexture, descriptor, name: "_FluidIntermediateDepthTexture");
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidSmoothedDepthTexture, descriptor, name: "_FluidSmoothedDepthTexture");
        m_PBFDepthSmoothingPass.Setup(m_fluidDepthTexture,
                                      m_fluidIntermediateDepthTexture,
                                      m_fluidSmoothedDepthTexture,
                                      settings.kernelRadius,
                                      settings.sigmaD,
                                      settings.sigmaR);
        
        // normal pass
        descriptor.colorFormat = RenderTextureFormat.ARGB32; // Or RGBAHalf for higher precision normals
        descriptor.sRGB = false;
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidNormalTexture, descriptor, name: "_FluidNormalTexture");
        m_PBFReconstructNormalPass.Setup(m_fluidSmoothedDepthTexture, m_fluidNormalTexture);

        // thickness pass
        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        RenderingUtils.ReAllocateIfNeeded(ref m_fluidThicknessTexture, descriptor, name: "_FluidThicknessTexture");
        m_PBFThicknessPass.Setup(m_fluidThicknessTexture, settings.particleSize, settings.thicknessScalar);
        
        // final composite pass
        m_PBFCompositePass.Setup(settings.particleSize, 
                                 settings.particleMinDensity,
                                 settings.refractScalar,
                                 settings.indexOfRefraction,
                                 settings.turbidity,
                                 settings.particleColor,
                                 settings.absorptionColor,
                                 settings.scatterColor,
                                 settings.debugPassType);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            m_instanceBuffer?.Release();
            m_fluidDepthTexture?.Release();
            m_fluidSmoothedDepthTexture?.Release();
            m_fluidNormalTexture?.Release();
            m_fluidThicknessTexture?.Release();
        }
    }
}