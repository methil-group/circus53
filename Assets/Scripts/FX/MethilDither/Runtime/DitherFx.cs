/// <summary>
/// Fichier : DitherFx.cs — Point d'entrée du plugin MethilDither dans le pipeline de rendu URP.
/// 
/// <see cref="MethilDither"/> est un <see cref="ScriptableRendererFeature"/> qui s'intègre
/// dans l'Universal Render Pipeline pour appliquer un effet de dithering par palette.
/// 
/// Architecture :
/// <list type="bullet">
///   <item><see cref="MethilDither"/> (ScriptableRendererFeature) — enregistré dans le Renderer Asset URP.</item>
///   <item><see cref="PassExecution"/> (ScriptableRenderPass, classe imbriquée) — exécute le rendu via l'API RenderGraph.</item>
///   <item><see cref="MethilDitherPass"/> (VolFx.Pass) — logique de dithering, configurée comme sous-asset.</item>
/// </list>
/// 
/// Flux de rendu (RenderGraph) :
/// <list type="number">
///   <item><c>PassExecution.RecordRenderGraph</c> est appelé par URP.</item>
///   <item>Validation de la passe (<c>_pass.Validate()</c>) — si inactive, on sort.</item>
///   <item><b>Passe 1</b> : Application du dithering via <c>Blitter.BlitTexture</c> avec le material de la passe.</item>
///   <item><b>Passe 2</b> : Copie du résultat vers la cible caméra avec <c>Blitter.BlitTexture</c> sans material.</item>
/// </list>
/// </summary>
/// <remarks>
/// Compatible uniquement avec Unity 2022+ (URP avec API RenderGraph).
/// L'attribut <c>#if !VOL_FX</c> empêche la compilation si le package VolFx complet est installé.
/// </remarks>
#if !VOL_FX

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace VolFx
{
    /// <summary>
    /// ScriptableRendererFeature principal de MethilDither.
    /// S'ajoute dans le Renderer Asset URP et pilote l'exécution de la passe de dithering.
    /// </summary>
    public class MethilDither : ScriptableRendererFeature
    {
        /// <summary>Tags de shader utilisés pour identifier les objets rendus (non utilisé directement ici).</summary>
        protected static List<ShaderTagId> k_ShaderTags;
        
        /// <summary>ID shader pour _BlitTexture (utilisé par le blit manuel).</summary>
        public static int s_BlitTexId       = Shader.PropertyToID("_BlitTexture");
        /// <summary>ID shader pour _BlitScaleBias (utilisé par le blit manuel).</summary>
        public static int s_BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        
        /// <summary>Moment d'exécution dans la pipeline (par défaut après le post-processing).</summary>
        [Tooltip("When to execute")]
        public RenderPassEvent _event  = RenderPassEvent.AfterRenderingPostProcessing;
        
        /// <summary>La passe de dithering (ScriptableObject, sous-asset de ce RendererFeature).</summary>
        public MethilDitherPass _pass;
        
        /// <summary>Shader de blit URP standard.</summary>
        [HideInInspector]
        public Shader _blitShader;

        /// <summary>Material de blit créé à partir du shader URP Blit.</summary>
        [NonSerialized]
        public Material _blit;
        
        /// <summary>Instance de la passe d'exécution (ScriptableRenderPass).</summary>
        [NonSerialized]
        public PassExecution _execution;

        /// <summary>
        /// ScriptableRenderPass imbriquée qui implémente le rendu via l'API RenderGraph d'URP.
        /// </summary>
        /// <remarks>
        /// La méthode <see cref="RecordRenderGraph"/> est le point d'entrée appelé par URP.
        /// Elle crée deux passes raster :
        /// <list type="number">
        ///   <item>Application du dithering (source → texture temporaire avec material).</item>
        ///   <item>Copie finale (texture temporaire → cible caméra).</item>
        /// </list>
        /// </remarks>
        public class PassExecution : ScriptableRenderPass
        {
            /// <summary>Référence au RendererFeature propriétaire.</summary>
            public MethilDither _owner;
            
            /// <summary>
            /// Initialise l'événement de rendu depuis la configuration du feature.
            /// </summary>
            public void Init()
            {
                renderPassEvent = _owner._event;
            }

            /// <summary>
            /// Point d'entrée RenderGraph — configurer et exécuter les passes de rendu.
            /// </summary>
            /// <param name="renderGraph">Le RenderGraph URP courant.</param>
            /// <param name="frameData">Données de frame (contient les ressources universelles).</param>
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

                // Allocation de la texture temporaire
                TextureHandle destination = renderGraph.CreateTexture(desc);

                // Passe 1 : Appliquer le post-process de dithering
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

                // Passe 2 : Copier le résultat vers la cible caméra
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

            /// <summary>
            /// Conteneur de données pour les lambdas de rendu RenderGraph.
            /// </summary>
            private class PassData
            {
                /// <summary>Material de la passe de dithering.</summary>
                public Material material;
                /// <summary>Texture source à traiter.</summary>
                public TextureHandle source;
            }
        }
        
        /// <summary>
        /// Blit manuel (non utilisé par le chemin RenderGraph, conservé pour compatibilité).
        /// </summary>
        /// <param name="cmd">CommandBuffer.</param>
        /// <param name="source">Texture source.</param>
        /// <param name="destination">Texture destination.</param>
        public void Blit(CommandBuffer cmd, RTHandle source, RTHandle destination)
        {
            cmd.SetGlobalVector(s_BlitScaleBiasId, new Vector4(1, 1, 0));
            cmd.SetGlobalTexture(s_BlitTexId, source);
            cmd.SetRenderTarget(destination, 0);
            cmd.DrawMesh(Utils.FullscreenMesh, Matrix4x4.identity, _blit, 0, 0);
        }
        
        /// <summary>
        /// Appelé par URP à la création du RendererFeature.
        /// Initialise le material de blit, la passe d'exécution, la liste de shader tags,
        /// et la sous-passe de dithering.
        /// </summary>
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
        
        /// <summary>
        /// Réinitialisation : supprime le sous-asset de la passe et sauvegarde.
        /// </summary>
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

        /// <summary>
        /// Ajoute la passe d'exécution au renderer pour les caméras Game et SceneView.
        /// </summary>
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

        /// <summary>
        /// Nettoyage à la destruction : supprime le sous-asset.
        /// </summary>
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
