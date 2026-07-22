using System;
using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    /// <summary>
    /// Événement déclenché après un certain temps passé sur un Place.
    /// </summary>
    [Serializable]
    public class PlaceEvent
    {
        [Tooltip("Délai en secondes avant de déclencher l'événement.")]
        public float Delay = 3f;

        [Tooltip("Se déclenche une seule fois ou à chaque visite.")]
        public bool Once = true;

        [Tooltip("Si renseigné, ce dialogue est joué avant le UnityEvent.")]
        public DialogLine Dialog;

        [Tooltip("L'événement à lancer (peut appeler une animation, un son, etc.).")]
        public UnityEvent OnTrigger;

        [NonSerialized] public bool HasTriggered;
    }
}
