using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>Un endroit cliquable du circus et l'état de décor associé.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CameraClickPoint))]
    public class Place : MonoBehaviour
    {
        [Header("Décor")]
        [SerializeField] private GameObject[] _objectsToActivate = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _objectsToDeactivate = System.Array.Empty<GameObject>();

        [Header("Voisins (navigation nodale)")]
        [SerializeField] private Place _frontPlace;
        [SerializeField] private Place _backPlace;
        [SerializeField] private Place _leftPlace;
        [SerializeField] private Place _rightPlace;

        [Header("Orientation")]
        [SerializeField, Tooltip("Direction dans laquelle le joueur regarde quand il est sur ce Place.")]
        private Vector3 _lookDirection = Vector3.forward;

        public IEnumerable<GameObject> ObjectsToActivate => _objectsToActivate;
        public Place FrontPlace => _frontPlace;
        public Place BackPlace => _backPlace;
        public Place LeftPlace => _leftPlace;
        public Place RightPlace => _rightPlace;

        /// <summary>Position cible pour le joueur (venant du CameraClickPoint).</summary>
        public Vector3 TargetPosition => transform.position;

        /// <summary>Rotation de cadrage pour la caméra (depuis CameraClickPoint).</summary>
        public Quaternion TargetRotation
        {
            get
            {
                CameraClickPoint point = GetComponent<CameraClickPoint>();
                return point != null ? point.GetViewRotation() : transform.rotation;
            }
        }

        /// <summary>Rotation correspondant à la direction de regard.</summary>
        public Quaternion LookRotation => Quaternion.LookRotation(_lookDirection.normalized, Vector3.up);

        public void Apply()
        {
            SetActive(_objectsToActivate, true);
            SetActive(_objectsToDeactivate, false);
        }

        private static void SetActive(IEnumerable<GameObject> objects, bool active)
        {
            foreach (GameObject gameObject in objects)
            {
                if (gameObject != null)
                    gameObject.SetActive(active);
            }
        }

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
