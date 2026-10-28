using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace EnvironmentCartoon
{
    public class EnvironmentCartoonOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _outlineShader;

        private EnvironmentCartoonOutlineMetadataPass _metadataPass;
        private EnvironmentCartoonOutlineCompositePass _compositePass;

        public override void Create()
        {
            _metadataPass = new EnvironmentCartoonOutlineMetadataPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
            };
            _compositePass = new EnvironmentCartoonOutlineCompositePass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _compositePass.Setup(_outlineShader);
            renderer.EnqueuePass(_metadataPass);
            renderer.EnqueuePass(_compositePass);
        }
    }

    internal class EnvironmentCartoonOutlineMetadataData : ContextItem
    {
        public TextureHandle Color;
        public TextureHandle Depth;

        public override void Reset()
        {
            Color = default;
            Depth = default;
        }
    }

    internal class EnvironmentCartoonOutlineTargetData : ContextItem
    {
        public TextureHandle Color;

        public override void Reset()
        {
            Color = default;
        }
    }

    internal class EnvironmentCartoonOutlineMetadataPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId OutlineMetadataTag = new ShaderTagId("EnvironmentOutlines");

        private class PassData
        {
            public RendererListHandle Renderers;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            EnvironmentCartoonOutlineMetadataData metadata =
                frameData.GetOrCreate<EnvironmentCartoonOutlineMetadataData>();

            TextureDesc colorDescriptor = resources.activeColorTexture.GetDescriptor(renderGraph);
            colorDescriptor.name = "EnvironmentCartoonOutlineMetadata";
            colorDescriptor.useMipMap = false;
            colorDescriptor.clearBuffer = false;
            colorDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
            colorDescriptor.wrapMode = TextureWrapMode.Clamp;
            colorDescriptor.filterMode = FilterMode.Point;

            TextureDesc depthDescriptor = colorDescriptor;
            depthDescriptor.name = "EnvironmentCartoonOutlineMetadataDepth";
            depthDescriptor.depthBufferBits = DepthBits.Depth32;

            metadata.Color = renderGraph.CreateTexture(colorDescriptor);
            metadata.Depth = renderGraph.CreateTexture(depthDescriptor);

            var sorting = new SortingSettings(cameraData.camera);
            var drawing = new DrawingSettings(OutlineMetadataTag, sorting);
            var filtering = new FilteringSettings(RenderQueueRange.all);
            var rendererListParams = new RendererListParams(renderingData.cullResults, drawing, filtering);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(GetType().Name, out PassData passData))
            {
                passData.Renderers = renderGraph.CreateRendererList(rendererListParams);
                builder.UseRendererList(passData.Renderers);
                builder.SetRenderAttachment(metadata.Color, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(metadata.Depth, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(true, true, Color.black);
                    context.cmd.DrawRendererList(data.Renderers);
                });
            }
        }
    }

    internal class EnvironmentCartoonOutlineCompositePass : ScriptableRenderPass
    {
        private const string OutlineTextureName = "_EnvironmentCartoonOutlineTex";
        private static readonly int OutlineTextureId = Shader.PropertyToID(OutlineTextureName);
        private Material _material;

        private class PassData
        {
            public TextureHandle MetadataColor;
            public TextureHandle OutlineColor;
            public Material Material;
        }

        public void Setup(Shader outlineShader)
        {
            if (_material == null && outlineShader != null)
            {
                _material = new Material(outlineShader);
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
            {
                return;
            }

            EnvironmentCartoonOutlineMetadataData metadata =
                frameData.Get<EnvironmentCartoonOutlineMetadataData>();
            EnvironmentCartoonOutlineTargetData outline =
                frameData.GetOrCreate<EnvironmentCartoonOutlineTargetData>();

            TextureDesc descriptor = metadata.Color.GetDescriptor(renderGraph);
            descriptor.name = OutlineTextureName;
            descriptor.useMipMap = false;
            descriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.clearBuffer = true;
            descriptor.wrapMode = TextureWrapMode.Clamp;

            outline.Color = renderGraph.CreateTexture(descriptor);

            using (var builder = renderGraph.AddUnsafePass<PassData>(GetType().Name, out PassData passData))
            {
                passData.MetadataColor = metadata.Color;
                passData.OutlineColor = outline.Color;
                passData.Material = _material;

                builder.SetGlobalTextureAfterPass(outline.Color, OutlineTextureId);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.UseTexture(metadata.Color, AccessFlags.Read);
                builder.UseTexture(outline.Color, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    command.SetGlobalTexture("_EnvironmentCartoonOutlineMetadataTex", data.MetadataColor);
                    Blitter.BlitCameraTexture(
                        command,
                        data.MetadataColor,
                        data.OutlineColor,
                        RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store,
                        data.Material,
                        0);
                });
            }
        }
    }
}
