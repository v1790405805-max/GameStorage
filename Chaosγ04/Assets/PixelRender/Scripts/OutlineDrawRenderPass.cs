using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PixelRender
{
    public class OutlineDrawRenderPass : ScriptableRenderPass
    {
        private const string OUTLINE_BUFFER = "_PixelOutlineTex";
        private Material _material;
        private readonly int _outline_Buffer_Global_ID = Shader.PropertyToID(OUTLINE_BUFFER);
        

        private void InitMater()
        {
            if(_material == null)
                _material = new Material(Shader.Find("Hidden/PixelRender/Outline"));
        }

        private class PassData
        {
            public TextureHandle MetadataColor;
            public TextureHandle OutlineColor;
            public TextureHandle NormalColor;
            public Material Material;
        }

        public void Setup()
        {
            // 配置摄像机输出深度和法线纹理
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            InitMater();
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var metadata = frameData.Get<OutlineMetadataTargetData>();
            var outline = frameData.GetOrCreate<OutlineTargetData>();
            var resources = frameData.Get<UniversalResourceData>();
            
            var outlineDescriptor = metadata.Color.GetDescriptor(renderGraph);
            outlineDescriptor.useMipMap = false;
            outlineDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            outlineDescriptor.depthBufferBits = DepthBits.None;
            outlineDescriptor.name = OUTLINE_BUFFER;
            outlineDescriptor.clearBuffer = true;
            outlineDescriptor.wrapMode = TextureWrapMode.Clamp;
            
            outline.Color = renderGraph.CreateTexture(outlineDescriptor);
            
            using (var builder = renderGraph.AddUnsafePass<PassData>(GetType().Name, out var passData))
            {
                passData.OutlineColor = outline.Color;
                passData.MetadataColor = metadata.Color;
                passData.NormalColor = resources.cameraNormalsTexture;
                passData.Material = _material;
                
                builder.SetGlobalTextureAfterPass(outline.Color, _outline_Buffer_Global_ID);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.UseTexture(outline.Color,AccessFlags.Write);
                builder.UseTexture(metadata.Color, AccessFlags.Read);
                builder.SetRenderFunc((PassData passData, UnsafeGraphContext context) => ExecutePass(passData, context));
            }
            
        }

        private void ExecutePass(PassData passData, UnsafeGraphContext context)
        {
            var command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            command.SetGlobalTexture("_OutlineTex", passData.MetadataColor);
            command.SetGlobalTexture("_NormalTex", passData.NormalColor);
            Blitter.BlitCameraTexture(command,passData.MetadataColor, passData.OutlineColor, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.Material, 0);
        }
    }
}

