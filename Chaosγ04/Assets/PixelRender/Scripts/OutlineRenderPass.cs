using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PixelRender
{
    public class OutlineRenderPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId _outlineMetadataLightmodeShaderTag = new ShaderTagId("Outlines");

        public class PassData
        {
            public RendererListHandle MetadataObjects;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var metadataTarget = frameData.GetOrCreate<OutlineMetadataTargetData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.GetOrCreate<UniversalRenderingData>();
            var resources = frameData.Get<UniversalResourceData>();
            
            var colorDescriptor = resources.activeColorTexture.GetDescriptor(renderGraph);
            colorDescriptor.useMipMap = false;
            colorDescriptor.name = "Outline_Metadata";
            colorDescriptor.clearBuffer = false;
            colorDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
            colorDescriptor.wrapMode = TextureWrapMode.Clamp;
            var depthDescriptor = colorDescriptor;
            depthDescriptor.depthBufferBits = DepthBits.Depth32;
            depthDescriptor.name = "Outline_Metadata_Depth";
            depthDescriptor.clearBuffer = false;
            depthDescriptor.wrapMode = TextureWrapMode.Clamp;
            metadataTarget.Color = renderGraph.CreateTexture(colorDescriptor);
            metadataTarget.Depth = renderGraph.CreateTexture(depthDescriptor);
            
            var sort = new SortingSettings(cameraData.camera);
            var drawingSettings = new DrawingSettings( _outlineMetadataLightmodeShaderTag, sort);
            var filteringSettings = new FilteringSettings(RenderQueueRange.all);
            var renderListParams = new RendererListParams( renderingData.cullResults , drawingSettings, filteringSettings);


            using (var builder = renderGraph.AddRasterRenderPass<PassData>(GetType().Name, out var passData))
            {
                passData.MetadataObjects = renderGraph.CreateRendererList(renderListParams);
                
                builder.UseRendererList(passData.MetadataObjects);
                builder.SetRenderAttachment(metadataTarget.Color, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(metadataTarget.Depth, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData passData, RasterGraphContext context) => ExecutePass(passData,context));
            }
            
        }

        private void ExecutePass(PassData passData, RasterGraphContext context)
        {
            context.cmd.ClearRenderTarget(true,true,Color.black);
            context.cmd.DrawRendererList(passData.MetadataObjects);
        }
    }
}

