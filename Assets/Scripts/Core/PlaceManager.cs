using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core
{
    /// <summary>
    /// Gestionnaire de navigation nodale entre les Place du circus.
    /// Singleton. Chaque Place a 4 voisins (front/back/left/right).
    /// Les boutons UI appellent GoFront/GoBack/GoLeft/GoRight pour se déplacer.
    /// Remplace le clic direct sur les points caméra.
    /// </summary>
    public class PlaceManager : MonoBehaviour
    {
        public static PlaceManager Instance { get; private set; }

        private enum State { Idle, Walking, AligningView }

        [Header("Références")]
        [SerializeField] private CircusManager _circusManager;
        [SerializeField] private Place _startingPlace;
        [SerializeField] private NavMeshAgent _navMeshAgent;

        [Header("UI Buttons")]
        [SerializeField] private Button _frontButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _leftButton;
        [SerializeField] private Button _rightButton;

        [Header("Navigation")]
        [SerializeField] private float _speed = 10f;
        [SerializeField] private float _acceleration = 200f;
        [SerializeField] private float _arrivalThreshold = 1.5f;
        [SerializeField, Tooltip("Durée totale de la rotation de tête à l'arrivée.")]
        private float _lookAtDuration = 0.8f;
        [SerializeField, Tooltip("Overshoot de la saccade : la tête dépasse la cible de X% avant de corriger. 0 = pas d'overshoot, 0.1 = 10%.")]
        [Range(0f, 0.2f)] private float _lookOvershoot = 0.06f;
        [SerializeField, Tooltip("Délai de réaction minimum avant de tourner la tête (secondes).")]
        private float _lookDelayMin = 0.05f;
        [SerializeField, Tooltip("Délai de réaction maximum avant de tourner la tête (secondes).")]
        private float _lookDelayMax = 0.15f;
        [SerializeField] private float _rotationSpeed = 360f;

        [Header("Head Bob")]
        [SerializeField] private bool _enableHeadBob = true;
        [SerializeField, Tooltip("Transform qui reçoit le bob (ex: CameraHolder). Si null, cherche automatiquement le parent de la Main Camera.")]
        private Transform _headBobTarget;
        [SerializeField, Tooltip("Amplitude du balancement horizontal (cos). Balancement gauche/droite en courant.")]
        private float _bobAmountX = 0.06f;
        [SerializeField, Tooltip("Amplitude du rebond vertical (sin). Rebond haut/bas en courant.")]
        private float _bobAmountY = 0.12f;
        [SerializeField, Tooltip("Vitesse d'oscillation horizontale. Plus la valeur est haute, plus le balancement latéral est rapide.")]
        private float _bobFrequencyX = 8.5f;
        [SerializeField, Tooltip("Vitesse d'oscillation verticale. Plus la valeur est haute, plus le rebond est rapide.")]
        private float _bobFrequencyY = 5f;
        [SerializeField, Tooltip("Transition horizontale : lerp vers la position cible. Plus c'est haut, plus c'est fluide.")]
        private float _bobSmoothX = 15f;
        [SerializeField, Tooltip("Transition verticale : lerp vers la position cible. Plus c'est haut, plus c'est fluide.")]
        private float _bobSmoothY = 15f;
        [SerializeField, Tooltip("Amplitude de rotation en pitch (X). La tête hoche haut/bas en courant.")]
        private float _bobRotX = 4f;
        [SerializeField, Tooltip("Amplitude de rotation en yaw (Y). La tête tourne gauche/droite en courant.")]
        private float _bobRotY = 3f;
        [SerializeField, Tooltip("Amplitude de rotation en roll (Z). La tête penche gauche/droite.")]
        private float _bobRotZ = 2f;
        [SerializeField, Tooltip("Vitesse d'oscillation des rotations de tête.")]
        private float _bobRotFrequency = 4f;
        [SerializeField, Tooltip("Transition des rotations : lerp vers l'angle cible. Plus c'est haut, plus c'est fluide.")]
        private float _bobRotSmooth = 15f;

        [Header("Events")]
        [SerializeField] private UnityEvent<Place> _onPlaceChanged;
        [SerializeField] private UnityEvent _onNavigationStarted;
        [SerializeField] private UnityEvent _onNavigationComplete;

        // =======================================================================

        private State _state = State.Idle;
        private Place _currentPlace;
        private Place _targetPlace;
        private float _alignTimer;
        private float _alignDelay;
        private Quaternion _alignFromRotation;
        private Quaternion _alignToRotation;
        private float _alignWobbleX;
        private float _alignWobbleZ;

        // Live editor sync
        private Vector3 _lastPlacePosition;
        private Quaternion _lastPlaceRotation;

        // Head bob
        private float _bobPhase;
        private Vector3 _defaultBobPosition;
        private Quaternion _defaultBobRotation;
        private bool _hasDefaultBobPos;

        /// <summary>Place actuel du joueur.</summary>
        public Place CurrentPlace => _currentPlace;

        /// <summary>Place devant (null si pas de voisin).</summary>
        public Place FrontPlace => _currentPlace != null ? _currentPlace.FrontPlace : null;
        public Place BackPlace => _currentPlace != null ? _currentPlace.BackPlace : null;
        public Place LeftPlace => _currentPlace != null ? _currentPlace.LeftPlace : null;
        public Place RightPlace => _currentPlace != null ? _currentPlace.RightPlace : null;

        public bool CanGoFront => FrontPlace != null;
        public bool CanGoBack => BackPlace != null;
        public bool CanGoLeft => LeftPlace != null;
        public bool CanGoRight => RightPlace != null;

        public bool CanNavigate => _state == State.Idle;

        public bool IsNavigating => _state != State.Idle;

        public UnityEvent<Place> OnPlaceChanged => _onPlaceChanged;
        public UnityEvent OnNavigationStarted => _onNavigationStarted;
        public UnityEvent OnNavigationComplete => _onNavigationComplete;

        // =======================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_navMeshAgent == null)
                _navMeshAgent = FindAnyObjectByType<NavMeshAgent>();

            if (_navMeshAgent != null)
            {
                _navMeshAgent.speed = _speed;
                _navMeshAgent.acceleration = _acceleration;
                Debug.Log($"[PlaceManager] NavMeshAgent speed={_speed}, acceleration={_acceleration}");
            }

            if (_circusManager == null)
                _circusManager = FindAnyObjectByType<CircusManager>();

            // Branchement automatique des boutons UI
            WireButton(_frontButton, GoFront);
            WireButton(_backButton, GoBack);
            WireButton(_leftButton, GoLeft);
            WireButton(_rightButton, GoRight);
        }

        private void Start()
        {
            // Init head bob
            if (_enableHeadBob)
            {
                if (_headBobTarget == null)
                {
                    Camera mainCam = Camera.main;
                    if (mainCam != null)
                        _headBobTarget = mainCam.transform.parent; // CameraHolder
                }

                if (_headBobTarget != null)
                {
                    _defaultBobPosition = _headBobTarget.localPosition;
                    _defaultBobRotation = _headBobTarget.localRotation;
                    _hasDefaultBobPos = true;
                }
            }

            if (_startingPlace != null && _currentPlace == null)
            {
                _currentPlace = _startingPlace;
                _circusManager?.SelectPlace(_currentPlace);
                _onPlaceChanged?.Invoke(_currentPlace);

                // Warp immédiat à la position et orientation de la Place de départ
                if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.Warp(_currentPlace.TargetPosition);
                    _navMeshAgent.transform.rotation = _currentPlace.LookRotation;
                }

                // Init trackers pour le live sync
                _lastPlacePosition = _currentPlace.TargetPosition;
                _lastPlaceRotation = _currentPlace.LookRotation;

                Debug.Log($"[PlaceManager] Place de départ : '{_currentPlace.name}' " +
                    $"(pos: {_currentPlace.TargetPosition}, rot: {_currentPlace.LookRotation.eulerAngles})");
            }

            RefreshButtons();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (_state)
            {
                case State.Idle:
                    LiveSyncCurrentPlace();
                    break;

                case State.Walking:
                    UpdateWalking(dt);
                    break;

                case State.AligningView:
                    UpdateAligningView(dt);
                    break;
            }

            UpdateHeadBob(dt);
        }

        // =======================================================================
        // Navigation publique (appelée par les boutons UI)
        // =======================================================================

        public void GoFront() => NavigateTo(FrontPlace);
        public void GoBack() => NavigateTo(BackPlace);
        public void GoLeft() => NavigateTo(LeftPlace);
        public void GoRight() => NavigateTo(RightPlace);

        /// <summary>Force la navigation vers un Place spécifique (debug ou scripting).</summary>
        public void NavigateTo(Place target)
        {
            if (target == null || !CanNavigate)
            {
                Debug.LogWarning($"[PlaceManager] Navigation impossible : target={target?.name}, state={_state}");
                return;
            }

            if (_navMeshAgent == null)
            {
                Debug.LogError("[PlaceManager] Pas de NavMeshAgent !");
                return;
            }

            _targetPlace = target;

            Vector3 destination = _targetPlace.TargetPosition;
            if (_navMeshAgent.isOnNavMesh && _navMeshAgent.isActiveAndEnabled)
            {
                _navMeshAgent.isStopped = false;
                bool ok = _navMeshAgent.SetDestination(destination);
                Debug.Log($"[PlaceManager] SetDestination({destination}) -> {(ok ? "OK" : "FAIL")}, isOnNavMesh={_navMeshAgent.isOnNavMesh}");
            }
            else
            {
                Debug.LogWarning($"[PlaceManager] Agent pas sur le NavMesh ! isOnNavMesh={_navMeshAgent.isOnNavMesh}, isActiveAndEnabled={_navMeshAgent.isActiveAndEnabled}");
            }

            _state = State.Walking;
            RefreshButtons();
            _onNavigationStarted?.Invoke();
            Debug.Log($"[PlaceManager] Navigation vers '{_targetPlace.name}' — boutons désactivés pendant la marche (pos: {destination})");
        }

        [ContextMenu("Snap To Current Place")]
        private void SnapToCurrentPlace()
        {
            if (_currentPlace == null)
            {
                Debug.LogWarning("[PlaceManager] Pas de Place courant.");
                return;
            }

            if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.Warp(_currentPlace.TargetPosition);
                _navMeshAgent.transform.rotation = _currentPlace.LookRotation;
            }

            _lastPlacePosition = _currentPlace.TargetPosition;
            _lastPlaceRotation = _currentPlace.LookRotation;

            // Reset head bob defaults après warp
            if (_headBobTarget != null)
            {
                _defaultBobPosition = _headBobTarget.localPosition;
                _defaultBobRotation = _headBobTarget.localRotation;
            }

            Debug.Log($"[PlaceManager] Snap vers '{_currentPlace.name}' " +
                $"(pos: {_currentPlace.TargetPosition}, rot: {_currentPlace.LookRotation.eulerAngles})");
        }

        // =======================================================================
        // Walking
        // =======================================================================

        private void UpdateWalking(float dt)
        {
            if (_navMeshAgent == null || _targetPlace == null)
            {
                StopNavigation();
                return;
            }

            // Chemin invalide ?
            if (_navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[PlaceManager] Chemin invalide vers '{_targetPlace.name}' — abandon.");
                StopNavigation();
                return;
            }

            // Pas encore calculé ou pas de path
            if (_navMeshAgent.pathPending)
                return;

            if (!_navMeshAgent.hasPath)
            {
                Debug.LogWarning($"[PlaceManager] Pas de path vers '{_targetPlace.name}' — abandon.");
                StopNavigation();
                return;
            }

            // Rotation vers la direction de marche
            Vector3 desiredVelocity = _navMeshAgent.desiredVelocity;
            if (desiredVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(desiredVelocity.normalized, Vector3.up);
                _navMeshAgent.transform.rotation = Quaternion.RotateTowards(
                    _navMeshAgent.transform.rotation,
                    lookRotation,
                    _rotationSpeed * dt
                );
            }

            // Arrivée ?
            if (!_navMeshAgent.pathPending && _navMeshAgent.hasPath &&
                _navMeshAgent.remainingDistance <= _arrivalThreshold)
            {
                Debug.Log($"[PlaceManager] Arrivé à '{_targetPlace.name}'");
                StartViewAlignment();
            }
        }

        // =======================================================================
        // View alignment
        // =======================================================================

        private void StartViewAlignment()
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.ResetPath();

            _alignFromRotation = _navMeshAgent.transform.rotation;
            _alignToRotation = _targetPlace.LookRotation;

            // Wobble aléatoire sur les axes X (pitch) et Z (roll) pour l'effet essouflé
            _alignWobbleX = Random.Range(-3f, 3f);
            _alignWobbleZ = Random.Range(-1.5f, 1.5f);

            _alignDelay = Random.Range(_lookDelayMin, _lookDelayMax);
            _alignTimer = 0f;
            _state = State.AligningView;

            // Active le décor immédiatement
            Place previousPlace = _currentPlace;
            _currentPlace = _targetPlace;
            _circusManager?.SelectPlace(_currentPlace);
            _onPlaceChanged?.Invoke(_currentPlace);
            RefreshButtons();

            // Init trackers pour le live sync
            _lastPlacePosition = _currentPlace.TargetPosition;
            _lastPlaceRotation = _currentPlace.LookRotation;

            Debug.Log($"[PlaceManager] Place changé : '{previousPlace?.name}' → '{_currentPlace.name}'");

            Debug.Log($"[PlaceManager] Alignement vue ({_lookAtDuration}s) vers '{_currentPlace.name}'");
        }

        private void UpdateAligningView(float dt)
        {
            _alignTimer += dt;

            // Phase 0 : délai de réaction (immobile)
            if (_alignTimer < _alignDelay)
                return;

            float activeTime = _alignTimer - _alignDelay;
            float activeDuration = _lookAtDuration;

            if (activeTime >= activeDuration)
            {
                _navMeshAgent.transform.rotation = _alignToRotation;
                _state = State.Idle;
                _targetPlace = null;
                RefreshButtons();
                _onNavigationComplete?.Invoke();
                Debug.Log("[PlaceManager] Navigation terminée.");
                return;
            }

            float t = Mathf.Clamp01(activeTime / activeDuration);
            float humanT = HumanLookCurve(t);

            // Rotation principale (Y) avec overshoot
            Quaternion baseRotation = Quaternion.SlerpUnclamped(
                _alignFromRotation,
                _alignToRotation,
                humanT
            );

            // Wobble X (pitch) et Z (roll) : suit la saccade (0→1→0, pic à t=0.7)
            float wobbleFactor = t < 0.7f ? (t / 0.7f) : (1f - (t - 0.7f) / 0.3f);

            Quaternion wobble = Quaternion.Euler(
                _alignWobbleX * wobbleFactor,
                0f,
                _alignWobbleZ * wobbleFactor
            );

            _navMeshAgent.transform.rotation = baseRotation * wobble;
        }

        /// <summary>
        /// Courbe de regard humain : saccade qui dépasse la cible (70% du temps),
        /// puis correction vers la cible exacte (30% restants).
        /// Retourne des valeurs de 0 → 1+overshoot → 1, utilisées avec SlerpUnclamped.
        /// </summary>
        private float HumanLookCurve(float t)
        {
            const float split = 0.7f;
            float overshoot = _lookOvershoot;

            if (t < split)
            {
                // Phase 1 : saccade vers 1 + overshoot
                float phaseT = t / split;
                return (1f + overshoot) * SmoothStep(phaseT);
            }
            else
            {
                // Phase 2 : correction de l'overshoot vers 1.0
                float phaseT = (t - split) / (1f - split);
                float start = 1f + overshoot;
                return Mathf.Lerp(start, 1f, SmoothStep(phaseT));
            }
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        // =======================================================================
        // UI Buttons
        // =======================================================================

        private void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            // Branchement onClick
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            // Forcer la transition couleur sur le bouton
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            // Remplacer le targetGraphic par l'Image du premier enfant
            // pour que la transition couleur du Button s'applique dessus
            if (button.transform.childCount > 0)
            {
                Transform firstChild = button.transform.GetChild(0);
                Image childImage = firstChild.GetComponent<Image>();
                if (childImage != null)
                {
                    button.targetGraphic = childImage;
                    childImage.enabled = false;
                }

                // Désactiver le Raycast Target sur les Graphic des enfants
                foreach (Graphic graphic in firstChild.GetComponentsInChildren<Graphic>(includeInactive: true))
                {
                    graphic.raycastTarget = false;
                }
            }

            // Cacher l'enfant par défaut
            SetFirstChildActive(button, false);

            // EventTrigger pour hover
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            trigger.triggers.Clear();

            EventTrigger.Entry enter = new() { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => OnButtonHover(button, true));
            trigger.triggers.Add(enter);

            EventTrigger.Entry exit = new() { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => OnButtonHover(button, false));
            trigger.triggers.Add(exit);
        }

        private void RefreshButtons()
        {
            bool canClick = CanNavigate;
            SetButtonInteractable(_frontButton, CanGoFront && canClick);
            SetButtonInteractable(_backButton, CanGoBack && canClick);
            SetButtonInteractable(_leftButton, CanGoLeft && canClick);
            SetButtonInteractable(_rightButton, CanGoRight && canClick);

            Debug.Log($"[PlaceManager] Boutons mis à jour (interactable={canClick}) — " +
                $"Front={(CanGoFront ? (FrontPlace != null ? FrontPlace.name : "?") : "X")} " +
                $"Back={(CanGoBack ? (BackPlace != null ? BackPlace.name : "?") : "X")} " +
                $"Left={(CanGoLeft ? (LeftPlace != null ? LeftPlace.name : "?") : "X")} " +
                $"Right={(CanGoRight ? (RightPlace != null ? RightPlace.name : "?") : "X")}");
        }

        private void SetButtonInteractable(Button button, bool interactable)
        {
            if (button == null) return;
            button.interactable = interactable;

            // Si désactivé, cacher l'enfant et désactiver son Image
            if (!interactable)
            {
                SetFirstChildActive(button, false);
                SetChildImageEnabled(button, false);
            }
        }

        private void OnButtonHover(Button button, bool hover)
        {
            if (button == null) return;

            bool show = hover && button.interactable;
            SetFirstChildActive(button, show);
            SetChildImageEnabled(button, show);
        }

        private static void SetFirstChildActive(Button button, bool active)
        {
            if (button == null || button.transform.childCount == 0) return;
            Transform firstChild = button.transform.GetChild(0);
            if (firstChild != null)
                firstChild.gameObject.SetActive(active);
        }

        private static void SetChildImageEnabled(Button button, bool enabled)
        {
            // Enable/disable the targetGraphic (which is the child's Image)
            // so the Button's Color Tint transition can animate it on hover
            Graphic graphic = button?.targetGraphic;
            if (graphic != null)
                graphic.enabled = enabled;
        }

        // =======================================================================

        // =======================================================================
        // Head Bob
        // =======================================================================

        private void UpdateHeadBob(float dt)
        {
            if (!_enableHeadBob || _headBobTarget == null || !_hasDefaultBobPos) return;

            Vector3 targetBob = Vector3.zero;
            Vector3 targetRot = Vector3.zero;

            if (_state == State.Walking && _navMeshAgent != null && _navMeshAgent.velocity.sqrMagnitude > 0.01f)
            {
                float phaseX = _bobPhase * _bobFrequencyX;
                float phaseY = _bobPhase * _bobFrequencyY;
                float phaseRot = _bobPhase * _bobRotFrequency;
                _bobPhase += _speed * dt;

                float horizontal = Mathf.Cos(phaseX) * _bobAmountX;
                float vertical = Mathf.Sin(phaseY) * _bobAmountY;
                targetBob = new Vector3(horizontal, vertical, 0f);

                float rotX = Mathf.Sin(phaseRot * 0.7f) * _bobRotX;
                float rotY = Mathf.Cos(phaseRot * 1.3f) * _bobRotY;
                float rotZ = Mathf.Sin(phaseRot * 1.1f) * _bobRotZ;
                targetRot = new Vector3(rotX, rotY, rotZ);
            }

            // Position bob
            Vector3 currentPos = _headBobTarget.localPosition;
            Vector3 targetPos = _defaultBobPosition + targetBob;
            _headBobTarget.localPosition = new Vector3(
                Mathf.Lerp(currentPos.x, targetPos.x, _bobSmoothX * dt),
                Mathf.Lerp(currentPos.y, targetPos.y, _bobSmoothY * dt),
                currentPos.z
            );

            // Rotation bob
            Quaternion targetRotation = _defaultBobRotation * Quaternion.Euler(targetRot);
            _headBobTarget.localRotation = Quaternion.Slerp(
                _headBobTarget.localRotation,
                targetRotation,
                _bobRotSmooth * dt
            );
        }

        // =======================================================================

        // =======================================================================
        // Live Editor Sync
        // =======================================================================

        private void LiveSyncCurrentPlace()
        {
            if (_currentPlace == null || _navMeshAgent == null) return;

            Vector3 currentPos = _currentPlace.TargetPosition;
            Quaternion currentRot = _currentPlace.LookRotation;

            if (currentPos != _lastPlacePosition || currentRot != _lastPlaceRotation)
            {
                if (_navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.Warp(currentPos);
                    _navMeshAgent.transform.rotation = currentRot;
                }

                _lastPlacePosition = currentPos;
                _lastPlaceRotation = currentRot;

                // Reset head bob defaults après live sync
                if (_headBobTarget != null)
                {
                    _defaultBobPosition = _headBobTarget.localPosition;
                    _defaultBobRotation = _headBobTarget.localRotation;
                }
            }
        }

        // =======================================================================

        private void StopNavigation()
        {
            _state = State.Idle;
            _targetPlace = null;
            if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh && _navMeshAgent.isActiveAndEnabled)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();
            }
            Debug.Log("[PlaceManager] Navigation stoppée.");
        }
    }
}
