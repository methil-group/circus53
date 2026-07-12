/// <summary>
/// Fichier : Optional.cs
/// Wrapper générique Optionnel&lt;T&gt; avec un flag d'activation booléen.
/// Permet de rendre n'importe quel type sérialisable optionnel dans l'inspecteur Unity,
/// avec un toggle enable/disable géré par <see cref="VolFx.Editor.OptionalDrawer"/>.
/// </summary>
/// <typeparam name="T">Le type de la valeur à rendre optionnelle.</typeparam>
/// <remarks>
/// Fournit des opérateurs implicites vers <c>bool</c> et <c>T</c> pour une utilisation
/// transparente dans le code. Les méthodes <see cref="GetValue(T)"/> et <see cref="GetValueOrDefault()"/>
/// permettent de récupérer la valeur avec un fallback quand le flag est désactivé.
/// </remarks>
#if !VOL_FX

using System;
using UnityEngine;

namespace VolFx
{
    /// <summary>
    /// Enveloppe un type <typeparamref name="T"/> avec un booléen d'activation.
    /// </summary>
    [Serializable]
    public sealed class Optional<T>
    {
        /// <summary>Flag d'activation de la valeur.</summary>
        [SerializeField]
        internal bool enabled;

        /// <summary>Valeur enveloppée.</summary>
        [SerializeField]
        internal T value = default!;
    
        /// <summary>Accès public au flag d'activation.</summary>
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        /// <summary>Accès public à la valeur enveloppée.</summary>
        public T Value
        {
            get => value;
            set => this.value = value;
        }

        /// <summary>
        /// Constructeur avec flag uniquement (la valeur prend la valeur par défaut de T).
        /// </summary>
        /// <param name="enabled">État initial du flag.</param>
        public Optional(bool enabled)
        {
            this.enabled = enabled;
        }

        /// <summary>
        /// Constructeur complet.
        /// </summary>
        /// <param name="value">Valeur initiale.</param>
        /// <param name="enabled">État initial du flag.</param>
        public Optional(T value, bool enabled)
        {
            this.enabled = enabled;
            this.value   = value;
        }

        /// <summary>
        /// Retourne la valeur si activée, sinon la valeur désactivée fournie.
        /// </summary>
        /// <param name="disabledValue">Valeur de repli quand désactivé.</param>
        public T GetValue(T disabledValue)
        {
            return enabled ? value : disabledValue;
        }
        
        /// <summary>
        /// Retourne la valeur si activée, sinon <c>default(T)</c>.
        /// </summary>
        public T GetValueOrDefault()
        {
            return enabled ? value : default;
        }
        
        /// <summary>
        /// Retourne la valeur si activée, sinon le fallback fourni.
        /// </summary>
        /// <param name="fallback">Valeur de repli quand désactivé.</param>
        public T GetValueOrDefault(T fallback)
        {
            return enabled ? value : fallback;
        }
        
        /// <summary>Conversion implicite vers bool : retourne le flag d'activation.</summary>
        public static implicit operator bool(Optional<T> opt)
        {
            return opt.enabled;
        }

        /// <summary>Conversion implicite vers T : retourne la valeur enveloppée.</summary>
        public static implicit operator T(Optional<T> opt)
        {
            return opt.value;
        }
    }
}

#endif
