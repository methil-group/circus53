using System;
using UnityEngine;

namespace Core
{
    /// <summary>Un endroit du circus avec ses voisins et sa direction de regard.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CameraClickPoint))]
    public class Place : MonoBehaviour
    {
        [Header("Voisins (navigation nodale)")]
        [SerializeField] private Place _frontPlace;
        [SerializeField] private Place _backPlace;
        [SerializeField] private Place _leftPlace;
        [SerializeField] private Place _rightPlace;

        [Header("Orientation")]
        [SerializeField, Tooltip("Direction dans laquelle le joueur regarde quand il est sur ce Place.")]
        private Vector3 _lookDirection = Vector3.forward;

        [Header("Dialogues")]
        [SerializeField] private DialogLine[] _dialogues = Array.Empty<DialogLine>();

        [Header("Ambiance")]
        [SerializeField, Tooltip("Son d'ambiance joué en boucle tant que le joueur est sur ce Place.")]
        private AudioClip _ambientSound;

        [SerializeField, Tooltip("Anxiété ajoutée à l'arrivée sur ce Place.")]
        private float _arrivalAnxietyIncrease;

        public Place FrontPlace => _frontPlace;
        public Place BackPlace => _backPlace;
        public Place LeftPlace => _leftPlace;
        public Place RightPlace => _rightPlace;
        [Header("Événements temporels")]
        [SerializeField] private PlaceEvent[] _events = Array.Empty<PlaceEvent>();

        public DialogLine[] Dialogues => _dialogues;
        public PlaceEvent[] Events => _events;
        public AudioClip AmbientSound => _ambientSound;
        public float ArrivalAnxietyIncrease => _arrivalAnxietyIncrease;

        /// <summary>Position cible pour le joueur (venant du CameraClickPoint).</summary>
        public Vector3 TargetPosition => transform.position;

        /// <summary>Rotation correspondant à la direction de regard.</summary>
        public Quaternion LookRotation => Quaternion.LookRotation(_lookDirection.normalized, Vector3.up);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_lookDirection.sqrMagnitude < 0.001f) return;

            Vector3 origin = transform.position;
            Vector3 dir = _lookDirection.normalized;
            float arrowLength = 1.5f;
            float headSize = 0.3f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, dir * arrowLength);

            // Pointe de flèche
            Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 180f + 30f, 0f) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 180f - 30f, 0f) * Vector3.forward;
            Vector3 tip = origin + dir * arrowLength;
            Gizmos.DrawRay(tip, right * headSize);
            Gizmos.DrawRay(tip, left * headSize);
        }
#endif
    }
}
