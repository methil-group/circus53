using System;
using UnityEngine;

namespace Core
{
    /// <summary>Déclencheur d'un dialogue.</summary>
    public enum DialogTrigger
    {
        OnArrival,
        OnLook,
        Manual
    }

    /// <summary>Une ligne de dialogue / sous-titre.</summary>
    [Serializable]
    public class DialogLine
    {
        [TextArea(1, 5)]
        public string Text;

        public DialogTrigger Trigger = DialogTrigger.OnArrival;

        [Tooltip("Son optionnel (pas encore implémenté).")]
        public AudioClip Sound;

        /// <summary>True si ce dialogue a déjà été joué.</summary>
        [NonSerialized] public bool HasPlayed;
    }
}
