/// <summary>
/// Fichier : CurveParameter.cs
/// Fournit un <see cref="VolumeParameter{T}"/> spécialisé pour <see cref="CurveValue"/>,
/// permettant d'utiliser des courbes d'animation comme paramètres dans les Volume Components URP.
/// </summary>
/// <remarks>
/// Hérite de <see cref="VolumeParameter{T}"/> pour s'intégrer au système de Volume d'Unity.
/// Implémente <see cref="Interp"/> pour l'interpolation entre deux valeurs de courbe
/// et <see cref="SetValue"/> pour la copie de paramètres.
/// </remarks>
#if !VOL_FX

using System;
using UnityEngine.Rendering;

namespace VolFx
{
    /// <summary>
    /// Paramètre de volume pour une courbe d'animation.
    /// Permet de stocker et d'interpoler une <see cref="CurveValue"/> dans un Volume Profile URP.
    /// </summary>
    [Serializable]
    public class CurveParameter : VolumeParameter<CurveValue> 
    {
        /// <summary>
        /// Constructeur principal.
        /// </summary>
        /// <param name="value">La valeur initiale de la courbe.</param>
        /// <param name="overrideState">Si <c>true</c>, le paramètre surcharge la valeur par défaut.</param>
        public CurveParameter(CurveValue value, bool overrideState) : base(value, overrideState) { }

        /// <summary>
        /// Interpole linéairement entre deux valeurs de courbe.
        /// Appelé par le système de Volume lors des transitions.
        /// </summary>
        /// <param name="from">Valeur de départ.</param>
        /// <param name="to">Valeur d'arrivée.</param>
        /// <param name="t">Facteur d'interpolation (0 à 1).</param>
        public override void Interp(CurveValue from, CurveValue to, float t)
        {
            m_Value.Blend(from, to, t);
        }

        /// <summary>
        /// Copie la valeur d'un autre paramètre de volume.
        /// </summary>
        /// <param name="parameter">Le paramètre source à copier.</param>
        public override void SetValue(VolumeParameter parameter)
        {
            m_Value.SetValue(((VolumeParameter<CurveValue>)parameter).value);
        }
    }
}

#endif
