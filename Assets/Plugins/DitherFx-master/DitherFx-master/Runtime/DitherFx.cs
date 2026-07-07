#if !VOL_FX

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

//  Dither © NullTale - https://x.com/NullTale
namespace VolFx
{
    public class DitherFx : ScriptableRendererFeature
    {
        protected static List<ShaderTagId> k_ShaderTags;
        
        public static int s_BlitTexId       = Shader.PropertyToID("_BlitTexture");
        public static int s_BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        
        [Tooltip("When to execute")]
        public RenderPassEvent _event  = RenderPassEvent.AfterRenderingPostProcessing;
        
        public DitherPass _pass;
        
        [HideInInspector]
        public Shader _blitShader;

        [NonSerialized]
        public Material _blit;
        
        [NonSerialized]
        public PassExecution _execution;

        // =======================================================================
        public class PassExecution : ScriptableRenderPass
        {
            public  DitherFx    _owner;
            
            // =======================================================================
            public void Init()
            {
                renderPassEvent = _owner._event;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                _owner._pass.Validate();
                if (_owner._pass.IsActive == false)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                var desc = renderGraph.GetTextureDesc(source);
                desc.depthBufferBits = 0;
                desc.name = _owner.name;

                // Allocate temporary texture
                TextureHandle destination = renderGraph.CreateTexture(desc);

                // Pass 1: Apply dither post-process
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(_owner.name, out var passData))
                {
                    passData.material = _owner._pass.Material;
                    passData.source = source;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                // Pass 2: Blit back to the camera target
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(_owner.name + "Copy", out var passData))
                {
                    passData.source = destination;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                    });
                }
            }

            private class PassData
            {
                public Material material;
                public TextureHandle source;
            }
        }
        
        // =======================================================================
        public void Blit(CommandBuffer cmd, RTHandle source, RTHandle destination)
        {
            cmd.SetGlobalVector(s_BlitScaleBiasId, new Vector4(1, 1, 0));
            cmd.SetGlobalTexture(s_BlitTexId, source);
            cmd.SetRenderTarget(destination, 0);
            cmd.DrawMesh(Utils.FullscreenMesh, Matrix4x4.identity, _blit, 0, 0);
        }
        
        public override void Create()
        {
#if UNITY_EDITOR
            _blitShader = Shader.Find("Hidden/Universal Render Pipeline/Blit");
            
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            _blit      = new Material(_blitShader);
            _execution = new PassExecution() { _owner = this };
            _execution.Init();
            
            if (_pass != null)
                _pass._init();
            
            if (k_ShaderTags == null)
            {
                k_ShaderTags = new List<ShaderTagId>(new[]
                {
                    new ShaderTagId("SRPDefaultUnlit"),
                    new ShaderTagId("UniversalForward"),
                    new ShaderTagId("UniversalForwardOnly")
                });
            }
        }
        
        private void Reset()
        {
#if UNITY_EDITOR
            if (_pass != null)
            {
                UnityEditor.AssetDatabase.RemoveObjectFromAsset(_pass);
                UnityEditor.AssetDatabase.SaveAssets();
                _pass = null;
            }
#endif
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView)
                return;
#if UNITY_EDITOR
            if (_blit == null)
                _blit = new Material(_blitShader);
            
            if (_pass == null)
                return;
#endif
            renderer.EnqueuePass(_execution);
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            if (_pass != null)
            {
                UnityEditor.AssetDatabase.RemoveObjectFromAsset(_pass);
                UnityEditor.AssetDatabase.SaveAssets();
                _pass = null;
            }
#endif
        }
    }
}

#endif