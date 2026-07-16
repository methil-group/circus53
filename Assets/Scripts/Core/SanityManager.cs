using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    /// <summary>
    /// Gestionnaire de santé mentale du joueur.
    /// Singleton MonoBehaviour à placer dans chaque scène de jeu.
    /// La santé mentale baisse passivement et suite à certains événements.
    /// Si elle atteint 0, c'est game over.
    /// </summary>
    public class SanityManager : MonoBehaviour
    {
        public static SanityManager Instance { get; private set; }

        [Header("Sanity Settings")]
        [SerializeField, Range(0f, 200f)] private float _maxSanity = 100f;
        [SerializeField, Range(0f, 10f)] private float _passiveDrainRate = 0.5f;
        [SerializeField, Tooltip("Seuil où les effets visuels commencent (0-1).")]
        [Range(0f, 1f)] private float _lowSanityThreshold = 0.3f;
        [SerializeField, Tooltip("Seuil critique (0-1).")]
        [Range(0f, 1f)] private float _criticalSanityThreshold = 0.1f;

        [Header("Events")]
        [SerializeField] private UnityEvent<float> _onSanityChanged;
        [SerializeField] private UnityEvent _onLowSanity;
        [SerializeField] private UnityEvent _onCriticalSanity;
        [SerializeField] private UnityEvent _onSanityRestored;
        [SerializeField] private UnityEvent _onGameOver;

        // =======================================================================

        private float _currentSanity;
        private bool _wasLow;
        private bool _wasCritical;
        private bool _gameOver;

        /// <summary>Santé mentale actuelle (0 = game over).</summary>
        public float CurrentSanity => _currentSanity;

        /// <summary>Santé mentale normalisée (0 à 1).</summary>
        public float NormalizedSanity => Mathf.Clamp01(_currentSanity / _maxSanity);

        /// <summary>True si la santé est sous le seuil bas.</summary>
        public bool IsLow => NormalizedSanity <= _lowSanityThreshold;

        /// <summary>True si la santé est sous le seuil critique.</summary>
        public bool IsCritical => NormalizedSanity <= _criticalSanityThreshold;

        /// <summary>True si le jeu est terminé (santé = 0).</summary>
        public bool IsGameOver => _gameOver;

        /// <summary>Événement quand la santé change (reçoit la valeur normalisée 0-1).</summary>
        public UnityEvent<float> OnSanityChanged => _onSanityChanged;

        /// <summary>Événement quand la santé passe sous le seuil bas.</summary>
        public UnityEvent OnLowSanity => _onLowSanity;

        /// <summary>Événement quand la santé passe sous le seuil critique.</summary>
        public UnityEvent OnCriticalSanity => _onCriticalSanity;

        /// <summary>Événement quand la santé remonte au-dessus d'un seuil.</summary>
        public UnityEvent OnSanityRestored => _onSanityRestored;

        /// <summary>Événement game over.</summary>
        public UnityEvent OnGameOver => _onGameOver;

        // =======================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _currentSanity = _maxSanity;
            _wasLow = false;
            _wasCritical = false;
            _gameOver = false;
        }

        private void Update()
        {
            if (_gameOver) return;

            // Drain passif
            if (_passiveDrainRate > 0f)
            {
                Drain(_passiveDrainRate * Time.deltaTime);
            }
        }

        // =======================================================================

        /// <summary>
        /// Réduit la santé mentale.
        /// </summary>
        /// <param name="amount">Quantité à drainer (positive).</param>
        public void Drain(float amount)
        {
            if (_gameOver || amount <= 0f) return;

            _currentSanity -= amount;

            if (_currentSanity <= 0f)
            {
                _currentSanity = 0f;
                _gameOver = true;
                _onSanityChanged?.Invoke(0f);
                _onGameOver?.Invoke();
                return;
            }

            _onSanityChanged?.Invoke(NormalizedSanity);

            // Vérifier les seuils
            CheckThresholds();
        }

        /// <summary>
        /// Restaure de la santé mentale.
        /// </summary>
        /// <param name="amount">Quantité à restaurer (positive).</param>
        public void Restore(float amount)
        {
            if (_gameOver || amount <= 0f) return;

            float before = NormalizedSanity;
            _currentSanity = Mathf.Min(_currentSanity + amount, _maxSanity);

            _onSanityChanged?.Invoke(NormalizedSanity);

            // Si on remonte au-dessus des seuils
            if (before <= _lowSanityThreshold && NormalizedSanity > _lowSanityThreshold)
            {
                _onSanityRestored?.Invoke();
            }

            _wasLow = IsLow;
            _wasCritical = IsCritical;
        }

        // =======================================================================

        private void CheckThresholds()
        {
            if (!_wasLow && IsLow)
            {
                _wasLow = true;
                _onLowSanity?.Invoke();
            }

            if (!_wasCritical && IsCritical)
            {
                _wasCritical = true;
                _onCriticalSanity?.Invoke();
            }
        }
    }
}
