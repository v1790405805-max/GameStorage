using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PixelRender
{
    public class PixelRenderFeature : ScriptableRendererFeature
    {
        private OutlineRenderPass _outlinePass;
        private OutlineDrawRenderPass _outlineDrawPass;
        
        public override void Create()
        {
            _outlinePass = new OutlineRenderPass();
            _outlineDrawPass = new OutlineDrawRenderPass();
            _outlinePass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            _outlineDrawPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _outlineDrawPass.Setup();
            renderer.EnqueuePass(_outlinePass);
            renderer.EnqueuePass(_outlineDrawPass);
        }
    }
}


