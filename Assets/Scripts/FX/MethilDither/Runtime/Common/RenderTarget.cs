/// <summary>
/// Fichier : RenderTarget.cs
/// Wrapper autour du système <see cref="Rendering.RTHandle"/> d'Unity URP
/// pour la gestion simplifiée des render textures temporaires.
/// </summary>
/// <remarks>
/// Combine un <see cref="RTHandle"/> (allocation persistante) et un ID de propriété shader.
/// Supporte deux modes :
/// <list type="bullet">
///   <item><b>Allocation persistante</b> via <see cref="RTHandles.Alloc"/> — pour les textures permanentes.</item>
///   <item><b>Allocation temporaire</b> via <see cref="CommandBuffer.GetTemporaryRT"/> — pour les textures frame-local.</item>
/// </list>
/// L'opérateur implicite vers <see cref="RTHandle"/> permet de l'utiliser directement
/// dans les APIs Unity qui attendent un RTHandle.
/// </remarks>
#if !VOL_FX

using UnityEngine;
using UnityEngine.Rendering;

namespace VolFx
{
    /// <summary>
    /// Gestionnaire de render texture avec RTHandle et ID shader.
    /// </summary>
    public class RenderTarget
    {
        /// <summary>Handle de la render texture (allocation URP).</summary>
        public  RTHandle Handle;
        /// <summary>ID de propriété shader associé.</summary>
        public  int      Id;
        /// <summary>Flag indiquant si une RT temporaire a été allouée ce frame.</summary>
        private bool     _allocated;

        /// <summary>
        /// Alloue une render texture persistante à partir d'une RenderTexture existante.
        /// </summary>
        /// <param name="rt">La RenderTexture à wrapper.</param>
        /// <param name="name">Nom utilisé pour l'ID shader et le handle.</param>
        /// <returns>Ce RenderTarget (fluent API).</returns>
        public RenderTarget Allocate(RenderTexture rt, string name)
        {
            Handle = RTHandles.Alloc(rt, name);
            Id     = Shader.PropertyToID(name);
            return this;
        }

        /// <summary>
        /// Alloue une render texture persistante par nom.
        /// </summary>
        /// <param name="name">Nom utilisé pour l'ID shader et le handle.</param>
        /// <returns>Ce RenderTarget (fluent API).</returns>
        public RenderTarget Allocate(string name)
        {
            Handle = RTHandles.Alloc(name, name: name);
            Id     = Shader.PropertyToID(name);
            return this;
        }

        /// <summary>
        /// Alloue une render texture temporaire pour le frame en cours.
        /// </summary>
        /// <param name="cmd">CommandBuffer actif.</param>
        /// <param name="desc">Descripteur de la texture.</param>
        public void Get(CommandBuffer cmd, in RenderTextureDescriptor desc)
        {
            _allocated = true;
            cmd.GetTemporaryRT(Id, desc);
        }
        
        /// <summary>
        /// Alloue une render texture temporaire avec un mode de filtrage spécifique.
        /// </summary>
        /// <param name="cmd">CommandBuffer actif.</param>
        /// <param name="desc">Descripteur de la texture.</param>
        /// <param name="filter">Mode de filtrage (Point, Bilinear, Trilinear).</param>
        public void Get(CommandBuffer cmd, in RenderTextureDescriptor desc, FilterMode filter)
        {
            _allocated = true;
            cmd.GetTemporaryRT(Id, desc, filter);
        }

        /// <summary>
        /// Libère la render texture temporaire si elle a été allouée ce frame.
        /// Sans effet si aucune allocation temporaire n'a eu lieu.
        /// </summary>
        /// <param name="cmd">CommandBuffer actif.</param>
        public void Release(CommandBuffer cmd)
        {
            if (_allocated == false) 
                return;
            
            _allocated = false;
            cmd.ReleaseTemporaryRT(Id);
        }
        
        /// <summary>Conversion implicite vers RTHandle pour utilisation directe dans les APIs Unity.</summary>
        public static implicit operator RTHandle(RenderTarget rt) => rt.Handle;
    }
}

#endif
