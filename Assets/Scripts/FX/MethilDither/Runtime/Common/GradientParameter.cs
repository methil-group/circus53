/// <summary>
/// Fichier : GradientParameter.cs
/// Fournit un <see cref="VolumeParameter{T}"/> spécialisé pour <see cref="GradientValue"/>,
/// permettant d'utiliser des dégradés comme paramètres dans les Volume Components URP.
/// </summary>
/// <remarks>
/// Fonctionne de manière identique à <see cref="CurveParameter"/> mais pour les dégradés.
/// L'interpolation entre deux dégradés mélange les pixels et choisit le mode du dégradé
/// le plus proche selon le facteur t.
/// </remarks>
#if !VOL_FX

using System;
using UnityEngine.Rendering;

namespace VolFx
{
    /// <summary>
    /// Paramètre de volume pour un dégradé.
    /// Permet de stocker et d'interpoler un <see cref="GradientValue"/> dans un Volume Profile URP.
    /// </summary>
    [Serializable]
    public class GradientParameter : VolumeParameter<GradientValue> 
    {
        /// <summary>
        /// Constructeur principal.
        /// </summary>
        /// <param name="value">La valeur initiale du dégradé.</param>
        /// <param name="overrideState">Si <c>true</c>, le paramètre surcharge la valeur par défaut.</param>
        public GradientParameter(GradientValue value, bool overrideState) : base(value, overrideState) { }

        /// <summary>
        /// Interpole entre deux valeurs de dégradé.
        /// Appelé par le système de Volume lors des transitions.
        /// </summary>
        /// <param name="from">Valeur de départ.</param>
        /// <param name="to">Valeur d'arrivée.</param>
        /// <param name="t">Facteur d'interpolation (0 à 1).</param>
        public override void Interp(GradientValue from, GradientValue to, float t)
        {
            m_Value.Blend(from, to, t);
        }

        /// <summary>
        /// Copie la valeur d'un autre paramètre de volume.
        /// </summary>
        /// <param name="parameter">Le paramètre source à copier.</param>
        public override void SetValue(VolumeParameter parameter)
        {
            m_Value.SetValue(((VolumeParameter<GradientValue>)parameter).value);
        }
    }
}

#endif
