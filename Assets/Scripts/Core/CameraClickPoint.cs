using Core.Player;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Point de vue cliquable pour le déplacement point & click.
    /// À placer sur une caméra de blocking : le joueur est instantanément
    /// positionné et orienté comme cette caméra.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class CameraClickPoint : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _clickAreaSize = 0.05f;
        [SerializeField, Min(1f)] private float _screenClickRadius = 96f;
        [SerializeField, Tooltip("Les caméras importées du FBX regardent dans l'axe inverse de Unity.")]
        private bool _flipForward = true;
        [SerializeField] private Color _gizmoColor = new(1f, 0.55f, 0f, 0.75f);

        public void MovePlayerTo(PlayerController player)
        {
            if (player == null || player.CameraTransform == null) return;

            Transform playerRoot = player.transform;
            Transform playerCamera = player.CameraTransform;
            CharacterController controller = player.CharacterController;
            bool controllerWasEnabled = controller != null && controller.enabled;

            if (controllerWasEnabled) controller.enabled = false;

            AlignPlayerView(player);

            // Puis compense l'offset entre le root et la caméra (hauteur du joueur,
            // CameraHolder, head bob éventuel) pour aligner la caméra au pixel près.
            playerRoot.position += transform.position - playerCamera.position;

            if (controllerWasEnabled) controller.enabled = true;

        }

        /// <summary>Retourne le delta de rotation à appliquer au root du Player pour cadrer la caméra.</summary>
        public Quaternion GetViewRotationDelta(PlayerController player)
        {
            if (player == null || player.CameraTransform == null) return Quaternion.identity;

            Quaternion targetRotation = transform.rotation;
            if (_flipForward)
                targetRotation *= Quaternion.Euler(0f, 180f, 0f);

            Transform playerCamera = player.CameraTransform;
            return targetRotation * Quaternion.Inverse(playerCamera.rotation);
        }

        /// <summary>Applique le cadrage instantanément (téléportation, conservé pour compatibilité).</summary>
        public void AlignPlayerView(PlayerController player)
        {
            if (player == null || player.CameraTransform == null) return;

            Quaternion rotationDelta = GetViewRotationDelta(player);
            player.transform.rotation = rotationDelta * player.transform.rotation;

            SelectPlace();
        }

        /// <summary>Active le Place correspondant dans le CircusManager.</summary>
        public void SelectPlace()
        {
            CircusManager manager = GetComponentInParent<CircusManager>();
            manager?.SelectPlace(GetComponent<Place>());
        }

        public void SetClickAreaSize(float size)
        {
            _clickAreaSize = Mathf.Max(0.01f, size);
            ConfigureCollider();
        }

        public float ScreenClickRadius => _screenClickRadius;

        private void Awake()
        {
            // Les caméras du FBX ne servent que de repères de point de vue.
            // La seule caméra qui rend le jeu est la Main Camera du Player.
            if (!Application.isPlaying) return;

            UnityEngine.Camera blockingCamera = GetComponent<UnityEngine.Camera>();
            if (blockingCamera != null)
                blockingCamera.enabled = false;
        }

        private void Reset() => ConfigureCollider();

        private void OnValidate() => ConfigureCollider();

        private void ConfigureCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = Vector3.one * _clickAreaSize;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * _clickAreaSize);

            // Direction du plan de caméra pour rendre le point de vue lisible.
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * (_clickAreaSize * 1.25f));
            Gizmos.DrawWireSphere(Vector3.forward * (_clickAreaSize * 1.25f), _clickAreaSize * 0.12f);
        }
    }
}
