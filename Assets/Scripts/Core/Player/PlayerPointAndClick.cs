using System;
using Framework;
using Framework.Camera;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Player
{
    /// <summary>
    /// Système de déplacement point & click.
    /// Remplace PlayerMovement : le joueur clique sur une ClickableZone,
    /// tourne la tête vers la cible, puis marche jusqu'à elle.
    /// Head bob pendant la marche.
    /// </summary>
    [Serializable]
    public class PlayerPointAndClick : Updatable<PlayerController>
    {
        private enum State { Idle, Looking, Walking }

        [Header("Movement")]
        [SerializeField] private bool canMove = true;
        [SerializeField] private float speed = 4f;
        [SerializeField] private float arrivalThreshold = 1.5f;

        [Header("Look Before Walk")]
        [SerializeField] private bool lookBeforeWalk = true;
        [SerializeField, Tooltip("Durée du regard vers la cible avant de commencer à marcher.")]
        private float lookDuration = 0.4f;

        [Header("Rotation During Walk")]
        [SerializeField] private float rotationSpeed = 360f;

        [Header("Look At On Arrival")]
        [SerializeField] private float lookAtDuration = 0.8f;

        [Header("Head Bob")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField, Tooltip("Transform qui reçoit le bob (ex: CameraHolder). Si null, utilise le parent de la caméra.")]
        private Transform _bobTransform;
        [SerializeField] private float _bobAmount = 0.03f;
        [SerializeField] private float _bobFrequency = 3f;
        [SerializeField] private AnimationCurve _bobCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float _bobSmoothSpeed = 8f;

        [Header("Stuck Detection")]
        [SerializeField] private float stuckTimeout = 1.5f;
        [SerializeField] private float stuckDistanceDelta = 0.05f;

        [Header("Inputs (optional — fallback to Mouse.current)")]
        [SerializeField] private InputActionReference pointAction;
        [SerializeField] private InputActionReference clickAction;

        [Header("Cursor")]
        [SerializeField] private Texture2D defaultCursor;
        [SerializeField] private Vector2 defaultCursorHotspot = Vector2.zero;

        // =======================================================================

        private CharacterController _characterController;
        private Transform _cameraTransform;
        private Transform _bobTarget;
        private MethilCamera _methilCamera;

        private State _state = State.Idle;
        private ClickableZone _currentTarget;
        private ClickableZone _hoveredZone;
        private Vector3 _targetPosition;
        private bool _isMoving;

        // Look-before-walk
        private float _lookTimer;

        // Stuck detection
        private float _stuckTimer;
        private float _lastStuckDistance;
        private bool _stuckInitialized;

        // Head bob
        private float _bobPhase;
        private Vector3 _currentBobOffset;
        private Vector3 _defaultBobPosition;
        private bool _hasDefaultBobPos;

        // Debug
        private float _debugNextLogTime;

        /// <summary>Active ou désactive le déplacement point & click.</summary>
        public void SetActive(bool active) => canMove = active;

        /// <summary>True si le joueur est en train de se déplacer vers une zone.</summary>
        public bool IsMoving => _isMoving;

        /// <summary>La zone actuellement survolée (null si aucune).</summary>
        public ClickableZone HoveredZone => _hoveredZone;

        // =======================================================================

        public override void Start(PlayerController controller)
        {
            _characterController = controller.CharacterController;
            _cameraTransform = controller.CameraTransform;

            if (_cameraTransform != null)
            {
                _methilCamera = _cameraTransform.GetComponent<MethilCamera>();
                if (_methilCamera != null)
                    Debug.Log($"[PointAndClick] MethilCamera trouvé sur '{_cameraTransform.name}'");
                else
                    Debug.LogWarning($"[PointAndClick] MethilCamera INTROUVABLE sur '{_cameraTransform.name}'.");
            }
            else
            {
                Debug.LogWarning("[PointAndClick] CameraTransform est null !");
            }

            // Init bob transform
            if (enableHeadBob)
            {
                _bobTarget = _bobTransform != null ? _bobTransform : _cameraTransform?.parent;
                if (_bobTarget != null)
                {
                    _defaultBobPosition = _bobTarget.localPosition;
                    _hasDefaultBobPos = true;
                }
            }

            // Curseur toujours visible
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetCursor(defaultCursor, defaultCursorHotspot);

            // Enable input actions
            EnableAction(pointAction);
            EnableAction(clickAction);
        }

        // =======================================================================

        public override void Update(PlayerController controller)
        {
            if (_characterController == null || _cameraTransform == null) return;

            float dt = Time.deltaTime;

            if (!canMove)
            {
                UpdateHover();
                return;
            }

            switch (_state)
            {
                case State.Idle:
                    UpdateHover();
                    UpdateClick();
                    break;

                case State.Looking:
                    UpdateLooking(dt);
                    break;

                case State.Walking:
                    UpdateMovement(dt);
                    break;
            }

            // Head bob toujours mis à jour
            UpdateHeadBob(dt);
        }

        // =======================================================================
        // Hover
        // =======================================================================

        private void UpdateHover()
        {
            if (_cameraTransform == null) return;

            Vector2 mousePos = GetMousePosition();
            var cam = _cameraTransform.GetComponent<UnityEngine.Camera>();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var zone = hit.collider.GetComponent<ClickableZone>();
                if (zone != _hoveredZone)
                {
                    _hoveredZone = zone;

                    if (zone != null && zone.CursorIcon != null)
                        SetCursor(zone.CursorIcon, zone.CursorHotspot);
                    else
                        SetCursor(defaultCursor, defaultCursorHotspot);
                }
            }
            else if (_hoveredZone != null)
            {
                _hoveredZone = null;
                SetCursor(defaultCursor, defaultCursorHotspot);
            }
        }

        // =======================================================================
        // Click
        // =======================================================================

        private void UpdateClick()
        {
            if (!IsClickPressed()) return;
            Debug.Log($"[PointAndClick] Clic détecté. HoveredZone = {(_hoveredZone != null ? _hoveredZone.name : "NULL")}");
            if (_hoveredZone == null) return;

            _currentTarget = _hoveredZone;
            _targetPosition = _currentTarget.TargetPosition;
            _stuckTimer = 0f;
            _stuckInitialized = false;

            Debug.Log($"[PointAndClick] Cible: '{_currentTarget.name}' (targetPos: {_targetPosition})");

            if (lookBeforeWalk)
            {
                _state = State.Looking;
                _lookTimer = 0f;
                Debug.Log($"[PointAndClick] Phase LOOK avant marche ({lookDuration}s)");
            }
            else
            {
                _state = State.Walking;
                _isMoving = true;
                Debug.Log($"[PointAndClick] Démarrage direct WALK");
            }
        }

        // =======================================================================
        // Looking (turn to face target before walking)
        // =======================================================================

        private void UpdateLooking(float dt)
        {
            _lookTimer += dt;

            // Rotation du joueur pour faire face à la cible
            Vector3 toTarget = _targetPosition - _characterController.transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                _characterController.transform.rotation = Quaternion.RotateTowards(
                    _characterController.transform.rotation,
                    targetRotation,
                    rotationSpeed * 2f * dt // 2x plus rapide que la rotation de marche
                );
            }

            // Après la durée de look, on commence à marcher
            if (_lookTimer >= lookDuration)
            {
                _state = State.Walking;
                _isMoving = true;
                Debug.Log($"[PointAndClick] Look terminé, marche vers '{_currentTarget.name}'");
            }
        }

        // =======================================================================
        // Movement
        // =======================================================================

        private void UpdateMovement(float dt)
        {
            if (_currentTarget == null)
            {
                Debug.Log("[PointAndClick] UpdateMovement: _currentTarget est null, retour à Idle.");
                _state = State.Idle;
                _isMoving = false;
                return;
            }

            Vector3 currentPos = _characterController.transform.position;
            Vector3 toTarget = _targetPosition - currentPos;
            Vector3 horizontalToTarget = toTarget;
            horizontalToTarget.y = 0f;
            float horizontalDistance = horizontalToTarget.magnitude;

            // Log périodique
            if (Time.time > _debugNextLogTime)
            {
                _debugNextLogTime = Time.time + 0.5f;
                Debug.Log($"[PointAndClick] Distance: {horizontalDistance:F2} (seuil: {arrivalThreshold})");
            }

            // Arrivée ?
            if (horizontalDistance <= arrivalThreshold)
            {
                Debug.Log($"[PointAndClick] ARRIVÉ ! dist={horizontalDistance:F2}");
                OnArrived();
                return;
            }

            // Stuck detection
            if (_stuckInitialized)
            {
                float improvement = _lastStuckDistance - horizontalDistance;
                if (improvement < stuckDistanceDelta)
                {
                    _stuckTimer += dt;
                    if (_stuckTimer > stuckTimeout)
                    {
                        Debug.Log($"[PointAndClick] COINCÉ — arrivée forcée. dist={horizontalDistance:F2}");
                        OnArrived();
                        return;
                    }
                }
                else
                {
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckInitialized = true;
                _stuckTimer = 0f;
            }
            _lastStuckDistance = horizontalDistance;

            // Rotation vers la direction de marche
            if (horizontalDistance > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(horizontalToTarget.normalized, Vector3.up);
                _characterController.transform.rotation = Quaternion.RotateTowards(
                    _characterController.transform.rotation,
                    targetRotation,
                    rotationSpeed * dt
                );
            }

            // Déplacement
            Vector3 moveDir = horizontalToTarget.normalized;
            Vector3 move = moveDir * speed;

            if (_characterController.isGrounded && move.y < 0)
                move.y = -2f;
            else if (!_characterController.isGrounded)
                move.y += Physics.gravity.y * dt;

            _characterController.Move(move * dt);
        }

        private void OnArrived()
        {
            _state = State.Idle;
            _isMoving = false;

            Debug.Log($"[PointAndClick] Arrivé à la zone '{_currentTarget.name}'");

            // Orienter la caméra vers la cible de regard
            if (_currentTarget.LookAtTarget == null)
            {
                Debug.LogWarning($"[PointAndClick] LookAtTarget est null sur '{_currentTarget.name}'.");
            }
            else if (_methilCamera == null)
            {
                Debug.LogWarning($"[PointAndClick] MethilCamera introuvable.");
            }
            else
            {
                Debug.Log($"[PointAndClick] LookAtSmooth → '{_currentTarget.LookAtTarget.name}' ({lookAtDuration}s)");
                _methilCamera.LookAtSmooth(_currentTarget.LookAtTarget.gameObject, lookAtDuration);
            }

            // Drain de santé mentale
            if (_currentTarget.SanityDrain > 0f && SanityManager.Instance != null)
            {
                SanityManager.Instance.Drain(_currentTarget.SanityDrain);
            }

            // Événement d'arrivée
            _currentTarget.OnArrival?.Invoke();

            _currentTarget = null;
        }

        // =======================================================================
        // Head Bob
        // =======================================================================

        private void UpdateHeadBob(float dt)
        {
            if (!enableHeadBob || _bobTarget == null || !_hasDefaultBobPos) return;

            Vector3 targetBob = Vector3.zero;

            if (_state == State.Walking)
            {
                _bobPhase += speed * _bobFrequency * dt;
                float phase = _bobPhase % (Mathf.PI * 2f);
                float vertical = Mathf.Sin(phase) * _bobAmount;

                float curveValue = _bobCurve != null ? _bobCurve.Evaluate((Mathf.Sin(phase) + 1f) / 2f) : 1f;
                vertical *= curveValue;

                targetBob = new Vector3(0f, vertical, 0f);
            }

            _currentBobOffset = Vector3.Lerp(_currentBobOffset, targetBob, _bobSmoothSpeed * dt);
            _bobTarget.localPosition = _defaultBobPosition + _currentBobOffset;
        }

        // =======================================================================
        // Input helpers
        // =======================================================================

        private Vector2 GetMousePosition()
        {
            if (pointAction != null && pointAction.action != null)
                return pointAction.action.ReadValue<Vector2>();

            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            return Vector2.zero;
        }

        private bool IsClickPressed()
        {
            if (clickAction != null && clickAction.action != null)
                return clickAction.action.triggered;

            if (Mouse.current != null)
                return Mouse.current.leftButton.wasPressedThisFrame;

            return false;
        }

        // =======================================================================
        // Cursor
        // =======================================================================

        private void SetCursor(Texture2D icon, Vector2 hotspot)
        {
            if (icon != null)
                Cursor.SetCursor(icon, hotspot, CursorMode.Auto);
            else
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private static void EnableAction(InputActionReference actionRef)
        {
            if (actionRef != null && actionRef.action != null)
                actionRef.action.Enable();
        }

        public override void OnDestroy(PlayerController controller)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
