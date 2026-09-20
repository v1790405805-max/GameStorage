using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PixelRender
{
    public class SdfLightFeature : ScriptableRendererFeature{

        private SdfLightPass _sdfLightPass;
        private SdfLightBlendPass _sdfLightBlendPass;
        
        public override void Create() {
            _sdfLightPass = new SdfLightPass();
            _sdfLightPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            _sdfLightBlendPass = new SdfLightBlendPass();
            _sdfLightBlendPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if(SDFLightVolume.Instance == null)
                return;
            _sdfLightPass.Setup();
            _sdfLightBlendPass.Setup();
            renderer.EnqueuePass(_sdfLightPass);
            renderer.EnqueuePass(_sdfLightBlendPass);
        }
        
        private class SdfLightPass : ScriptableRenderPass
        {
            private const string SDFLIGHT_BUFFER = "_SdfAddLightTex";
            private Material _lightMaterial;
            private readonly int _sdflight_Buffer_Global_ID = Shader.PropertyToID(SDFLIGHT_BUFFER);
            
            private const int KLightDataSize = (4 * 4 + 1 + 4 + 2 + 1 + 1 + 1 + 1) * 4;
            private const int KSphereMaxCount = 16;//圆形光源最大数量
            private const int KBoxMaxCount = 8;//矩形光源最大数量
            private const int KTorusMaxCout = 8;//圆环光源最大数量
            private const int KCapsuleMaxCount = 8;//胶囊光源最大数量
            
            
            private GraphicsBuffer _sphereBuffer;
            private GraphicsBuffer _boxBuffer;
            private GraphicsBuffer _torusBuffer;
            private GraphicsBuffer _capsuleBuffer;

            private int _sphereCountShaderID = Shader.PropertyToID("_sphereCount");
            private int _boxCountShaderID = Shader.PropertyToID("_boxCount");
            private int _torusCountShaderID = Shader.PropertyToID("_torusCount");
            private int _capsuleCountShaderID = Shader.PropertyToID("_capsuleCount");
            private int _sphereBufferID = Shader.PropertyToID("_sphereBuffer");
            private int _boxBufferID = Shader.PropertyToID("_boxBuffer");
            private int _torusBufferID = Shader.PropertyToID("_torusBuffer");
            private int _capsuleBufferID = Shader.PropertyToID("_capsuleBuffer");
            private int _inverseProjectionMatrixID = Shader.PropertyToID("_InverseProjectionMatrix");
            private int _inverseViewMatrixID = Shader.PropertyToID("_InverseViewMatrix");
            private int _cameraNormalsTextureID = Shader.PropertyToID("_CameraNormalsTexture");

            public SdfLightPass()
            {
                _sphereBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,KSphereMaxCount, KLightDataSize + (1) * 4);
                _boxBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,KBoxMaxCount, KLightDataSize + (3 + 1) * 4);
                _torusBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,KTorusMaxCout, KLightDataSize + (3) * 4);
                _capsuleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,KCapsuleMaxCount, KLightDataSize + (1+1) * 4);
            }

            private class PassData
            {
                public BufferHandle SphereBufferHandle;
                public BufferHandle BoxBufferHandle;
                public BufferHandle TorusBufferHandle;
                public BufferHandle CapsuleBufferHandle;
                public Matrix4x4 InverseProjectionMatrix;
                public Matrix4x4 InverseViewMatrix;
                public TextureHandle NormalColor;
                public TextureHandle LightColor;
                public Material LightMaterial;
            }

            private void InitMaterial()
            {
                if(_lightMaterial == null)
                    _lightMaterial = new Material(Shader.Find("Hidden/PixelRender/SdfLight"));
            }

            public void Setup()
            {
                // 配置摄像机输出深度和法线纹理
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                InitMaterial();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var addLight = frameData.GetOrCreate<AddLightTargetData>();
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                var addLightDescriptor = resources.activeColorTexture.GetDescriptor(renderGraph);
                addLightDescriptor.useMipMap = false;
                addLightDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
                addLightDescriptor.depthBufferBits = DepthBits.None;
                addLightDescriptor.name = SDFLIGHT_BUFFER;
                addLightDescriptor.clearBuffer = true;
                addLightDescriptor.wrapMode = TextureWrapMode.Clamp;

                addLight.Color = renderGraph.CreateTexture(addLightDescriptor);
                
                Matrix4x4 projectionMatrix =
                    GL.GetGPUProjectionMatrix(cameraData.camera.projectionMatrix, false);
                
                var volume = SDFLightVolume.Instance;
                var sphereDatas = volume.GetSphereDatas();
                var boxDatas = volume.GetBoxDatas();
                var capsuleDatas = volume.GetCapsuleDatas();
                var torusDatas = volume.GetTorusDatas();
                
                _lightMaterial.SetMatrix(_inverseProjectionMatrixID, projectionMatrix.inverse);
                _lightMaterial.SetMatrix(_inverseViewMatrixID,cameraData.camera.cameraToWorldMatrix);
                
                _lightMaterial.SetInt(_sphereCountShaderID, sphereDatas.Length);
                _lightMaterial.SetInt(_boxCountShaderID, boxDatas.Length);
                _lightMaterial.SetInt(_torusCountShaderID, torusDatas.Length);
                _lightMaterial.SetInt(_capsuleCountShaderID, capsuleDatas.Length);
                
                using (var builder = renderGraph.AddUnsafePass<PassData>(GetType().Name, out var passData))
                {
                    passData.NormalColor = resources.cameraNormalsTexture;
                    passData.LightColor = addLight.Color;
                    passData.LightMaterial = _lightMaterial;

                    if (sphereDatas.Length > 0)
                    {
                        _sphereBuffer.SetData(sphereDatas,0,0,sphereDatas.Length);
                    }
                    if (boxDatas.Length > 0)
                    {
                        _boxBuffer.SetData(boxDatas,0, 0,boxDatas.Length);
                    }
                    if (capsuleDatas.Length > 0)
                    {
                        _capsuleBuffer.SetData(capsuleDatas,0,0,capsuleDatas.Length);
                    }
                    if (torusDatas.Length > 0)
                    {
                        _torusBuffer.SetData(torusDatas,0,0,torusDatas.Length);
                    }

                   
                    
                    var sphereHandle = renderGraph.ImportBuffer(_sphereBuffer);
                    passData.SphereBufferHandle = sphereHandle;
                    
                    var boxHandle = renderGraph.ImportBuffer(_boxBuffer);
                    passData.BoxBufferHandle = boxHandle;
                    
                    var capsuleHandle = renderGraph.ImportBuffer(_capsuleBuffer); 
                    passData.CapsuleBufferHandle = capsuleHandle;
                    
                    var torusHandle = renderGraph.ImportBuffer(_torusBuffer);
                    passData.TorusBufferHandle = torusHandle;
                    
                    builder.SetGlobalTextureAfterPass(addLight.Color,_sdflight_Buffer_Global_ID);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.UseTexture(addLight.Color,AccessFlags.Write);
                    builder.UseTexture(resources.cameraNormalsTexture,AccessFlags.Read);
                    builder.UseBuffer(sphereHandle, AccessFlags.Read);
                    builder.UseBuffer(boxHandle, AccessFlags.Read);
                    builder.UseBuffer(capsuleHandle, AccessFlags.Read);
                    builder.UseBuffer(torusHandle, AccessFlags.Read);
                    builder.SetRenderFunc((PassData passData, UnsafeGraphContext context) => ExecutePass(passData, context));
                    
                }
            }
            
            private void ExecutePass(PassData passData, UnsafeGraphContext context)
            {
                var command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                command.SetGlobalTexture(_cameraNormalsTextureID, passData.NormalColor);
                command.SetGlobalBuffer(_sphereBufferID, passData.SphereBufferHandle);
                command.SetGlobalBuffer(_boxBufferID, passData.BoxBufferHandle);
                command.SetGlobalBuffer(_capsuleBufferID, passData.CapsuleBufferHandle);
                command.SetGlobalBuffer(_torusBufferID, passData.TorusBufferHandle);
                Blitter.BlitCameraTexture(command,passData.NormalColor, passData.LightColor, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.LightMaterial, 0);
                
            }
        }
        
        private class SdfLightBlendPass : ScriptableRenderPass
        {
            private Material _blenderMaterial;
            
            private class PassData
            {
                public TextureHandle LightColor;
                public TextureHandle CameraColor;
                public TextureHandle TempCopy;
                public Material Material;
            }

            private void InitMaterial()
            {
                if(_blenderMaterial == null)
                    _blenderMaterial = new Material(Shader.Find("Hidden/PixelRender/SdfLightBlend"));
            }

            public void Setup()
            {
                InitMaterial();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var addLight = frameData.Get<AddLightTargetData>();
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                
                var descriptor = resources.activeColorTexture.GetDescriptor(renderGraph);
                
                var tempCopy = renderGraph.CreateTexture(descriptor);
                using (var builder = renderGraph.AddUnsafePass<PassData>(GetType().Name, out var passData))
                {
                    passData.LightColor = addLight.Color;
                    passData.CameraColor = resources.activeColorTexture;
                    passData.Material = _blenderMaterial;
                    passData.TempCopy = tempCopy;
                    
                    builder.UseTexture(addLight.Color,AccessFlags.Read);
                    builder.UseTexture(resources.activeColorTexture,AccessFlags.Write);
                    builder.UseTexture(tempCopy);
                    builder.SetRenderFunc((PassData passData, UnsafeGraphContext context) => ExecutePass(passData, context));
                }
            }
            
            private void ExecutePass(PassData passData, UnsafeGraphContext context)
            {
                var command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                command.SetGlobalTexture("_AddLightTex", passData.LightColor);
                command.SetGlobalTexture("_CameraTex",passData.TempCopy);
                Blitter.BlitCameraTexture(command,passData.CameraColor,passData.TempCopy);
                Blitter.BlitCameraTexture(command, passData.TempCopy,passData.CameraColor, passData.Material,0);
            }
        }
    }
}
