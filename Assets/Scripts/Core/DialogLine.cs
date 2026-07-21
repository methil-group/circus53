using System;
using UnityEngine;

namespace Core
{
    public enum DialogTrigger
    {
        OnArrival,
        OnLook,
        Manual
    }

    public enum DialogReveal
    {
        Instant,
        CharByChar,
        WordByWord
    }

    [Serializable]
    public class DialogLine
    {
        [TextArea(1, 5)]
        public string Text;

        public DialogTrigger Trigger = DialogTrigger.OnArrival;

        [Header("Timing")]
        [Tooltip("Délai avant l'apparition du texte (secondes).")]
        public float Delay;

        [Tooltip("Mode d'apparition du texte.")]
        public DialogReveal Reveal = DialogReveal.Instant;

        [Tooltip("Vitesse d'apparition (secondes par caractère ou par mot).")]
        public float RevealSpeed = 0.03f;

        [Tooltip("Durée d'affichage une fois le texte complet (0 = reste affiché).")]
        public float DisplayDuration = 4f;

        [Tooltip("Bloque la navigation tant que le dialogue est visible.")]
        public bool BlockMovement = true;

        [Header("Sound")]
        [Tooltip("Son optionnel (pas encore implémenté).")]
        public AudioClip Sound;

        [NonSerialized] public bool HasPlayed;
    }
}
