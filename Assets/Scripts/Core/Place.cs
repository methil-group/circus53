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

        public IEnumerable<GameObject> ObjectsToActivate => _objectsToActivate;
        public Place FrontPlace => _frontPlace;
        public Place BackPlace => _backPlace;
        public Place LeftPlace => _leftPlace;
        public Place RightPlace => _rightPlace;

        /// <summary>Position cible pour le joueur (venant du CameraClickPoint).</summary>
        public Vector3 TargetPosition => transform.position;

        /// <summary>Rotation de cadrage pour la caméra.</summary>
        public Quaternion TargetRotation
        {
            get
            {
                CameraClickPoint point = GetComponent<CameraClickPoint>();
                return point != null ? point.GetViewRotation() : transform.rotation;
            }
        }

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
    }
}
