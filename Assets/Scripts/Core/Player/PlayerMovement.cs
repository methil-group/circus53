using System;
using Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Core.Player
{
    [Serializable]
    public class PlayerMovement : Updatable<PlayerController>
    {
        [Header("Movement Settings")]
        [SerializeField] private bool canMove = true;
        [SerializeField] private float speed = 5f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float movementSmoothTime = 4f; // Controls the slide/inertia speed

        [Header("Inputs Reference")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;

        // Events
        public UnityAction OnJump;
        public UnityAction OnMove;

        private Vector3 _velocity;
        private Vector3 _currentMoveVelocity;
        private bool _isGrounded;

        public override void Start(PlayerController controller)
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Enable();
            }
            if (jumpAction != null && jumpAction.action != null)
            {
                jumpAction.action.Enable();
            }
        }

        public override void Update(PlayerController controller)
        {
            var charController = controller.CharacterController;
            if (charController == null) return;

            // Ground check
            _isGrounded = charController.isGrounded;
            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            // Skip input processing if movement is disabled (gravity still applies)
            if (!canMove)
            {
                _currentMoveVelocity = Vector3.Lerp(_currentMoveVelocity, Vector3.zero, movementSmoothTime * Time.deltaTime);
                charController.Move(_currentMoveVelocity * Time.deltaTime);

                _velocity.y += gravity * Time.deltaTime;
                charController.Move(_velocity * Time.deltaTime);
                return;
            }

            // Input System reading
            Vector2 moveInput = Vector2.zero;
            bool jumpPressed = false;

            // 1. Try to read from assigned InputActionReferences
            if (moveAction != null && moveAction.action != null)
            {
                moveInput = moveAction.action.ReadValue<Vector2>();
            }
            if (jumpAction != null && jumpAction.action != null)
            {
                jumpPressed = jumpAction.action.triggered;
            }

            // 2. Fallback to direct hardware queries if InputActionReferences are not set
            if (moveAction == null || moveAction.action == null)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveInput.y += 1f;
                    if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveInput.y -= 1f;
                    if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput.x -= 1f;
                    if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput.x += 1f;
                }

                if (Gamepad.current != null)
                {
                    moveInput += Gamepad.current.leftStick.ReadValue();
                }
            }

            if (jumpAction == null || jumpAction.action == null)
            {
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    jumpPressed = true;
                }

                if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    jumpPressed = true;
                }
            }

            // Calculate movement direction relative to camera
            Vector3 move = Vector3.zero;
            if (controller.CameraTransform != null)
            {
                Vector3 forward = controller.CameraTransform.forward;
                Vector3 right = controller.CameraTransform.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
                move = forward * moveInput.y + right * moveInput.x;
            }
            else
            {
                move = new Vector3(moveInput.x, 0f, moveInput.y);
            }

            // Lerp movement input for dreamy slide/inertia
            _currentMoveVelocity = Vector3.Lerp(_currentMoveVelocity, move * speed, movementSmoothTime * Time.deltaTime);
            charController.Move(_currentMoveVelocity * Time.deltaTime);

            if (_currentMoveVelocity.sqrMagnitude > 0.0001f)
            {
                OnMove?.Invoke();
            }

            // Jump
            if (jumpPressed && _isGrounded)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                OnJump?.Invoke();
            }

            // Gravity
            _velocity.y += gravity * Time.deltaTime;
            charController.Move(_velocity * Time.deltaTime);
        }
    }
}
