/// <summary>
/// Fichier : VolFx.Pass.cs
/// Définit la classe statique <see cref="VolFx"/> et la classe de base abstraite
/// <see cref="VolFx.Pass"/> pour toutes les passes de rendu du framework.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="VolFx.Pass"/> est un <see cref="ScriptableObject"/> qui encapsule :
/// <list type="bullet">
///   <item>Un <see cref="Shader"/> découvert automatiquement via <see cref="ShaderNameAttribute"/>.</item>
///   <item>Un <see cref="Material"/> créé à partir de ce shader.</item>
///   <item>Un accès simplifié à la <see cref="VolumeStack"/> URP via <see cref="Stack"/>.</item>
///   <item>Un cycle de vie : <see cref="Init"/> (une fois) → <see cref="Validate"/> (chaque frame) → <see cref="Invoke"/> (rendu) → <see cref="Cleanup"/> (nettoyage).</item>
/// </list>
/// </para>
/// <para>
/// Les passes concrètes (comme <see cref="MethilDitherPass"/>) doivent implémenter
/// <see cref="Validate"/> et peuvent surcharger les méthodes virtuelles selon leurs besoins.
/// </para>
/// </remarks>
#if !VOL_FX

using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolFx
{
    /// <summary>
    /// Classe statique contenant la définition de <see cref="Pass"/>.
    /// </summary>
    public static class VolFx
    {
        /// <summary>
        /// Classe de base abstraite pour toutes les passes de rendu du framework.
        /// Hérite de <see cref="ScriptableObject"/> pour être sérialisable et
        /// stockable en tant que sous-asset d'un <see cref="ScriptableRendererFeature"/>.
        /// </summary>
        [Serializable]
        public abstract class Pass : ScriptableObject
        {
            /// <summary>Référence au ScriptableRendererFeature propriétaire.</summary>
            [NonSerialized]
            public MethilDither _owner;
            /// <summary>Flag d'activation utilisateur de la passe.</summary>
            [SerializeField]
            internal bool _active = true;
            /// <summary>Shader utilisé par la passe (découvert via ShaderNameAttribute).</summary>
            [SerializeField] [HideInInspector]
            private Shader _shader;
            /// <summary>Material créé à partir du shader.</summary>
            protected Material _material;
            /// <summary>Material public (lecture seule).</summary>
            public Material Material => _material;
            /// <summary>État actif interne.</summary>
            private   bool     _isActive;
            
            /// <summary>
            /// Raccourci vers la pile de volumes URP.
            /// Permet d'accéder aux Volume Components comme <see cref="MethilDitherVol"/>.
            /// </summary>
            protected VolumeStack Stack => VolumeManager.instance.stack;
            
            /// <summary>
            /// Indique si le blit doit inverser l'axe Y.
            /// Surchargeable par les sous-classes.
            /// </summary>
            protected virtual bool Invert => false;

            /// <summary>
            /// État actif de la passe : actif utilisateur + material valide + validation frame.
            /// </summary>
            internal bool IsActive
            {
                get => _isActive && _active && _material != null;
                set => _isActive = value;
            }
            
            /// <summary>
            /// Active ou désactive la passe manuellement.
            /// </summary>
            /// <param name="isActive">Nouvel état.</param>
            public void SetActive(bool isActive)
            {
                _active = isActive;
            }
            
            /// <summary>
            /// Initialisation interne : localise le shader via <see cref="ShaderNameAttribute"/>,
            /// crée le material, puis appelle <see cref="Init"/>.
            /// Appelé par le <see cref="ScriptableRendererFeature"/> propriétaire.
            /// </summary>
            internal void _init()
            {
#if UNITY_EDITOR
#if !UNITY_2022_1_OR_NEWER
                Debug.LogError($"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} require Unity 2022 or higher");
#endif
                // Localise le shader à partir de l'attribut [ShaderName] sur la classe concrète
                if (_shader == null || _material == null)
                {
                    var shaderName = GetType().GetCustomAttributes(typeof(ShaderNameAttribute), true).FirstOrDefault() as ShaderNameAttribute;
                    if (shaderName != null)
                    {
                        _shader = Shader.Find(shaderName._name);
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
                }
#endif
                
                if (_shader != null)
                    _material = new Material(_shader);
                
                Init();
            }

            /// <summary>
            /// Méthode de rendu par défaut : blit la source vers la destination avec le material.
            /// Les sous-classes peuvent surcharger pour un rendu personnalisé.
            /// </summary>
            /// <param name="cmd">CommandBuffer actif.</param>
            /// <param name="source">Render texture source.</param>
            /// <param name="dest">Render texture destination.</param>
            /// <param name="context">Contexte de rendu scriptable.</param>
            /// <param name="renderingData">Données de rendu de la caméra.</param>
            public virtual void Invoke(CommandBuffer cmd, RTHandle source, RTHandle dest, ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Utils.Blit(cmd, source, dest, _material, 0, Invert);
            }
            
            /// <summary>
            /// Validation publique : vérifie/recharge le shader en éditeur, crée le material
            /// si nécessaire, puis appelle <see cref="Validate(Material)"/> pour la logique métier.
            /// Appelé chaque frame par le RenderGraph.
            /// </summary>
            public void Validate()
            {
#if UNITY_EDITOR
                if (_shader == null || _editorValidate)
                {
                    var shaderName = GetType().GetCustomAttributes(typeof(ShaderNameAttribute), true).FirstOrDefault() as ShaderNameAttribute;
                    if (shaderName != null)
                    {
                        _shader = Shader.Find(shaderName._name);
                        var assetPath = UnityEditor.AssetDatabase.GetAssetPath(_shader);
                        if (string.IsNullOrEmpty(assetPath) == false)
                            _editorSetup(Path.GetDirectoryName(assetPath), Path.GetFileNameWithoutExtension(assetPath));
                        
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
                }
                
                if ((_material == null || _material.shader != _shader) && _shader != null)
                {
                    _material = new Material(_shader);
                    Init();
                }
#endif
                
                IsActive = Validate(_material);
            }

            /// <summary>
            /// Appelé une fois après la création du material.
            /// Surchargeable pour initialiser l'état de la passe.
            /// </summary>
            public virtual void Init()
            {
            }

            /// <summary>
            /// Appelé chaque frame pour vérifier si le rendu est nécessaire
            /// et configurer les propriétés du material.
            /// </summary>
            /// <param name="mat">Le material à configurer.</param>
            /// <returns><c>true</c> si le rendu doit avoir lieu, <c>false</c> sinon.</returns>
            public abstract bool Validate(Material mat);
            
            /// <summary>
            /// Appelé après le rendu pour libérer les ressources.
            /// Surchargeable par les passes qui allouent des ressources temporaires.
            /// </summary>
            /// <param name="cmd">CommandBuffer actif.</param>
            public virtual void Cleanup(CommandBuffer cmd)
            {
            }
            
            /// <summary>
            /// Indique si la validation éditeur est nécessaire.
            /// Si <c>true</c>, <see cref="_editorSetup"/> sera appelée.
            /// </summary>
            protected virtual bool _editorValidate => false;
            
            /// <summary>
            /// Hook éditeur appelé quand le shader est localisé.
            /// Permet de charger des assets supplémentaires (textures par défaut, etc.).
            /// </summary>
            /// <param name="folder">Dossier contenant le shader.</param>
            /// <param name="asset">Nom du fichier shader (sans extension).</param>
            protected virtual void _editorSetup(string folder, string asset)
            {
            }
        }
    }
}
#endif
