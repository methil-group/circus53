using Framework.UI.FX;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI
{
    /// <summary>
    /// Affiche la jauge de santé mentale et les effets visuels associés.
    /// À placer sur un Canvas en Screen Space - Overlay.
    /// </summary>
    public class SanityUI : MonoBehaviour
    {
        [Header("Barre de santé")]
        [SerializeField] private Image _sanityFillImage;
        [SerializeField] private Gradient _sanityColor;
        [SerializeField, Tooltip("Animation de la barre : vitesse de smooth.")]
        private float _barSmoothSpeed = 5f;

        [Header("Effets écran")]
        [SerializeField] private Image _vignetteImage;
        [SerializeField, Tooltip("Opacité max de la vignette."), Range(0f, 1f)]
        private float _vignetteMaxAlpha = 0.6f;
        [SerializeField, Tooltip("La vignette commence à apparaître à ce seuil."), Range(0f, 1f)]
        private float _vignetteStartThreshold = 0.5f;

        [Header("Pulsation critique")]
        [SerializeField, Tooltip("Vitesse de pulsation quand la santé est critique.")]
        private float _pulseSpeed = 3f;
        [SerializeField, Tooltip("Amplitude de la pulsation (alpha)."), Range(0f, 0.3f)]
        private float _pulseAmplitude = 0.15f;

        [Header("Glitch Text (optionnel)")]
        [SerializeField] private GlitchText _glitchText;

        // =======================================================================

        private SanityManager _sanity;
        private float _displayedFill = 1f;
        private bool _isCritical;

        private void Start()
        {
            _sanity = SanityManager.Instance;
            if (_sanity != null)
            {
                _sanity.OnSanityChanged.AddListener(OnSanityChanged);
                _sanity.OnCriticalSanity.AddListener(OnCritical);
                _sanity.OnSanityRestored.AddListener(OnRestored);
                _sanity.OnGameOver.AddListener(OnGameOver);
            }

            if (_sanityFillImage != null)
            {
                _sanityFillImage.fillAmount = 1f;
                _displayedFill = 1f;
            }

            if (_vignetteImage != null)
            {
                var c = _vignetteImage.color;
                c.a = 0f;
                _vignetteImage.color = c;
            }
        }

        private void OnDestroy()
        {
            if (_sanity != null)
            {
                _sanity.OnSanityChanged.RemoveListener(OnSanityChanged);
                _sanity.OnCriticalSanity.RemoveListener(OnCritical);
                _sanity.OnSanityRestored.RemoveListener(OnRestored);
                _sanity.OnGameOver.RemoveListener(OnGameOver);
            }
        }

        private void Update()
        {
            if (_sanity == null || _sanityFillImage == null) return;

            // Lissage de la barre
            float target = _sanity.NormalizedSanity;
            _displayedFill = Mathf.Lerp(_displayedFill, target, _barSmoothSpeed * Time.deltaTime);
            _sanityFillImage.fillAmount = _displayedFill;

            if (_sanityColor != null)
                _sanityFillImage.color = _sanityColor.Evaluate(_displayedFill);

            // Vignette
            UpdateVignette();

            // Pulsation critique
            UpdatePulse();

            // Glitch text
            if (_glitchText != null && _sanity != null)
            {
                float glitchIntensity = 1f - _sanity.NormalizedSanity;
                _glitchText.Intensity = glitchIntensity * glitchIntensity; // quadratique pour un effet plus marqué à la fin
            }
        }

        private void UpdateVignette()
        {
            if (_vignetteImage == null || _sanity == null) return;

            float sanity = _sanity.NormalizedSanity;
            float targetAlpha;

            if (sanity <= _vignetteStartThreshold)
            {
                float t = 1f - (sanity / _vignetteStartThreshold);
                targetAlpha = t * _vignetteMaxAlpha;
            }
            else
            {
                targetAlpha = 0f;
            }

            var color = _vignetteImage.color;
            color.a = Mathf.Lerp(color.a, targetAlpha, 3f * Time.deltaTime);
            _vignetteImage.color = color;
        }

        private void UpdatePulse()
        {
            if (_vignetteImage == null || !_isCritical) return;

            float pulse = Mathf.Sin(Time.time * _pulseSpeed) * 0.5f + 0.5f;
            float extraAlpha = pulse * _pulseAmplitude;

            var color = _vignetteImage.color;
            color.a = Mathf.Min(color.a + extraAlpha, _vignetteMaxAlpha + _pulseAmplitude);
            _vignetteImage.color = color;
        }

        // =======================================================================
        // Event callbacks
        // =======================================================================

        private void OnSanityChanged(float normalized)
        {
            // Rien de spécial ici, géré dans Update
        }

        private void OnCritical()
        {
            _isCritical = true;
        }

        private void OnRestored()
        {
            _isCritical = false;
        }

        private void OnGameOver()
        {
            // Game over — pourrait déclencher une transition, un écran noir, etc.
            Debug.Log("SANTÉ MENTALE ÉPUISÉE — GAME OVER");
        }
    }
}
