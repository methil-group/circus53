using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using VolFx;

namespace Core
{
    /// <summary>
    /// Gestionnaire d'anxiété du joueur.
    /// Singleton MonoBehaviour à placer dans chaque scène de jeu.
    /// L'anxiété monte passivement (+1 toutes les 3 secondes) et
    /// contrôle l'effet MethilDither (m_Impact) proportionnellement.
    /// </summary>
    public class AnxietyManager : MonoBehaviour
    {
        public static AnxietyManager Instance { get; private set; }

        [Header("Anxiety Settings")]
        [SerializeField, Range(0f, 100f), Tooltip("Anxiété maximum.")]
        private float _maxAnxiety = 100f;
        [SerializeField, Tooltip("L'anxiété augmente de 1 toutes les X secondes.")]
        private float _increaseInterval = 3f;
        [SerializeField, Tooltip("Seuil où les premiers effets se déclenchent (0-1).")]
        [Range(0f, 1f)] private float _highAnxietyThreshold = 0.5f;
        [SerializeField, Tooltip("Seuil critique (0-1).")]
        [Range(0f, 1f)] private float _criticalAnxietyThreshold = 0.8f;

        [Header("Dither Effect")]
        [SerializeField, Tooltip("Volume global de la scène contenant le MethilDither.")]
        private Volume _globalVolume;
        [SerializeField, Tooltip("Impact minimum du dither (à 0 d'anxiété).")]
        [Range(0f, 1f)] private float _ditherImpactMin;
        [SerializeField, Tooltip("Impact maximum du dither (à 100 d'anxiété).")]
        [Range(0f, 1f)] private float _ditherImpactMax = 0.8f;

        [Header("Events")]
        [SerializeField] private UnityEvent<float> _onAnxietyChanged;
        [SerializeField] private UnityEvent _onHighAnxiety;
        [SerializeField] private UnityEvent _onCriticalAnxiety;
        [SerializeField] private UnityEvent _onAnxietyCalmed;

        // =======================================================================

        private float _currentAnxiety;
        private float _increaseTimer;
        private bool _wasHigh;
        private bool _wasCritical;
        private MethilDitherVol _ditherVol;

        /// <summary>Anxiété actuelle.</summary>
        public float CurrentAnxiety => _currentAnxiety;

        /// <summary>Anxiété normalisée (0 à 1).</summary>
        public float NormalizedAnxiety => Mathf.Clamp01(_currentAnxiety / _maxAnxiety);

        /// <summary>True si l'anxiété a dépassé le seuil haut.</summary>
        public bool IsHigh => NormalizedAnxiety >= _highAnxietyThreshold;

        /// <summary>True si l'anxiété a dépassé le seuil critique.</summary>
        public bool IsCritical => NormalizedAnxiety >= _criticalAnxietyThreshold;

        /// <summary>Événement quand l'anxiété change (reçoit la valeur normalisée 0-1).</summary>
        public UnityEvent<float> OnAnxietyChanged => _onAnxietyChanged;

        /// <summary>Événement quand l'anxiété passe au-dessus du seuil haut.</summary>
        public UnityEvent OnHighAnxiety => _onHighAnxiety;

        /// <summary>Événement quand l'anxiété passe au-dessus du seuil critique.</summary>
        public UnityEvent OnCriticalAnxiety => _onCriticalAnxiety;

        /// <summary>Événement quand l'anxiété redescend sous un seuil.</summary>
        public UnityEvent OnAnxietyCalmed => _onAnxietyCalmed;

        // =======================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _currentAnxiety = 0f;
            _increaseTimer = 0f;
            _wasHigh = false;
            _wasCritical = false;
        }

        private void Start()
        {
            if (_globalVolume == null)
            {
                // Cherche le Volume global par défaut
                Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
                foreach (Volume vol in volumes)
                {
                    if (vol.isGlobal)
                    {
                        _globalVolume = vol;
                        break;
                    }
                }
            }

            if (_globalVolume != null && _globalVolume.profile != null)
            {
                if (!_globalVolume.profile.TryGet(out _ditherVol))
                {
                    Debug.LogWarning("[AnxietyManager] Pas de MethilDitherVol dans le Volume global. Ajoute 'VolFx/MethilDither' au profil.");
                }
                else
                {
                    Debug.Log("[AnxietyManager] MethilDitherVol trouvé dans le Volume global.");
                }
            }
            else
            {
                Debug.LogWarning("[AnxietyManager] Aucun Volume global trouvé dans la scène.");
            }
        }

        private void Update()
        {
            // Augmentation de +1 toutes les _increaseInterval secondes
            _increaseTimer += Time.deltaTime;
            if (_increaseTimer >= _increaseInterval)
            {
                _increaseTimer -= _increaseInterval;
                Increase(1f);
            }

            UpdateDither();
        }

        // =======================================================================

        /// <summary>
        /// Augmente l'anxiété.
        /// </summary>
        /// <param name="amount">Quantité à ajouter (positive).</param>
        public void Increase(float amount)
        {
            if (amount <= 0f) return;

            _currentAnxiety = Mathf.Min(_currentAnxiety + amount, _maxAnxiety);

            _onAnxietyChanged?.Invoke(NormalizedAnxiety);
            CheckThresholds();
        }

        /// <summary>
        /// Calme l'anxiété.
        /// </summary>
        /// <param name="amount">Quantité à retirer (positive).</param>
        public void Calm(float amount)
        {
            if (amount <= 0f) return;

            float before = NormalizedAnxiety;
            _currentAnxiety = Mathf.Max(_currentAnxiety - amount, 0f);

            _onAnxietyChanged?.Invoke(NormalizedAnxiety);

            // Si on redescend sous les seuils
            if (before >= _criticalAnxietyThreshold && NormalizedAnxiety < _criticalAnxietyThreshold)
            {
                _onAnxietyCalmed?.Invoke();
            }

            _wasHigh = IsHigh;
            _wasCritical = IsCritical;
        }

        // =======================================================================

        private void CheckThresholds()
        {
            if (!_wasHigh && IsHigh)
            {
                _wasHigh = true;
                _onHighAnxiety?.Invoke();
            }

            if (!_wasCritical && IsCritical)
            {
                _wasCritical = true;
                _onCriticalAnxiety?.Invoke();
            }
        }

        private void UpdateDither()
        {
            if (_ditherVol == null) return;

            float t = NormalizedAnxiety;
            _ditherVol.m_Impact.value = Mathf.Lerp(_ditherImpactMin, _ditherImpactMax, t);
        }
    }
}
