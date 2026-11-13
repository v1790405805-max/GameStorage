using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 将反击阶段中的玩家与攻击者渲染为 Alpha 遮罩，并只压暗遮罩外的画面。
/// </summary>
public class CounterTimeMaskFeature : ScriptableRendererFeature
{
    private const string DefaultCompositeShader = "Hidden/Chaos/CounterTimeMaskComposite";

    [SerializeField] private Shader compositeShader;
    [SerializeField] private LayerMask maskedLayers = 0;
    [SerializeField, Range(0f, 1f)] private float darkenAmount = 0.68f;

    private Material compositeMaterial;
    private CounterTimeMaskPass maskPass;
    private CounterTimeCompositePass compositePass;

    public override void Create()
    {
        Shader resolvedCompositeShader =
            compositeShader != null ? compositeShader : Shader.Find(DefaultCompositeShader);

        compositeMaterial = resolvedCompositeShader != null
            ? CoreUtils.CreateEngineMaterial(resolvedCompositeShader)
            : null;

        maskPass = new CounterTimeMaskPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
        compositePass = new CounterTimeCompositePass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (!CounterTimeManager.IsCounterTimeMaskActive ||
            compositeMaterial == null)
        {
            return;
        }

        Camera camera = renderingData.cameraData.camera;
        if (camera == null ||
            camera != MainCameraAppoint.Current ||
            camera.cameraType == CameraType.Preview ||
            camera.cameraType == CameraType.Reflection)
        {
            return;
        }

        int resolvedMask = maskedLayers.value;
        if (resolvedMask == 0)
        {
            resolvedMask = LayerMask.GetMask(
                CounterTimeManager.PlayerMaskLayerName,
                CounterTimeManager.AttackerMaskLayerName);
        }

        if (resolvedMask == 0)
        {
            return;
        }

        maskPass.Setup(resolvedMask);
        compositePass.Setup(compositeMaterial, darkenAmount);
        renderer.EnqueuePass(maskPass);
        renderer.EnqueuePass(compositePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(compositeMaterial);
        base.Dispose(disposing);
    }

    private sealed class CounterTimeMaskData : ContextItem
    {
        public TextureHandle MaskTexture;

        public override void Reset()
        {
            MaskTexture = TextureHandle.nullHandle;
        }
    }

    private sealed class CounterTimeMaskPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            public RendererListHandle RendererList;
        }

        private int layerMask;

        public void Setup(int mask)
        {
            layerMask = mask;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            UniversalRenderingData renderingData =
                frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            CounterTimeMaskData maskData =
                frameData.GetOrCreate<CounterTimeMaskData>();

            TextureDesc maskDescriptor =
                resources.activeColorTexture.GetDescriptor(renderGraph);
            maskDescriptor.name = "_CounterTimeMask";
            maskDescriptor.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
            maskDescriptor.depthBufferBits = DepthBits.None;
            maskDescriptor.msaaSamples = MSAASamples.None;
            maskDescriptor.clearBuffer = true;
            maskDescriptor.filterMode = FilterMode.Bilinear;
            maskDescriptor.wrapMode = TextureWrapMode.Clamp;
            maskData.MaskTexture = renderGraph.CreateTexture(maskDescriptor);

            List<ShaderTagId> forwardShaderTags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("LightweightForward")
            };
            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                forwardShaderTags,
                renderingData,
                cameraData,
                lightData,
                cameraData.defaultOpaqueSortFlags);
            FilteringSettings filteringSettings =
                new FilteringSettings(RenderQueueRange.all, layerMask);
            RendererListParams rendererListParams = new RendererListParams(
                renderingData.cullResults,
                drawingSettings,
                filteringSettings);

            using (var builder =
                renderGraph.AddRasterRenderPass<PassData>(
                    "Counter Time Alpha Mask",
                    out PassData passData))
            {
                passData.RendererList =
                    renderGraph.CreateRendererList(rendererListParams);
                builder.UseRendererList(passData.RendererList);
                builder.SetRenderAttachment(
                    maskData.MaskTexture,
                    0,
                    AccessFlags.Write);

                if (resources.activeDepthTexture.IsValid())
                {
                    builder.SetRenderAttachmentDepth(
                        resources.activeDepthTexture,
                        AccessFlags.ReadWrite);
                }

                builder.AllowPassCulling(false);
                builder.SetRenderFunc(
                    (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(
                            false,
                            true,
                            Color.clear);
                        context.cmd.DrawRendererList(data.RendererList);
                    });
            }
        }
    }

    private sealed class CounterTimeCompositePass : ScriptableRenderPass
    {
        private const string MaskTextureName = "_CounterTimeMaskTexture";
        private const string DarkenAmountName = "_CounterTimeDarkenAmount";

        private sealed class PassData
        {
            public TextureHandle SourceColor;
            public TextureHandle MaskTexture;
            public TextureHandle DestinationColor;
            public Material Material;
            public float DarkenAmount;
        }

        private Material material;
        private float darkenAmount;

        public void Setup(Material compositeMaterial, float amount)
        {
            material = compositeMaterial;
            darkenAmount = amount;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            UniversalResourceData resources =
                frameData.Get<UniversalResourceData>();
            CounterTimeMaskData maskData =
                frameData.GetOrCreate<CounterTimeMaskData>();

            if (!maskData.MaskTexture.IsValid() ||
                !resources.activeColorTexture.IsValid())
            {
                return;
            }

            TextureDesc sourceDescriptor =
                resources.activeColorTexture.GetDescriptor(renderGraph);
            sourceDescriptor.name = "_CounterTimeSceneColor";
            sourceDescriptor.depthBufferBits = DepthBits.None;
            sourceDescriptor.msaaSamples = MSAASamples.None;
            sourceDescriptor.clearBuffer = false;
            TextureHandle sourceColor =
                renderGraph.CreateTexture(sourceDescriptor);
            renderGraph.AddCopyPass(
                resources.activeColorTexture,
                sourceColor,
                "Counter Time Copy Scene Color");

            using (var builder =
                renderGraph.AddUnsafePass<PassData>(
                    "Counter Time Darken Composite",
                    out PassData passData))
            {
                passData.SourceColor = sourceColor;
                passData.MaskTexture = maskData.MaskTexture;
                passData.DestinationColor = resources.activeColorTexture;
                passData.Material = material;
                passData.DarkenAmount = darkenAmount;

                builder.UseTexture(sourceColor, AccessFlags.Read);
                builder.UseTexture(maskData.MaskTexture, AccessFlags.Read);
                builder.UseTexture(
                    resources.activeColorTexture,
                    AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(
                    (PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer command =
                            CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        command.SetGlobalTexture(
                            MaskTextureName,
                            data.MaskTexture);
                        command.SetGlobalFloat(
                            DarkenAmountName,
                            data.DarkenAmount);
                        Blitter.BlitCameraTexture(
                            command,
                            data.SourceColor,
                            data.DestinationColor,
                            RenderBufferLoadAction.DontCare,
                            RenderBufferStoreAction.Store,
                            data.Material,
                            0);
                    });
            }
        }
    }
}
