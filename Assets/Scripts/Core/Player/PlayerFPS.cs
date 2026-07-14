using System;
using Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Player
{
    [Serializable]
    public class PlayerFPS : Updatable<PlayerController>
    {
        [Header("Look Settings")]
        [SerializeField] private bool canLook = true;
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float minPitch = -89f;
        [SerializeField] private float maxPitch = 89f;
        [SerializeField] private float lookSmoothSpeed = 10f;

        [Header("Inputs Reference")]
        [SerializeField] private InputActionReference lookAction;

        [Header("Cursor Settings")]
        [SerializeField] private bool lockCursor = true;

        private float _rotationX = 0f;
        private Vector2 _smoothLookInput;
        private bool _skipFrame; // évite un saut à la réactivation du look

        /// <summary>Active ou désactive le contrôle de la caméra (look).</summary>
        public void SetActive(bool active, Transform cameraTransform = null)
        {
            if (canLook == active) return; // évite double appel inutile
            
            canLook = active;
            _smoothLookInput = Vector2.zero;
            
            if (active && cameraTransform != null)
            {
                // Resynchronise _rotationX sur l'angle réel de la caméra
                float pitch = cameraTransform.localRotation.eulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                _rotationX = Mathf.Clamp(pitch, minPitch, maxPitch);
                _skipFrame = true; // saute le premier frame pour éviter conflit Cinemachine
            }
            
            UpdateCursorState();
        }

        // =======================================================================

        public override void Start(PlayerController controller)
        {
            if (lookAction != null && lookAction.action != null)
            {
                lookAction.action.Enable();
            }

            UpdateCursorState();
        }

        public override void Update(PlayerController controller)
        {
            // Skip si on vient de réactiver (évite conflit avec Cinemachine/LookAt)
            if (_skipFrame)
            {
                _skipFrame = false;
                return;
            }
            
            UpdateCursorState();

            if (!canLook || Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 lookInput = Vector2.zero;

            if (lookAction != null && lookAction.action != null)
            {
                lookInput = lookAction.action.ReadValue<Vector2>();
            }
            else
            {
                if (Mouse.current != null)
                {
                    lookInput += Mouse.current.delta.ReadValue();
                }
                if (Gamepad.current != null)
                {
                    lookInput += Gamepad.current.rightStick.ReadValue() * 10f;
                }
            }

            _smoothLookInput = Vector2.Lerp(_smoothLookInput, lookInput, lookSmoothSpeed * Time.deltaTime);

            float mouseX = _smoothLookInput.x * mouseSensitivity;
            float mouseY = _smoothLookInput.y * mouseSensitivity;

            _rotationX -= mouseY;
            _rotationX = Mathf.Clamp(_rotationX, minPitch, maxPitch);

            if (controller.CameraTransform != null)
            {
                controller.CameraTransform.localRotation = Quaternion.Euler(_rotationX, 0f, 0f);
            }

            controller.transform.Rotate(Vector3.up * mouseX);
        }

        // =======================================================================

        private void UpdateCursorState()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
