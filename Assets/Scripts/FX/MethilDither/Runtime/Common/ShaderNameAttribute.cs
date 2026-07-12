/// <summary>
/// Fichier : ShaderNameAttribute.cs
/// Attribut personnalisé pour lier une classe de passe de rendu à son shader.
/// </summary>
/// <remarks>
/// <para>
/// Utilisé par <see cref="VolFx.Pass._init"/> et <see cref="VolFx.Pass.Validate"/>
/// pour localiser automatiquement le shader associé à une passe via <see cref="Shader.Find"/>.
/// </para>
/// <para>
/// Restrictions d'usage :
/// <list type="bullet">
///   <item>Uniquement sur les classes (<see cref="AttributeTargets.Class"/>).</item>
///   <item>Une seule fois par classe (<see cref="AllowMultiple"/> = false).</item>
///   <item>Hérité par les sous-classes (<see cref="Inherited"/> = true).</item>
/// </list>
/// </para>
/// <para>
/// Exemple : <c>[ShaderName("Hidden/VolFx/MethilDither")]</c> sur <c>MethilDitherPass</c>
/// permet de trouver automatiquement le shader "Hidden/VolFx/MethilDither".
/// </para>
/// </remarks>
#if !VOL_FX

using System;

namespace VolFx
{
    /// <summary>
    /// Attribut qui associe une classe de passe à un chemin de shader.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ShaderNameAttribute : Attribute
    {
        /// <summary>Chemin/nom du shader (ex: "Hidden/VolFx/MethilDither").</summary>
        public string _name;
            
        /// <summary>
        /// Constructeur.
        /// </summary>
        /// <param name="name">Le chemin du shader tel que passé à <see cref="Shader.Find"/>.</param>
        public ShaderNameAttribute(string name)
        {
            _name = name;
        }
    }
}

#endif
