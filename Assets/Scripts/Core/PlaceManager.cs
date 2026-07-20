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
        [SerializeField] private float _speed = 4f;
        [SerializeField] private float _arrivalThreshold = 1.5f;
        [SerializeField] private float _lookAtDuration = 0.8f;
        [SerializeField] private float _rotationSpeed = 360f;

        [Header("Events")]
        [SerializeField] private UnityEvent<Place> _onPlaceChanged;
        [SerializeField] private UnityEvent _onNavigationStarted;
        [SerializeField] private UnityEvent _onNavigationComplete;

        // =======================================================================

        private State _state = State.Idle;
        private Place _currentPlace;
        private Place _targetPlace;
        private float _alignTimer;
        private Quaternion _alignFromRotation;
        private Quaternion _alignToRotation;
        private float _debugNextLogTime;

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
                _navMeshAgent.speed = _speed;

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
            if (_startingPlace != null && _currentPlace == null)
            {
                _currentPlace = _startingPlace;
                _circusManager?.SelectPlace(_currentPlace);
                _onPlaceChanged?.Invoke(_currentPlace);
                Debug.Log($"[PlaceManager] Place de départ : '{_currentPlace.name}'");
            }

            RefreshButtons();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (_state)
            {
                case State.Idle:
                    break;

                case State.Walking:
                    UpdateWalking(dt);
                    break;

                case State.AligningView:
                    UpdateAligningView(dt);
                    break;
            }
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
                _navMeshAgent.SetDestination(destination);
            }

            _state = State.Walking;
            RefreshButtons();
            _onNavigationStarted?.Invoke();
            Debug.Log($"[PlaceManager] Navigation vers '{_targetPlace.name}' — boutons désactivés pendant la marche (pos: {destination})");
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

            // Log périodique
            if (Time.time > _debugNextLogTime)
            {
                _debugNextLogTime = Time.time + 0.5f;
                float remaining = _navMeshAgent.hasPath ? _navMeshAgent.remainingDistance : float.MaxValue;
                Debug.Log($"[PlaceManager] remainingDistance: {remaining:F2} (seuil: {_arrivalThreshold})");
            }

            // Chemin invalide ?
            if (_navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[PlaceManager] Chemin invalide vers '{_targetPlace.name}' — abandon.");
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
            _alignTimer = 0f;
            _state = State.AligningView;

            // Active le décor immédiatement
            Place previousPlace = _currentPlace;
            _currentPlace = _targetPlace;
            _circusManager?.SelectPlace(_currentPlace);
            _onPlaceChanged?.Invoke(_currentPlace);
            RefreshButtons();

            Debug.Log($"[PlaceManager] Place changé : '{previousPlace?.name}' → '{_currentPlace.name}'");

            Debug.Log($"[PlaceManager] Alignement vue ({_lookAtDuration}s) vers '{_currentPlace.name}'");
        }

        private void UpdateAligningView(float dt)
        {
            _alignTimer += dt;
            float t = Mathf.Clamp01(_alignTimer / _lookAtDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            _navMeshAgent.transform.rotation = Quaternion.Slerp(
                _alignFromRotation,
                _alignToRotation,
                t
            );

            if (_alignTimer >= _lookAtDuration)
            {
                _navMeshAgent.transform.rotation = _alignToRotation;
                _state = State.Idle;
                _targetPlace = null;
                RefreshButtons();
                _onNavigationComplete?.Invoke();
                Debug.Log("[PlaceManager] Navigation terminée.");
            }
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
