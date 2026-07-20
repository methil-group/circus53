using System;
using Framework;
using Framework.Camera;
using UnityEngine;
using UnityEngine.AI;
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
        private enum State { Idle, Looking, Walking, AligningView }

        [Header("Movement")]
        [SerializeField] private bool canMove = true;
        [SerializeField] private float speed = 4f;
        [SerializeField] private float arrivalThreshold = 1.5f;
        [SerializeField] private float acceleration = 100f;

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

        [Header("Inputs (optional — fallback to Mouse.current)")]
        [SerializeField] private InputActionReference pointAction;
        [SerializeField] private InputActionReference clickAction;

        [Header("Cursor")]
        [SerializeField] private Texture2D defaultCursor;
        [SerializeField] private Vector2 defaultCursorHotspot = Vector2.zero;

        // =======================================================================

        private NavMeshAgent _navMeshAgent;
        private PlayerController _controller;
        private Transform _cameraTransform;
        private PointAndClickRayDebug _rayDebug;
        private Transform _bobTarget;
        private MethilCamera _methilCamera;

        private State _state = State.Idle;
        private ClickableZone _currentTarget;
        private CameraClickPoint _currentCameraPoint;
        private ClickableZone _hoveredZone;
        private CameraClickPoint _hoveredCameraPoint;
        private Vector3 _targetPosition;
        private bool _isMoving;

        // Look-before-walk
        private float _lookTimer;

        // Smooth view alignment on camera point arrival
        private float _alignViewTimer;
        private float _alignViewDuration;
        private Quaternion _alignViewFromRotation;
        private Quaternion _alignViewToRotation;

        // Head bob
        private float _bobPhase;
        private Vector3 _currentBobOffset;
        private Vector3 _defaultBobPosition;
        private bool _hasDefaultBobPos;
        private Ray _lastInteractionRay;
        private RaycastHit[] _lastInteractionHits = Array.Empty<RaycastHit>();

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
            _controller = controller;
            _cameraTransform = controller.CameraTransform;
            _rayDebug = controller.GetComponent<PointAndClickRayDebug>();
            if (_rayDebug == null)
                _rayDebug = controller.gameObject.AddComponent<PointAndClickRayDebug>();

            // NavMeshAgent : utilise le CharacterController existant pour copier rayon/hauteur
            _navMeshAgent = controller.GetComponent<NavMeshAgent>();
            if (_navMeshAgent == null)
            {
                _navMeshAgent = controller.gameObject.AddComponent<NavMeshAgent>();
                CharacterController cc = controller.CharacterController;
                if (cc != null)
                {
                    _navMeshAgent.radius = cc.radius;
                    _navMeshAgent.height = cc.height;
                    _navMeshAgent.baseOffset = cc.center.y - cc.height * 0.5f + _navMeshAgent.height * 0.5f;
                }
            }

            _navMeshAgent.speed = speed;
            _navMeshAgent.angularSpeed = rotationSpeed;
            _navMeshAgent.acceleration = acceleration;
            _navMeshAgent.stoppingDistance = arrivalThreshold;
            _navMeshAgent.autoBraking = true;
            _navMeshAgent.updatePosition = true;
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.updateUpAxis = true;
            _navMeshAgent.isStopped = true;

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
            if (_navMeshAgent == null || _cameraTransform == null) return;

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

                case State.AligningView:
                    UpdateAligningView(dt);
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
            _lastInteractionRay = ray;

            ClickableZone zone = null;
            CameraClickPoint cameraPoint = null;
            float nearestInteractiveDistance = float.MaxValue;

            // Le blocking possède aussi des colliders : RaycastAll évite qu'un mur
            // masque les points-caméra placés derrière lui.
            _lastInteractionHits = Physics.RaycastAll(ray, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in _lastInteractionHits)
            {
                ClickableZone candidateZone = hit.collider.GetComponentInParent<ClickableZone>();
                CameraClickPoint candidateCameraPoint = hit.collider.GetComponentInParent<CameraClickPoint>();

                if ((candidateZone == null && candidateCameraPoint == null) || hit.distance >= nearestInteractiveDistance)
                    continue;

                nearestInteractiveDistance = hit.distance;
                zone = candidateZone;
                cameraPoint = candidateCameraPoint;
            }

            // Une zone de 0.05 unité est idéale pour ne pas gêner les colliders
            // du décor, mais trop petite pour être visée à distance. Quand les
            // gizmos sont visibles, on autorise donc aussi un clic près de
            // l'icône caméra affichée à l'écran.
            if (zone == null && cameraPoint == null)
                cameraPoint = FindCameraPointNearCursor(cam, mousePos);

            if (cameraPoint != _hoveredCameraPoint || zone != _hoveredZone)
            {
                _hoveredZone = zone;
                _hoveredCameraPoint = cameraPoint;

                if (zone != null && zone.CursorIcon != null)
                    SetCursor(zone.CursorIcon, zone.CursorHotspot);
                else
                    SetCursor(defaultCursor, defaultCursorHotspot);
            }
        }

        private static CameraClickPoint FindCameraPointNearCursor(UnityEngine.Camera camera, Vector2 mousePosition)
        {
            CameraClickPoint closest = null;
            float closestDistanceSqr = float.MaxValue;

            foreach (CameraClickPoint point in UnityEngine.Resources.FindObjectsOfTypeAll<CameraClickPoint>())
            {
                Vector3 screenPosition = camera.WorldToScreenPoint(point.transform.position);
                if (screenPosition.z <= 0f) continue;

                float distanceSqr = ((Vector2)screenPosition - mousePosition).sqrMagnitude;
                float radius = point.ScreenClickRadius;
                if (distanceSqr > radius * radius || distanceSqr >= closestDistanceSqr) continue;

                closest = point;
                closestDistanceSqr = distanceSqr;
            }

            return closest;
        }

        private void LogLastRaycast()
        {
            _rayDebug?.Record(_lastInteractionRay, _lastInteractionHits);
            Debug.Log($"[PointAndClick] Ray: origin={_lastInteractionRay.origin}, direction={_lastInteractionRay.direction}, hits={_lastInteractionHits.Length}");

            foreach (RaycastHit hit in _lastInteractionHits)
            {
                Collider collider = hit.collider;
                Debug.Log($"[PointAndClick] Hit: '{collider.name}' | layer={LayerMask.LayerToName(collider.gameObject.layer)} ({collider.gameObject.layer}) | distance={hit.distance:F3} | zone={collider.GetComponentInParent<ClickableZone>() != null} | cameraPoint={collider.GetComponentInParent<CameraClickPoint>() != null}");
            }
        }

        // =======================================================================
        // Click
        // =======================================================================

        private void UpdateClick()
        {
            if (!IsClickPressed()) return;
            LogLastRaycast();
            Debug.Log($"[PointAndClick] Clic détecté. Zone = {(_hoveredZone != null ? _hoveredZone.name : "NULL")}, CameraPoint = {(_hoveredCameraPoint != null ? _hoveredCameraPoint.name : "NULL")}");
            if (_hoveredCameraPoint != null)
            {
                _currentTarget = null;
                _currentCameraPoint = _hoveredCameraPoint;
                _targetPosition = _currentCameraPoint.transform.position;
                StartTravelToCurrentTarget();
                return;
            }
            if (_hoveredZone == null) return;

            _currentTarget = _hoveredZone;
            _currentCameraPoint = null;
            _targetPosition = _currentTarget.TargetPosition;
            StartTravelToCurrentTarget();
        }

        private void StartTravelToCurrentTarget()
        {
            string targetName = _currentCameraPoint != null ? _currentCameraPoint.name : _currentTarget.name;
            Debug.Log($"[PointAndClick] Cible: '{targetName}' (targetPos: {_targetPosition})");

            _navMeshAgent.isStopped = true;
            _navMeshAgent.SetDestination(_targetPosition);

            if (lookBeforeWalk)
            {
                _state = State.Looking;
                _lookTimer = 0f;
                Debug.Log($"[PointAndClick] Phase LOOK avant marche ({lookDuration}s)");
            }
            else
            {
                _navMeshAgent.isStopped = false;
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
            Vector3 toTarget = _targetPosition - _controller.transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                _controller.transform.rotation = Quaternion.RotateTowards(
                    _controller.transform.rotation,
                    targetRotation,
                    rotationSpeed * 2f * dt // 2x plus rapide que la rotation de marche
                );
            }

            // Après la durée de look, on commence à marcher
            if (_lookTimer >= lookDuration)
            {
                _navMeshAgent.isStopped = false;
                _state = State.Walking;
                _isMoving = true;
                string targetName = _currentCameraPoint != null ? _currentCameraPoint.name : _currentTarget.name;
                Debug.Log($"[PointAndClick] Look terminé, marche vers '{targetName}'");
            }
        }

        // =======================================================================
        // Movement
        // =======================================================================

        private void UpdateMovement(float dt)
        {
            if (_currentTarget == null && _currentCameraPoint == null)
            {
                Debug.Log("[PointAndClick] UpdateMovement: cible null, retour à Idle.");
                StopNavigation();
                return;
            }

            // Vérifier si le path est valide
            if (_navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[PointAndClick] Chemin invalide vers la cible — abandon.");
                OnArrivedFallback();
                return;
            }

            // Log périodique
            if (Time.time > _debugNextLogTime)
            {
                _debugNextLogTime = Time.time + 0.5f;
                float remaining = _navMeshAgent.hasPath ? _navMeshAgent.remainingDistance : float.MaxValue;
                Debug.Log($"[PointAndClick] remainingDistance: {remaining:F2} (stoppingDistance: {_navMeshAgent.stoppingDistance})");
            }

            // Arrivée ?
            if (!_navMeshAgent.pathPending && _navMeshAgent.hasPath &&
                _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
            {
                Debug.Log($"[PointAndClick] ARRIVÉ ! remainingDistance={_navMeshAgent.remainingDistance:F2}");
                OnArrived();
                return;
            }

            // Rotation vers la direction de déplacement (là où le NavMeshAgent va)
            Vector3 desiredVelocity = _navMeshAgent.desiredVelocity;
            if (desiredVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity.normalized, Vector3.up);
                _controller.transform.rotation = Quaternion.RotateTowards(
                    _controller.transform.rotation,
                    targetRotation,
                    rotationSpeed * dt
                );
            }
        }

        private void UpdateAligningView(float dt)
        {
            _alignViewTimer += dt;
            float t = Mathf.Clamp01(_alignViewTimer / _alignViewDuration);
            t = Mathf.SmoothStep(0f, 1f, t); // ease-in-out

            _controller.transform.rotation = Quaternion.Slerp(
                _alignViewFromRotation,
                _alignViewToRotation,
                t
            );

            if (_alignViewTimer >= _alignViewDuration)
            {
                _controller.transform.rotation = _alignViewToRotation;
                _state = State.Idle;
                Debug.Log("[PointAndClick] Alignement vue terminé.");
            }
        }

        private void StopNavigation()
        {
            _state = State.Idle;
            _isMoving = false;
            _currentTarget = null;
            _currentCameraPoint = null;
            if (_navMeshAgent != null)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();
            }
        }

        /// <summary>Arrivée même si le path est invalide (fallback sans rien appliquer).</summary>
        private void OnArrivedFallback()
        {
            Debug.Log("[PointAndClick] Arrivée fallback (path invalide ou autre).");
            StopNavigation();
        }

        private void OnArrived()
        {
            _state = State.Idle;
            _isMoving = false;
            _navMeshAgent.isStopped = true;
            _navMeshAgent.ResetPath();

            if (_currentCameraPoint != null)
            {
                Debug.Log($"[PointAndClick] Arrivé au point caméra '{_currentCameraPoint.name}'");

                // Active le Place immédiatement (objets à activer/désactiver)
                _currentCameraPoint.SelectPlace();

                // Transition fluide de la vue vers le cadrage de la caméra
                Quaternion rotationDelta = _currentCameraPoint.GetViewRotationDelta(_controller);
                _alignViewFromRotation = _controller.transform.rotation;
                _alignViewToRotation = rotationDelta * _controller.transform.rotation;
                _alignViewTimer = 0f;
                _alignViewDuration = lookAtDuration;
                _state = State.AligningView;

                Debug.Log($"[PointAndClick] Début alignement vue ({_alignViewDuration}s)");
                _currentCameraPoint = null;
                return;
            }

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

            if (_state == State.Walking && _navMeshAgent != null && _navMeshAgent.velocity.sqrMagnitude > 0.01f)
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
