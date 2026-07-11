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
        [SerializeField] private float lookSmoothSpeed = 10f; // Speed of look smoothing (lower is slower/floatier)

        [Header("Inputs Reference")]
        [SerializeField] private InputActionReference lookAction;

        [Header("Cursor Settings")]
        [SerializeField] private bool lockCursor = true;

        private float _rotationX = 0f;
        private Vector2 _smoothLookInput;

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
            // Keep cursor state in sync (allows runtime toggling in the editor)
            UpdateCursorState();

            // Skip camera rotation if look is disabled
            if (!canLook) return;

            Vector2 lookInput = Vector2.zero;

            // 1. Try to read from assigned InputActionReference
            if (lookAction != null && lookAction.action != null)
            {
                lookInput = lookAction.action.ReadValue<Vector2>();
            }
            // 2. Fallback to direct mouse/gamepad delta
            else
            {
                if (Mouse.current != null)
                {
                    lookInput += Mouse.current.delta.ReadValue();
                }
                if (Gamepad.current != null)
                {
                    lookInput += Gamepad.current.rightStick.ReadValue() * 10f; // Scale gamepad stick speed
                }
            }

            // Lerp look input for smooth/dreamy camera lag
            _smoothLookInput = Vector2.Lerp(_smoothLookInput, lookInput, lookSmoothSpeed * Time.deltaTime);

            // Scale by sensitivity
            float mouseX = _smoothLookInput.x * mouseSensitivity;
            float mouseY = _smoothLookInput.y * mouseSensitivity;

            // Rotate camera vertically (Pitch)
            _rotationX -= mouseY;
            _rotationX = Mathf.Clamp(_rotationX, minPitch, maxPitch);

            if (controller.CameraTransform != null)
            {
                controller.CameraTransform.localRotation = Quaternion.Euler(_rotationX, 0f, 0f);
            }

            // Rotate player body horizontally (Yaw)
            controller.transform.Rotate(Vector3.up * mouseX);
        }

        private void UpdateCursorState()
        {
            if (canLook && lockCursor)
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
