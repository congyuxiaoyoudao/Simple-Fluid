using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PBFReconstructNormalPass : ScriptableRenderPass
{
    private string m_ProfilerTag;
    private ProfilingSampler m_ProfilingSampler;

    // Compute Shader asset reference
    public ComputeShader normalComputeShader;
    private int m_KernelHandle; // Kernel index

    // Output render target
    private RTHandle _depthTexture;
    private RTHandle _normalTexture;

    // Ctor.
    public PBFReconstructNormalPass(string profilerTag)
    {
        m_ProfilerTag = profilerTag;
        m_ProfilingSampler = new(m_ProfilerTag);
    }

    public void Setup(RTHandle depthTexture, RTHandle normalTexture)
    {
        _depthTexture = depthTexture;
        _normalTexture = normalTexture;
    }
    
    // Called before the pass executes for a camera
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (normalComputeShader == null)
        {
            Debug.LogError($"[{m_ProfilerTag}] Compute Shader is not assigned.");
            return;
        }

        // Find the kernel index
        m_KernelHandle = normalComputeShader.FindKernel("CSMain"); // Assuming your kernel is named "CSMain"
        if (m_KernelHandle == -1)
        {
            Debug.LogError($"[{m_ProfilerTag}] Compute shader kernel 'CSMain' not found.");
            return;
        }

        // Get camera target descriptor
        // var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        // descriptor.depthBufferBits = 0; // No depth needed for the output normal map
        // descriptor.colorFormat = RenderTextureFormat.ARGB32; // Or RGBAHalf for higher precision normals
        // descriptor.enableRandomWrite = true; // Needed for compute shader output (UAV)
        // descriptor.msaaSamples = 1;
        // // Allocate or reallocate the normal texture
        // RenderingUtils.ReAllocateIfNeeded(
        //     ref _normalTexture,
        //     descriptor,
        //     name: "_FluidNormalTexture"
        // );
        ConfigureInput(ScriptableRenderPassInput.Depth);
        // Configure the output render target for the compute shader (optional but good practice)
        ConfigureTarget(_normalTexture); // Not needed for compute shader UAV output setup below
        ConfigureClear(ClearFlag.None, Color.black); // Clear if needed, but compute shader will write over
    }

    // Executes the pass
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (normalComputeShader == null || m_KernelHandle == -1)
        {
            return; // Skip if compute shader or kernel is not ready
        }

        // Get command buffer
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            // Set input and output textures for the compute shader
            cmd.SetComputeTextureParam(normalComputeShader, m_KernelHandle, ShaderPropertyId.FluidDepth, _depthTexture); // Read global depth texture
            cmd.SetComputeTextureParam(normalComputeShader, m_KernelHandle, ShaderPropertyId.FluidNormal, _normalTexture); // Write to our normal texture

            // Set camera parameters needed for depth reconstruction
            var cameraData = renderingData.cameraData;
            var camera = cameraData.camera;

            cmd.SetComputeVectorParam(normalComputeShader, ShaderPropertyId.ScreenParams, new Vector4(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height, 1.0f / cameraData.cameraTargetDescriptor.width, 1.0f / cameraData.cameraTargetDescriptor.height));
            cmd.SetComputeMatrixParam(normalComputeShader, ShaderPropertyId.CameraToWorld, camera.cameraToWorldMatrix);
            cmd.SetComputeMatrixParam(normalComputeShader, ShaderPropertyId.InverseProjection, camera.projectionMatrix.inverse);
            // You might also need camera position: cmd.SetComputeVector(normalComputeShader, ShaderPropertyId.WorldSpaceCameraPos, camera.transform.position);

            // Get thread group sizes from the kernel
            uint xGroupSize, yGroupSize, zGroupSize;
            normalComputeShader.GetKernelThreadGroupSizes(m_KernelHandle, out xGroupSize, out yGroupSize, out zGroupSize);

            // Calculate dispatch dimensions
            int width = cameraData.cameraTargetDescriptor.width;
            int height = cameraData.cameraTargetDescriptor.height;

            int dispatchX = Mathf.CeilToInt((float)width / xGroupSize);
            int dispatchY = Mathf.CeilToInt((float)height / yGroupSize);
            int dispatchZ = 1; // Assuming a 2D texture processing

            // Dispatch the compute shader
            cmd.DispatchCompute(normalComputeShader, m_KernelHandle, dispatchX, dispatchY, dispatchZ);

            // Set the generated normal texture globally so other shaders can access it
            cmd.SetGlobalTexture(ShaderPropertyId.FluidNormal, _normalTexture);

            // Optionally, blit the normal texture to the screen for debugging
            // Blit(cmd, _normalTexture, cameraData.renderer.cameraColorTargetHandle);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        // _normalTexture?.Release();
        // _normalTexture = null;
    }

    public void SetComputeShader(ComputeShader cs)
    {
        normalComputeShader = cs;
    }
}