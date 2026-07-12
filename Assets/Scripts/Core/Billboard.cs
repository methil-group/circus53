using UnityEngine;

namespace Core
{
    /// <summary>
    /// Fait pivoter l'objet pour qu'il regarde toujours la caméra,
    /// en rotation horizontale uniquement (axe Y). L'objet reste droit,
    /// sans jamais pencher sur les axes X ou Z.
    /// 
    /// Comportement typique :
    /// - Ennemis ou sprites 2D dans un monde 3D qui font toujours face au joueur
    /// - Panneaux, pancartes, textures planes qui suivent la caméra horizontalement
    /// 
    /// Utilisation : attacher ce script à n'importe quel GameObject.
    /// Par défaut, l'axe forward local (-Z, ou +Z selon le flip) pointera vers la caméra.
    /// </summary>
    [DisallowMultipleComponent]
    public class Billboard : MonoBehaviour
    {
        // =======================================================================
        
        [Header("Référence")]
        [SerializeField, Tooltip("Caméra à regarder. Null = Camera.main (auto-détectée).")]
        private Camera _targetCamera;
        
        [Header("Comportement")]
        [SerializeField, Tooltip("Inverse le sens : l'objet tourne le dos à la caméra au lieu de lui faire face.")]
        private bool _flipDirection;
        
        [SerializeField, Tooltip("Si coché, le billboard est mis à jour uniquement via l'appel manuel Refresh().\n" +
                                 "Utile pour les objets statiques qui ne changent jamais de position.")]
        private bool _manualRefresh;
        
        // =======================================================================
        // Cache
        
        private Transform _cachedTransform;
        
        // =======================================================================
        
        private void Awake()
        {
            _cachedTransform = transform;
            
            if (_targetCamera == null)
                _targetCamera = Camera.main;
        }
        
        private void Start()
        {
            // Premier alignement immédiat
            Refresh();
        }
        
        private void LateUpdate()
        {
            if (_manualRefresh)
                return;
            
            Refresh();
        }
        
        // =======================================================================
        
        /// <summary>
        /// Force l'alignement immédiat vers la caméra.
        /// Appeler ceci si <see cref="_manualRefresh"/> est true,
        /// ou après avoir déplacé l'objet manuellement.
        /// </summary>
        [ContextMenu("Refresh")]
        public void Refresh()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
                if (_targetCamera == null)
                    return;
            }
            
            // Direction horizontale vers la caméra (Y = 0 → rotation Y uniquement)
            Vector3 camPosition = _targetCamera.transform.position;
            Vector3 flatDirection = camPosition - _cachedTransform.position;
            flatDirection.y = 0f;
            
            // Si l'objet est exactement au-dessus/en-dessous de la caméra → pas de rotation
            if (flatDirection.sqrMagnitude < 0.0001f)
                return;
            
            // Applique la rotation Y-only
            Quaternion targetRotation = Quaternion.LookRotation(
                _flipDirection ? -flatDirection : flatDirection,
                Vector3.up
            );
            
            _cachedTransform.rotation = targetRotation;
        }
        
        // =======================================================================
        
        /// <summary>
        /// Définit la caméra à regarder.
        /// </summary>
        public void SetCamera(Camera cam)
        {
            _targetCamera = cam;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
            
            // Pas de Refresh() automatique en mode édition pour éviter les modifications non sauvegardées
        }
        
        private void OnDrawGizmosSelected()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
            
            Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
            if (cam == null) return;
            
            // Visualisation de la direction de face
            Vector3 flat = (cam.transform.position - _cachedTransform.position).normalized;
            flat.y = 0f;
            flat = flat.normalized;
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(_cachedTransform.position, flat * 1.5f);
            
            // Petit marqueur de l'axe Y (pour montrer que l'objet reste droit)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(_cachedTransform.position, Vector3.up * 0.5f);
        }
#endif
    }
}
