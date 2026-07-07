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
