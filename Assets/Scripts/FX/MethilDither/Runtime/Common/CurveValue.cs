/// <summary>
/// Fichier : CurveValue.cs
/// Représente une valeur de courbe utilisée comme paramètre de shader.
/// Stocke à la fois une <see cref="AnimationCurve"/> et une version pré-calculée
/// en texture 1D de 32 pixels pour le sampling GPU.
/// </summary>
/// <remarks>
/// La largeur de texture est définie par la constante <see cref="k_Width"/> (32 pixels).
/// Cette valeur est partagée avec <see cref="GradientValue"/> et les drawers associés.
/// </remarks>
#if !VOL_FX

using System;
using UnityEngine;

namespace VolFx
{
    /// <summary>
    /// Valeur de courbe pour le sampling GPU.
    /// Convertit une <see cref="AnimationCurve"/> Unity en texture 1D de 32 pixels
    /// utilisable par les shaders via <c>sampler2D</c>.
    /// </summary>
    [Serializable]
    public class CurveValue
    {
        /// <summary>Nombre de pixels dans la texture (résolution d'échantillonnage).</summary>
        public const int k_Width = 32;
        
        /// <summary>La courbe source.</summary>
        public AnimationCurve _curve;
        /// <summary>Tableau de pixels pré-calculés (32 éléments, niveaux de gris).</summary>
        public Color[]        _pixels;
        /// <summary>Garde-fou contre la double initialisation.</summary>
        private bool          _build;

        /// <summary>
        /// Copie la valeur depuis une autre CurveValue.
        /// </summary>
        /// <param name="val">La valeur source.</param>
        internal void SetValue(CurveValue val)
        {
            if (val._build == false)
                val.Build();
            
            _curve = val._curve;
            val._pixels.CopyTo(_pixels, 0);
        }

        /// <summary>
        /// Interpole (mélange) deux CurveValues pixel par pixel.
        /// Utilise <see cref="Color.LerpUnclamped"/> pour permettre le dépassement.
        /// </summary>
        /// <param name="a">Première courbe.</param>
        /// <param name="b">Deuxième courbe.</param>
        /// <param name="t">Facteur de mélange.</param>
        public void Blend(CurveValue a, CurveValue b, float t)
        {
            for (var x = 0; x < k_Width; x++)
                _pixels[x] = Color.LerpUnclamped(a._pixels[x], b._pixels[x], t);
        }

        /// <summary>
        /// Crée une CurveValue à partir d'une AnimationCurve.
        /// Échantillonne la courbe en <see cref="k_Width"/> points et
        /// stocke le résultat en niveaux de gris.
        /// </summary>
        /// <param name="curve">La courbe d'animation source.</param>
        public CurveValue(AnimationCurve curve)
        {
            _curve  = curve; 
            _pixels = new Color[k_Width];
            for (var n = 0; n < k_Width; n++)
            {
                var val = _curve.Evaluate(n / (float)(k_Width - 1));
                _pixels[n] = new Color(val, val, val, val);
            }
        }
        
        /// <summary>
        /// Crée ou recycle une texture 1D à partir des pixels calculés.
        /// La texture est en format RGBA32, 32x1 pixels, avec WrapMode.Clamp
        /// et FilterMode.Bilinear pour un sampling lisse.
        /// </summary>
        /// <param name="tex">Référence à la texture à créer ou recycler.</param>
        /// <returns>La texture prête à être utilisée par le GPU.</returns>
        public Texture2D GetTexture(ref Texture2D tex)
        {
            if (tex == null)
            {
                tex = new Texture2D(k_Width, 1, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
            }
            
            tex.SetPixels(_pixels);
            tex.Apply();
            
            return tex;
        }
        
        /// <summary>
        /// Initialisation paresseuse : alloue le tableau de pixels
        /// et évalue la courbe si ce n'est pas déjà fait.
        /// </summary>
        public void Build()
        {
            if (_build)
                return;
            
            _build = true;
            
            _pixels = new Color[k_Width];
            for (var n = 0; n < k_Width; n++)
            {
                var val = _curve.Evaluate(n / (float)(k_Width - 1));
                _pixels[n] = new Color(val, val, val, val);
            }
        }
    }
}

#endif
