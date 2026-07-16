using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    /// <summary>
    /// Zone cliquable pour le système de point & click.
    /// Se place sur tout GameObject avec un Collider.
    /// Le joueur peut cliquer dessus pour s'y déplacer automatiquement.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ClickableZone : MonoBehaviour
    {
        [Header("Destination")]
        [SerializeField, Tooltip("Position exacte où le joueur s'arrêtera. Si null, utilise la position de ce GameObject.")]
        private Transform _targetPosition;

        [SerializeField, Tooltip("Point que la caméra regardera une fois arrivé. Si null, regarde vers l'avant.")]
        private Transform _lookAtTarget;

        [Header("Interaction")]
        [SerializeField, Tooltip("Événement déclenché quand le joueur arrive sur cette zone.")]
        private UnityEvent _onArrival;

        [SerializeField, Tooltip("Santé mentale drainée en arrivant sur cette zone (0 = pas de drain).")]
        private float _sanityDrain;

        [Header("Curseur")]
        [SerializeField, Tooltip("Icône du curseur au survol de cette zone. Null = garde le curseur par défaut.")]
        private Texture2D _cursorIcon;

        [SerializeField, Tooltip("Point de pivot du curseur (hotspot). Par défaut = coin supérieur gauche.")]
        private Vector2 _cursorHotspot = Vector2.zero;

        // =======================================================================

        /// <summary>Position vers laquelle le joueur se déplace.</summary>
        public Vector3 TargetPosition => _targetPosition != null ? _targetPosition.position : transform.position;

        /// <summary>Point vers lequel la caméra s'oriente à l'arrivée.</summary>
        public Transform LookAtTarget => _lookAtTarget;

        /// <summary>Événement à l'arrivée.</summary>
        public UnityEvent OnArrival => _onArrival;

        /// <summary>Quantité de santé mentale drainée à l'arrivée.</summary>
        public float SanityDrain => _sanityDrain;

        /// <summary>Icône du curseur au survol (null = pas de changement).</summary>
        public Texture2D CursorIcon => _cursorIcon;

        /// <summary>Hotspot du curseur.</summary>
        public Vector2 CursorHotspot => _cursorHotspot;

        // =======================================================================

        private void Reset()
        {
            // S'assure qu'il y a un collider en mode trigger
            // (trigger = le raycast le détecte, mais il ne bloque pas le CharacterController)
            var col = GetComponent<Collider>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }
            else
            {
                col.isTrigger = true;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 pos = TargetPosition;

            // Cercle au sol pour la destination
            Gizmos.color = new Color(0f, 1f, 0.8f, 0.5f);
            Gizmos.DrawSphere(pos, 0.2f);

            // Flèche vers le lookAt
            if (_lookAtTarget != null)
            {
                Gizmos.color = new Color(0f, 0.7f, 1f, 0.6f);
                Gizmos.DrawLine(pos, _lookAtTarget.position);
                Gizmos.DrawSphere(_lookAtTarget.position, 0.15f);
            }

            // Wireframe du collider
            var col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                if (col is BoxCollider box)
                {
                    Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
            }
        }
    }
}
