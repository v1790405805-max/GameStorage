using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace PixelRender
{
    public class OutlineMetadataTargetData : ContextItem
    {
        public TextureHandle Color;
        public TextureHandle Depth;

        public override void Reset()
        {
            Color = TextureHandle.nullHandle;
            Depth = TextureHandle.nullHandle;
        }
    }
    
    public class OutlineTargetData : ContextItem
    {
        public TextureHandle Color;
        public override void Reset()
        {
            Color = TextureHandle.nullHandle;
        }
    }

    public class AddLightTargetData : ContextItem
    {
        public TextureHandle Color;
        public override void Reset()
        {
            Color = TextureHandle.nullHandle;
        }
    }
}
