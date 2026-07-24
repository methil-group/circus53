using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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

        [Header("Max Anxiety")]
        [SerializeField, Tooltip("Son joué en boucle quand l'anxiété atteint son max.")]
        private AudioClip _maxAnxietyLoopSound;
        [SerializeField, Tooltip("GameObject qui apparaît quand l'anxiété atteint son max.")]
        private GameObject _maxAnxietyObject;
        [SerializeField, Tooltip("Délai avant l'apparition du GameObject (secondes).")]
        private float _objectAppearDelay = 5f;
        [SerializeField, Tooltip("Si coché, bloque les déplacements du joueur au max.")]
        private bool _blockMovementOnMax = true;
        [SerializeField, Tooltip("Délai avant de changer de scène au max (secondes).")]
        private float _sceneChangeDelay = 3f;
        [SerializeField, Tooltip("Build index de la scène cible.")]
        private int _targetSceneBuildIndex = 1;

        [Header("Dither Effect")]
        [SerializeField, Tooltip("Volume global de la scène contenant le MethilDither.")]
        private Volume _globalVolume;
        [SerializeField, Tooltip("Impact minimum du dither (à 0 d'anxiété).")]
        [Range(0f, 1f)] private float _ditherImpactMin = 0.6f;
        [SerializeField, Tooltip("Impact maximum du dither (à 100 d'anxiété).")]
        [Range(0f, 1f)] private float _ditherImpactMax = 0.9f;
        [SerializeField, Tooltip("Courbe de progression de l'impact (0→1 = anxiété 0→100). X=anxiété normalisée, Y=impact.")]
        private AnimationCurve _ditherCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Debug")]
        [SerializeField, Tooltip("Slider UI optionnel pour visualiser l'anxiété en temps réel.")]
        private Slider _debugSlider;
        [SerializeField] private UnityEvent<float> _onAnxietyChanged;
        [SerializeField] private UnityEvent _onHighAnxiety;
        [SerializeField] private UnityEvent _onCriticalAnxiety;
        [SerializeField] private UnityEvent _onAnxietyCalmed;

        // =======================================================================

        private float _currentAnxiety;
        private float _increaseTimer;
        private bool _wasHigh;
        private bool _wasCritical;
        private bool _hasTriggeredMax;
        private AudioSource _audioSource;
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
            _hasTriggeredMax = false;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;

            if (_maxAnxietyObject != null)
                _maxAnxietyObject.SetActive(false);
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
        /// Augmente l'anxiété instantanément.
        /// </summary>
        public void Increase(float amount)
        {
            if (amount <= 0f) return;

            _currentAnxiety = Mathf.Min(_currentAnxiety + amount, _maxAnxiety);

            _onAnxietyChanged?.Invoke(NormalizedAnxiety);
            CheckThresholds();

            // Max anxiety reached
            if (NormalizedAnxiety >= 1f && !_hasTriggeredMax)
                TriggerMaxAnxiety();
        }

        /// <summary>
        /// Augmente l'anxiété progressivement sur une durée donnée (lerp fluide).
        /// </summary>
        public void IncreaseOverTime(float amount, float duration)
        {
            if (amount <= 0f || duration <= 0f) return;
            StartCoroutine(IncreaseOverTimeRoutine(amount, duration));
        }

        private IEnumerator IncreaseOverTimeRoutine(float amount, float duration)
        {
            float startAnxiety = _currentAnxiety;
            float targetAnxiety = Mathf.Min(startAnxiety + amount, _maxAnxiety);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease in : lent au début, accélère à la fin (effet angoissant)
                t = t * t;

                _currentAnxiety = Mathf.Lerp(startAnxiety, targetAnxiety, t);

                _onAnxietyChanged?.Invoke(NormalizedAnxiety);
                CheckThresholds();

                if (NormalizedAnxiety >= 1f && !_hasTriggeredMax)
                    TriggerMaxAnxiety();

                yield return null;
            }

            // Snap final
            _currentAnxiety = targetAnxiety;
            _onAnxietyChanged?.Invoke(NormalizedAnxiety);
            CheckThresholds();

            if (NormalizedAnxiety >= 1f && !_hasTriggeredMax)
                TriggerMaxAnxiety();
        }

        private void TriggerMaxAnxiety()
        {
            _hasTriggeredMax = true;
            Debug.Log("[AnxietyManager] Anxiété maximale atteinte.");

            // Bloquer les déplacements
            if (_blockMovementOnMax)
            {
                PlaceManager.Instance?.SetBlocked(true);
            }

            // Lancer le son en boucle
            if (_maxAnxietyLoopSound != null && _audioSource != null)
            {
                _audioSource.clip = _maxAnxietyLoopSound;
                _audioSource.Play();
            }

            // Apparition de l'objet après délai + changement de scène
            StartCoroutine(MaxAnxietyRoutine());
        }

        private IEnumerator MaxAnxietyRoutine()
        {
            float elapsed = 0f;
            bool objectShown = false;

            while (true)
            {
                elapsed += Time.deltaTime;

                // Apparition de l'objet
                if (!objectShown && elapsed >= _objectAppearDelay && _maxAnxietyObject != null)
                {
                    objectShown = true;
                    _maxAnxietyObject.SetActive(true);
                }

                // Changement de scène
                if (elapsed >= _sceneChangeDelay)
                {
                    SceneManager.LoadScene(_targetSceneBuildIndex);
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Calme l'anxiété instantanément.
        /// </summary>
        public void Calm(float amount)
        {
            if (amount <= 0f) return;

            float before = NormalizedAnxiety;
            _currentAnxiety = Mathf.Max(_currentAnxiety - amount, 0f);

            _onAnxietyChanged?.Invoke(NormalizedAnxiety);

            if (before >= _criticalAnxietyThreshold && NormalizedAnxiety < _criticalAnxietyThreshold)
                _onAnxietyCalmed?.Invoke();

            _wasHigh = IsHigh;
            _wasCritical = IsCritical;
        }

        /// <summary>
        /// Calme l'anxiété progressivement sur une durée donnée (lerp fluide).
        /// </summary>
        public void CalmOverTime(float amount, float duration)
        {
            if (amount <= 0f || duration <= 0f) return;
            StartCoroutine(CalmOverTimeRoutine(amount, duration));
        }

        private IEnumerator CalmOverTimeRoutine(float amount, float duration)
        {
            float startAnxiety = _currentAnxiety;
            float targetAnxiety = Mathf.Max(startAnxiety - amount, 0f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease out : rapide au début, ralentit à la fin
                t = 1f - (1f - t) * (1f - t);

                float before = NormalizedAnxiety;
                _currentAnxiety = Mathf.Lerp(startAnxiety, targetAnxiety, t);

                _onAnxietyChanged?.Invoke(NormalizedAnxiety);

                if (before >= _criticalAnxietyThreshold && NormalizedAnxiety < _criticalAnxietyThreshold)
                    _onAnxietyCalmed?.Invoke();

                _wasHigh = IsHigh;
                _wasCritical = IsCritical;

                yield return null;
            }

            // Snap final
            _currentAnxiety = targetAnxiety;
            _onAnxietyChanged?.Invoke(NormalizedAnxiety);
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
            float t = NormalizedAnxiety;

            if (_ditherVol != null)
            {
                float curvedT = _ditherCurve.Evaluate(t);
                _ditherVol.m_Impact.value = Mathf.Lerp(_ditherImpactMin, _ditherImpactMax, curvedT);
            }

            if (_debugSlider != null)
                _debugSlider.value = t;
        }
    }
}
