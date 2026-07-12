/// <summary>
/// Fichier : DitherVol.cs — VolumeComponent MethilDither pour le système de Volume URP.
/// 
/// <see cref="MethilDitherVol"/> expose les paramètres de l'effet de dithering dans les
/// Volume Profiles Unity. Ces paramètres peuvent être surchargés par scène, par zone
/// (Box Volume, Sphere Volume) ou globalement, et sont lus par <see cref="MethilDitherPass"/>.
/// 
/// Paramètres exposés :
/// <list type="bullet">
///   <item><b>Impact</b> (0-1) — Intensité globale de l'effet.</item>
///   <item><b>Power</b> (0-1) — Puissance/seuil du dithering.</item>
///   <item><b>Scale</b> (0-1) — Échelle de l'image (pixelation).</item>
///   <item><b>Pixelate</b> (bool) — Active/désactive la pixellisation.</item>
///   <item><b>Fps</b> (0-120) — Images par seconde pour la gigue du dithering.</item>
///   <item><b>Palette</b> (Texture2D) — Texture de palette personnalisée.</item>
///   <item><b>Pattern</b> (Texture2D) — Motif de dithering personnalisé.</item>
///   <item><b>Mode</b> (Dither/Noise) — Mode de dithering : motif ou bruit.</item>
/// </list>
/// 
/// Le VolumeComponent est actif si au moins un des paramètres Impact, Power ou Scale
/// justifie un rendu (IsActive retourne true).
/// </summary>
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolFx
{
    /// <summary>
    /// Composant de volume pour l'effet MethilDither.
    /// Apparaît dans le menu "VolFx/MethilDither" des Volume Profiles.
    /// </summary>
    [Serializable, VolumeComponentMenu("VolFx/MethilDither")]
    public sealed class MethilDitherVol : VolumeComponent, IPostProcessComponent
    {
        // Ancien paramètre de seuil basé sur une courbe (commenté, conservé pour référence)
        /*[HideInInspector]
        public CurveParameter        m_Threshold  = new CurveParameter(new CurveValue(new AnimationCurve(
                                                                                      new Keyframe(0f, 0f),
                                                                                      new Keyframe(1f, 0f))), false);*/
        
        /// <summary>Intensité globale de l'effet (0 = pas d'effet, 1 = effet complet).</summary>
        public ClampedFloatParameter m_Impact = new ClampedFloatParameter(0, 0, 1);
        
        /// <summary>Puissance du dithering — contrôle le seuil de remplacement des couleurs.</summary>
        public ClampedFloatParameter m_Power = new ClampedFloatParameter(0, 0, 1);
        
        /// <summary>Échelle de l'image pour la pixellisation (1 = pas de pixellisation, 0 = max).</summary>
        public ClampedFloatParameter m_Scale    = new ClampedFloatParameter(1, 0, 1);
        /// <summary>Active ou désactive la pixellisation.</summary>
        public BoolParameter         m_Pixelate = new BoolParameter(true, false);
        
        /// <summary>Images par seconde pour la gigue (jitter) du motif de dithering.</summary>
        public ClampedIntParameter   m_Fps     = new ClampedIntParameter(0, 0, 120);
        /// <summary>Texture de palette (override). Null = utilise la palette par défaut.</summary>
        public Texture2DParameter    m_Palette = new Texture2DParameter(null, false);
        /// <summary>Texture de motif de dithering (override). Null = utilise le motif par défaut.</summary>
        public Texture2DParameter    m_Pattern = new Texture2DParameter(null, false);
        /// <summary>Mode de dithering : Dither (motif) ou Noise (bruit).</summary>
        public NoiseModeParameter    m_Mode = new NoiseModeParameter(MethilDitherPass.Mode.Dither, false);

        /// <summary>
        /// Paramètre de volume personnalisé pour le type enum <see cref="MethilDitherPass.Mode"/>.
        /// Wrapper nécessaire car Unity ne supporte pas nativement les enums comme VolumeParameter.
        /// </summary>
        [Serializable]
        public class NoiseModeParameter : VolumeParameter<MethilDitherPass.Mode>
        {
            /// <summary>
            /// Constructeur.
            /// </summary>
            /// <param name="value">Mode initial (Dither ou Noise).</param>
            /// <param name="overrideState">Si true, surcharge la valeur par défaut.</param>
            public NoiseModeParameter(MethilDitherPass.Mode value, bool overrideState) : base(value, overrideState) { }
        }
        
        // =======================================================================
        // IPostProcessComponent
        
        /// <summary>
        /// Détermine si l'effet doit être appliqué.
        /// Actif si le composant est activé ET qu'au moins un des paramètres
        /// Impact, Power ou Scale justifie un traitement.
        /// </summary>
        public bool IsActive() => active && (m_Scale.value < 1f || m_Power.value > 0f || m_Impact.value > 0f);

        /// <summary>
        /// Indique que cet effet n'est pas compatible avec le tile rendering.
        /// </summary>
        public bool IsTileCompatible() => false;
    }
}
