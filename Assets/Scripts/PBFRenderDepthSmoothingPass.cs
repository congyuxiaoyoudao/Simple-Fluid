using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PBFDepthSmoothingPass : ScriptableRenderPass
{
    private string m_ProfilerTag;
    private ProfilingSampler m_ProfilingSampler;

    // Compute Shader asset reference
    public ComputeShader depthSmoothingComputeShader;
    private int m_KernelHandle; // Kernel index

    // Output render target
    private RTHandle _depthTexture;
    private RTHandle _intermediateDepthTexture;
    private RTHandle _smoothedDepthTexture;

    // material parameters
    private float _sigmaD = 1.0f;
    private float _sigmaR = 1.0f;
    
    // Ctor.
    public PBFDepthSmoothingPass(string profilerTag)
    {
        m_ProfilerTag = profilerTag;
        m_ProfilingSampler = new(m_ProfilerTag);
    }

    public void Setup(RTHandle depthTexture, RTHandle intermediateDepthTexture, RTHandle smoothedDepthTexture, float sigmaD, float sigmaR)
    {
        _depthTexture = depthTexture;
        _intermediateDepthTexture = intermediateDepthTexture;
        _smoothedDepthTexture = smoothedDepthTexture;
        _sigmaD = sigmaD;
        _sigmaR = sigmaR;
    }
    
    // Called before the pass executes for a camera
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (depthSmoothingComputeShader == null)
        {
            Debug.LogError($"[{m_ProfilerTag}] Compute Shader is not assigned.");
            return;
        }

        // set this so we can see at camera color attachment
        ConfigureTarget(_smoothedDepthTexture);
        ConfigureClear(ClearFlag.None, Color.black); // Clear if needed, but compute shader will write over
    }

    private void BilateralFilterHorizontal(CommandBuffer cmd, ref RenderingData renderingData)
    {
        m_KernelHandle = depthSmoothingComputeShader.FindKernel("BilateralFilterHorizontalMain");
        if (m_KernelHandle == -1)
        {
            Debug.LogError($"[{m_ProfilerTag}] Compute shader kernel 'BilateralFilterHorizontalMain' not found.");
            return;
        }
        
        // configure input parameters
        cmd.SetComputeTextureParam(depthSmoothingComputeShader, m_KernelHandle, ShaderPropertyId.FluidDepth, _depthTexture); 
        cmd.SetComputeTextureParam(depthSmoothingComputeShader, m_KernelHandle, ShaderPropertyId.FluidIntermediateDepth, _intermediateDepthTexture); 
        
        var cameraData = renderingData.cameraData;
        int width = cameraData.cameraTargetDescriptor.width;
        int height = cameraData.cameraTargetDescriptor.height;
        cmd.SetComputeVectorParam(depthSmoothingComputeShader, ShaderPropertyId.ScreenParams, new Vector2(width, height));
        
        cmd.SetComputeFloatParam(depthSmoothingComputeShader, ShaderPropertyId.INV_2SIGMA_D_SQ, 1.0f / (2.0f * _sigmaD * _sigmaR));
        cmd.SetComputeFloatParam(depthSmoothingComputeShader, ShaderPropertyId.INV_2SIGMA_R_SQ, 1.0f / (2.0f * _sigmaR * _sigmaR));
        
        // Get thread group sizes from the kernel
        uint xGroupSize, yGroupSize, zGroupSize;
        depthSmoothingComputeShader.GetKernelThreadGroupSizes(m_KernelHandle, out xGroupSize, out yGroupSize, out zGroupSize);

        int dispatchX = Mathf.CeilToInt((float)width / xGroupSize);
        int dispatchY = Mathf.CeilToInt((float)height / yGroupSize);
        int dispatchZ = 1; // Assuming a 2D texture processing

        // Dispatch the compute shader
        cmd.DispatchCompute(depthSmoothingComputeShader, m_KernelHandle, dispatchX, dispatchY, dispatchZ);
        
        cmd.SetGlobalTexture(ShaderPropertyId.FluidIntermediateDepth, _intermediateDepthTexture);
    }
    
    private void BilateralFilterVertical(CommandBuffer cmd, ref RenderingData renderingData)
    {
        m_KernelHandle = depthSmoothingComputeShader.FindKernel("BilateralFilterVerticalMain");
        if (m_KernelHandle == -1)
        {
            Debug.LogError($"[{m_ProfilerTag}] Compute shader kernel 'BilateralFilterVerticalMain' not found.");
            return;
        }
        
        // configure input parameters
        cmd.SetComputeTextureParam(depthSmoothingComputeShader, m_KernelHandle, ShaderPropertyId.FluidIntermediateDepth, _intermediateDepthTexture); 
        cmd.SetComputeTextureParam(depthSmoothingComputeShader, m_KernelHandle, ShaderPropertyId.FluidSmoothedDepth, _smoothedDepthTexture); 
        
        var cameraData = renderingData.cameraData;
        int width = cameraData.cameraTargetDescriptor.width;
        int height = cameraData.cameraTargetDescriptor.height;
        cmd.SetComputeVectorParam(depthSmoothingComputeShader, ShaderPropertyId.ScreenParams, new Vector2(width, height));
        
        // Get thread group sizes from the kernel
        uint xGroupSize, yGroupSize, zGroupSize;
        depthSmoothingComputeShader.GetKernelThreadGroupSizes(m_KernelHandle, out xGroupSize, out yGroupSize, out zGroupSize);

        int dispatchX = Mathf.CeilToInt((float)width / xGroupSize);
        int dispatchY = Mathf.CeilToInt((float)height / yGroupSize);
        int dispatchZ = 1; // Assuming a 2D texture processing

        // Dispatch the compute shader
        cmd.DispatchCompute(depthSmoothingComputeShader, m_KernelHandle, dispatchX, dispatchY, dispatchZ);
        
        cmd.SetGlobalTexture(ShaderPropertyId.FluidSmoothedDepth, _smoothedDepthTexture);
    }
    
    // Executes the pass
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (depthSmoothingComputeShader == null || m_KernelHandle == -1)
        {
            return; // Skip if compute shader or kernel is not ready
        }

        // Get command buffer
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            BilateralFilterHorizontal(cmd, ref renderingData);
            BilateralFilterVertical(cmd, ref renderingData);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {

    }

    public void SetComputeShader(ComputeShader cs)
    {
        depthSmoothingComputeShader = cs;
    }
}