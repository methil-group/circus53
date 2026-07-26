using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core
{
    /// <summary>
    /// Gestionnaire centralisé des transitions entre scènes.
    /// Crée automatiquement un Canvas overlay noir qui fade in/out,
    /// et fade le son global en même temps.
    /// 
    /// Appeler SceneTransitionManager.LoadScene(buildIndex) partout.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        private static SceneTransitionManager _instance;

        [Header("Fade")]
        [SerializeField, Tooltip("Durée du fade out avant de charger la scène (secondes).")]
        private float _fadeOutDuration = 2f;

        [SerializeField, Tooltip("Durée du fade in après avoir chargé la scène (secondes).")]
        private float _fadeInDuration = 2f;

        // =======================================================================

        private Canvas _canvas;
        private Image _fadeImage;
        private bool _isTransitioning;

        // =======================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject("[SceneTransitionManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneTransitionManager>();
            _instance.CreateCanvas();
        }

        private void CreateCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<GraphicRaycaster>();

            _fadeImage = new GameObject("FadeImage").AddComponent<Image>();
            _fadeImage.transform.SetParent(transform, false);
            _fadeImage.rectTransform.anchorMin = Vector2.zero;
            _fadeImage.rectTransform.anchorMax = Vector2.one;
            _fadeImage.rectTransform.sizeDelta = Vector2.zero;
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
            _fadeImage.raycastTarget = false;
        }

        private void Start()
        {
            // Fade in au démarrage (première scène)
            StartCoroutine(FadeInRoutine());
        }

        // =======================================================================
        // Public API
        // =======================================================================

        /// <summary>
        /// Charge une scène avec un fade to black (visuel + audio) puis fade from black.
        /// </summary>
        public static void LoadScene(int buildIndex)
        {
            if (_instance == null)
            {
                SceneManager.LoadScene(buildIndex);
                return;
            }

            _instance.StartCoroutine(_instance.TransitionRoutine(buildIndex));
        }

        // =======================================================================
        // Routines
        // =======================================================================

        private IEnumerator TransitionRoutine(int buildIndex)
        {
            if (_isTransitioning) yield break;
            _isTransitioning = true;

            // 1. Fade to black (visuel + audio)
            yield return StartCoroutine(FadeRoutine(0f, 1f, _fadeOutDuration));

            // 2. Charger la scène
            var op = SceneManager.LoadSceneAsync(buildIndex);
            if (op != null)
            {
                op.allowSceneActivation = true;
                while (!op.isDone)
                    yield return null;
            }
            else
            {
                SceneManager.LoadScene(buildIndex);
            }

            // 3. Fade from black (visuel + audio)
            yield return StartCoroutine(FadeRoutine(1f, 0f, _fadeInDuration));

            _isTransitioning = false;
        }

        private IEnumerator FadeInRoutine()
        {
            yield return StartCoroutine(FadeRoutine(1f, 0f, _fadeInDuration));
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            _fadeImage.raycastTarget = true; // bloque les clics pendant le fade

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Visuel
                float alpha = Mathf.Lerp(from, to, t);
                _fadeImage.color = new Color(0f, 0f, 0f, alpha);

                // Audio global
                AudioListener.volume = Mathf.Lerp(1f - from, 1f - to, t);

                yield return null;
            }

            _fadeImage.color = new Color(0f, 0f, 0f, to);
            AudioListener.volume = 1f - to;
            _fadeImage.raycastTarget = false;
        }
    }
}
