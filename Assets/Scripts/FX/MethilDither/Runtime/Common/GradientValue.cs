/// <summary>
/// Fichier : GradientValue.cs
/// Représente une valeur de dégradé utilisée comme paramètre de shader.
/// Stocke à la fois un <see cref="Gradient"/> Unity et une version pré-calculée
/// en texture 1D de 32 pixels pour le sampling GPU.
/// </summary>
/// <remarks>
/// La largeur de texture est définie par la constante <see cref="k_Width"/> (32 pixels),
/// partagée avec <see cref="CurveValue"/>. La propriété statique <see cref="White"/>
/// fournit un dégradé blanc par défaut pratique.
/// </remarks>
#if !VOL_FX

using System;
using UnityEngine;

namespace VolFx
{
    /// <summary>
    /// Valeur de dégradé pour le sampling GPU.
    /// Convertit un <see cref="Gradient"/> Unity en texture 1D de 32 pixels
    /// utilisable par les shaders.
    /// </summary>
    [Serializable]
    public class GradientValue
    {
        /// <summary>Nombre de pixels dans la texture (résolution d'échantillonnage).</summary>
        public const int k_Width = 32;
        
        /// <summary>Le dégradé source.</summary>
        public Gradient _grad;
        /// <summary>Tableau de pixels pré-calculés (32 éléments).</summary>
        public Color[]  _pixels;

        /// <summary>Garde-fou contre la double initialisation.</summary>
        internal bool _build;
        
        /// <summary>
        /// Dégradé blanc par défaut, pratique comme valeur initiale.
        /// </summary>
        public static GradientValue White
        {
            get
            {
                var grad = new Gradient();
                grad.SetKeys(new []{new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f)}, new GradientAlphaKey[]{new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0f)});
                
                return new GradientValue(grad);
            }
        }
        
        /// <summary>
        /// Initialise le dégradé et pré-calcule le tableau de pixels.
        /// </summary>
        /// <param name="_mode">Mode d'interpolation du dégradé (Blend ou Fixed).</param>
        public void Build(GradientMode _mode)
        {
            _build = true;
            
            _grad.mode = _mode;
            _pixels    = new Color[k_Width];
            
            for (var x = 0; x < k_Width; x++)
                _pixels[x] = _grad.Evaluate(x / (float)(k_Width - 1));   
        }
        
        /// <summary>
        /// Copie la valeur depuis une autre GradientValue.
        /// </summary>
        /// <param name="val">La valeur source.</param>
        internal void SetValue(GradientValue val)
        {
            if (val._build == false)
                val.Build(val._grad.mode);
            
            _grad = val._grad;
            val._pixels.CopyTo(_pixels, 0);
        }
        
        /// <summary>
        /// Interpole (mélange) deux GradientValues pixel par pixel.
        /// Le mode du dégradé résultant est celui du côté le plus proche (a si t inférieur à 0.5, b sinon).
        /// </summary>
        /// <param name="a">Premier dégradé.</param>
        /// <param name="b">Deuxième dégradé.</param>
        /// <param name="t">Facteur de mélange.</param>
        public void Blend(GradientValue a, GradientValue b, float t)
        {
            _build = true;
            
            for (var x = 0; x < k_Width; x++)
                _pixels[x] = Color.LerpUnclamped(a._pixels[x], b._pixels[x], t);
            
            _grad.mode = t < .5f ? a._grad.mode : b._grad.mode;
        }
        
        /// <summary>
        /// Crée ou recycle une texture 1D à partir des pixels calculés.
        /// Texture RGBA32, 32x1 pixels, WrapMode.Clamp, FilterMode.Bilinear.
        /// </summary>
        /// <param name="tex">Référence à la texture à créer ou recycler.</param>
        /// <returns>La texture prête à être utilisée par le GPU.</returns>
        public Texture2D GetTexture(ref Texture2D tex)
        {
            if (tex == null)
            {
                tex = new Texture2D(GradientValue.k_Width, 1, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
            }
            
            tex.SetPixels(_pixels);
            tex.Apply();
            
            return tex;
        }

        /// <summary>
        /// Crée une GradientValue à partir d'un Gradient.
        /// Échantillonne le dégradé en <see cref="k_Width"/> points.
        /// </summary>
        /// <param name="grad">Le dégradé source.</param>
        public GradientValue(Gradient grad)
        {
            _grad   = grad; 
            _pixels = new Color[k_Width];
            for (var n = 0; n < k_Width; n++)
                _pixels[n] = grad.Evaluate(n / (float)(k_Width - 1));
        }
    }
}

#endif
