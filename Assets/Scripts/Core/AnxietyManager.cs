using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    /// <summary>
    /// Gestionnaire d'anxiété du joueur.
    /// Singleton MonoBehaviour à placer dans chaque scène de jeu.
    /// L'anxiété monte passivement avec le temps et suite à certains événements.
    /// </summary>
    public class AnxietyManager : MonoBehaviour
    {
        public static AnxietyManager Instance { get; private set; }

        [Header("Anxiety Settings")]
        [SerializeField, Range(0f, 100f)] private float _maxAnxiety = 100f;
        [SerializeField, Range(0f, 10f)] private float _passiveIncreaseRate = 1f;
        [SerializeField, Tooltip("Seuil où les premiers effets se déclenchent (0-1).")]
        [Range(0f, 1f)] private float _highAnxietyThreshold = 0.5f;
        [SerializeField, Tooltip("Seuil critique (0-1).")]
        [Range(0f, 1f)] private float _criticalAnxietyThreshold = 0.8f;

        [Header("Events")]
        [SerializeField] private UnityEvent<float> _onAnxietyChanged;
        [SerializeField] private UnityEvent _onHighAnxiety;
        [SerializeField] private UnityEvent _onCriticalAnxiety;
        [SerializeField] private UnityEvent _onAnxietyCalmed;

        // =======================================================================

        private float _currentAnxiety;
        private bool _wasHigh;
        private bool _wasCritical;

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
            _wasHigh = false;
            _wasCritical = false;
        }

        private void Update()
        {
            // Augmentation passive
            if (_passiveIncreaseRate > 0f)
            {
                Increase(_passiveIncreaseRate * Time.deltaTime);
            }
        }

        // =======================================================================

        /// <summary>
        /// Augmente l'anxiété.
        /// </summary>
        /// <param name="amount">Quantité à ajouter (positive).</param>
        public void Increase(float amount)
        {
            if (amount <= 0f) return;

            float before = NormalizedAnxiety;
            _currentAnxiety = Mathf.Min(_currentAnxiety + amount, _maxAnxiety);

            _onAnxietyChanged?.Invoke(NormalizedAnxiety);

            // Vérifier les seuils
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
    }
}
