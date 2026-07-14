using System.Linq;
using Framework.Controller;
using UnityEngine;

namespace Core.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : UpdatableController<PlayerController>
    {
        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform cameraTransform;

        public CharacterController CharacterController => characterController;
        public Transform CameraTransform => cameraTransform;

        // =======================================================================
        
        /// <summary>Active ou désactive le déplacement du joueur.</summary>
        public void ActivateMove(bool active)
        {
            var movement = updatables.OfType<PlayerMovement>().FirstOrDefault();
            movement?.SetActive(active);
        }
        
        /// <summary>Active ou désactive le contrôle de la caméra (look).</summary>
        public void ActivateLook(bool active)
        {
            var fps = updatables.OfType<PlayerFPS>().FirstOrDefault();
            fps?.SetActive(active, CameraTransform);
        }
        
        // =======================================================================

        protected override void Awake()
        {
            base.Awake();
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }
    }
}
