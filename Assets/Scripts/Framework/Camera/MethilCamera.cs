using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Framework.Camera
{
    /// <summary>
    /// Composant à attacher à la caméra principale.
    /// Force un ratio d'affichage fixe (16:9, 4:3, etc.) avec letterboxing/pillarboxing automatique.
    /// 
    /// Crée automatiquement une caméra de fond noir pour les barres.
    /// S'adapte à chaque changement de résolution (plein écran, redimensionnement fenêtre).
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    [DisallowMultipleComponent]
    public class MethilCamera : MonoBehaviour
    {
        /// <summary>Ratios pré-définis.</summary>
        public enum AspectRatioPreset
        {
            [InspectorName("16:9 — Standard")]
            SixteenNine,
            
            [InspectorName("4:3 — Classique")]
            FourThree,
            
            [InspectorName("21:9 — UltraWide")]
            TwentyOneNine,
            
            [InspectorName("16:10 — Écran large")]
            SixteenTen,
            
            [InspectorName("1:1 — Carré")]
            Square,
            
            [InspectorName("Personnalisé")]
            Custom
        }
        
        // =======================================================================
        
        [Header("Ratio d'affichage")]
        [SerializeField, Tooltip("Ratio cible. 'Personnalisé' permet de définir une valeur libre.")]
        private AspectRatioPreset _preset = AspectRatioPreset.SixteenNine;
        
        [SerializeField, Tooltip("Ratio personnalisé (largeur / hauteur). Utilisé uniquement si Preset = Personnalisé.")]
        private float _customRatio = 16f / 9f;
        
        [SerializeField, Tooltip("Couleur des barres de letterbox/pillarbox.")]
        private Color _barColor = Color.black;
        
        [Header("Transition LookAt")]
        [SerializeField, Tooltip("Courbe d'easing du LookAt smooth. X = progression 0→1, Y = valeur 0→1.\n" +
                                 "Par défaut : ease-out cubique (démarrage rapide, arrivée très douce).")]
        private AnimationCurve _smoothCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Human Look At")]
        [SerializeField, Tooltip("Durée par défaut du regard humain (secondes).")]
        private float _humanLookDuration = 1.5f;
        
        [SerializeField, Tooltip("Dépassement de la cible avant correction (0.05 = 5% au-delà).")]
        [Range(0f, 0.15f)]
        private float _humanLookOvershoot = 0.05f;
        
        [SerializeField, Tooltip("Délai de réaction minimum avant de commencer à tourner.")]
        private float _humanLookMinDelay = 0.1f;
        
        [SerializeField, Tooltip("Délai de réaction maximum avant de commencer à tourner.")]
        private float _humanLookMaxDelay = 0.2f;
        
        // =======================================================================
        
        private UnityEngine.Camera _gameCamera;
        private UnityEngine.Camera _barCamera;
        private int                _lastWidth;
        private int                _lastHeight;
        
        /// <summary>Ratio effectif actuellement utilisé.</summary>
        public float CurrentRatio => _preset == AspectRatioPreset.Custom ? _customRatio : GetPresetRatio(_preset);
        
        // =======================================================================
        
        private void Awake()
        {
            _gameCamera = GetComponent<UnityEngine.Camera>();
            CreateBarCamera();
            ForceUpdate();
            
            // Si la courbe est laissée par défaut (linéaire 0→1), on met une ease-out cubique
            if (_smoothCurve.keys.Length <= 2)
            {
                _smoothCurve = new AnimationCurve(
                    new Keyframe(0f, 0f, 0f, 0f),
                    new Keyframe(1f, 1f, 0f, 0f)
                );
                // Force une interpolation très douce à l'arrivée (smooth tangents)
                _smoothCurve.SmoothTangents(0, 0f);
                _smoothCurve.SmoothTangents(1, 8f); // tangente forte → freinage marqué à la fin
            }
        }
        
        private void LateUpdate()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
                ForceUpdate();
        }
        
        // =======================================================================
        
        /// <summary>
        /// Force la mise à jour immédiate du viewport (appelé automatiquement, mais peut être appelé manuellement).
        /// </summary>
        [ContextMenu("Force Update")]
        public void ForceUpdate()
        {
            _lastWidth  = Screen.width;
            _lastHeight = Screen.height;
            
            float target  = CurrentRatio;
            float current = (float)Screen.width / Screen.height;
            
            if (current > target)
            {
                // Pillarboxing : écran trop large → barres gauche/droite
                float scale = target / current;
                float inset = (1f - scale) / 2f;
                _gameCamera.rect = new Rect(inset, 0f, scale, 1f);
            }
            else
            {
                // Letterboxing : écran trop haut → barres haut/bas
                float scale = current / target;
                float inset = (1f - scale) / 2f;
                _gameCamera.rect = new Rect(0f, inset, 1f, scale);
            }
        }
        
        // =======================================================================
        
        /// <summary>Retourne le ratio numérique pour un preset donné.</summary>
        public static float GetPresetRatio(AspectRatioPreset preset)
        {
            return preset switch
            {
                AspectRatioPreset.SixteenNine    => 16f / 9f,
                AspectRatioPreset.FourThree      => 4f  / 3f,
                AspectRatioPreset.TwentyOneNine  => 21f / 9f,
                AspectRatioPreset.SixteenTen     => 16f / 10f,
                AspectRatioPreset.Square         => 1f,
                _                                => 16f / 9f
            };
        }
        
        /// <summary>
        /// Crée une caméra de fond dédiée aux barres noires.
        /// Plein écran, couleur unie, aucun objet rendu.
        /// </summary>
        private void CreateBarCamera()
        {
            var barGo = new GameObject("[MethilCamera] Bar Camera");
            barGo.transform.SetParent(transform);
            barGo.hideFlags = HideFlags.NotEditable;
            
            _barCamera = barGo.AddComponent<UnityEngine.Camera>();
            _barCamera.clearFlags      = CameraClearFlags.SolidColor;
            _barCamera.backgroundColor = _barColor;
            _barCamera.cullingMask     = 0;
            _barCamera.depth           = _gameCamera.depth - 1f;
            _barCamera.useOcclusionCulling = false;
            _barCamera.allowHDR        = false;
            _barCamera.allowMSAA       = false;
            _barCamera.rect            = new Rect(0f, 0f, 1f, 1f);
            
            var barData = barGo.AddComponent<UniversalAdditionalCameraData>();
            barData.renderPostProcessing = false;
            barData.requiresColorTexture = false;
            barData.requiresDepthTexture = false;
            barData.renderShadows        = false;
            barData.antialiasing         = AntialiasingMode.None;
            barData.stopNaN              = false;
            barData.dithering            = false;
            
            var audioListener = barGo.GetComponent<AudioListener>();
            if (audioListener != null)
                Destroy(audioListener);
        }
        
        // =======================================================================
        // LookAt
        // =======================================================================
        
        /// <summary>
        /// Oriente instantanément la caméra pour regarder la cible.
        /// </summary>
        /// <param name="target">Le GameObject à regarder.</param>
        public void LookAt(GameObject target)
        {
            if (target == null || _gameCamera == null) return;
            _gameCamera.transform.LookAt(target.transform);
        }
        
        /// <summary>
        /// Oriente la caméra vers une position monde.
        /// </summary>
        /// <param name="worldPosition">La position à regarder.</param>
        public void LookAt(Vector3 worldPosition)
        {
            if (_gameCamera == null) return;
            _gameCamera.transform.LookAt(worldPosition);
        }
        
        /// <summary>
        /// Oriente progressivement la caméra vers la cible (coroutine).
        /// </summary>
        /// <param name="target">Le GameObject à regarder.</param>
        /// <param name="duration">Durée de la transition en secondes.</param>
        public void LookAtSmooth(GameObject target, float duration)
        {
            if (target == null || _gameCamera == null) return;
            StartCoroutine(_lookAtRoutine(target.transform, duration));
        }
        
        /// <summary>
        /// Oriente progressivement la caméra vers une position monde (coroutine).
        /// </summary>
        /// <param name="worldPosition">La position à regarder.</param>
        /// <param name="duration">Durée de la transition en secondes.</param>
        public void LookAtSmooth(Vector3 worldPosition, float duration)
        {
            if (_gameCamera == null) return;
            StartCoroutine(_lookAtRoutine(worldPosition, duration));
        }
        
        private IEnumerator _lookAtRoutine(Transform target, float duration)
        {
            Quaternion from = _gameCamera.transform.rotation;
            Vector3 dir = (target.position - _gameCamera.transform.position).normalized;
            Quaternion to = Quaternion.LookRotation(dir);
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = _smoothCurve.Evaluate(elapsed / duration);
                _gameCamera.transform.rotation = Quaternion.Lerp(from, to, t);
                yield return null;
            }
            
            _gameCamera.transform.rotation = to;
        }
        
        private IEnumerator _lookAtRoutine(Vector3 worldPosition, float duration)
        {
            Quaternion from = _gameCamera.transform.rotation;
            Vector3 dir = (worldPosition - _gameCamera.transform.position).normalized;
            Quaternion to = Quaternion.LookRotation(dir);
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = _smoothCurve.Evaluate(elapsed / duration);
                _gameCamera.transform.rotation = Quaternion.Lerp(from, to, t);
                yield return null;
            }
            
            _gameCamera.transform.rotation = to;
        }
        
        /// <summary>
        /// Oriente progressivement la caméra vers la cible en 3 secondes.
        /// </summary>
        /// <param name="target">Le GameObject à regarder.</param>
        public void LookAtSmooth3Seconds(GameObject target) => LookAtSmooth(target, 3f);
        
        /// <summary>
        /// Oriente progressivement la caméra vers une position monde en 3 secondes.
        /// </summary>
        /// <param name="worldPosition">La position à regarder.</param>
        public void LookAtSmooth3Seconds(Vector3 worldPosition) => LookAtSmooth(worldPosition, 3f);

        // =======================================================================
        // Human Look At
        // =======================================================================

        /// <summary>
        /// Regard "humain" : délai aléatoire, saccade rapide avec léger overshoot, puis correction douce.
        /// </summary>
        public void LookAtHuman(GameObject target)
            => LookAtHuman(target, _humanLookDuration);

        /// <summary>
        /// Regard "humain" vers un GameObject avec durée personnalisée.
        /// </summary>
        public void LookAtHuman(GameObject target, float duration)
        {
            if (target == null || _gameCamera == null) return;
            StartCoroutine(_humanLookAtRoutine(target.transform.position, duration));
        }

        /// <summary>
        /// Regard "humain" vers une position monde (durée par défaut).
        /// </summary>
        public void LookAtHuman(Vector3 worldPosition)
            => LookAtHuman(worldPosition, _humanLookDuration);

        /// <summary>
        /// Regard "humain" vers une position monde avec durée personnalisée.
        /// </summary>
        public void LookAtHuman(Vector3 worldPosition, float duration)
        {
            if (_gameCamera == null) return;
            StartCoroutine(_humanLookAtRoutine(worldPosition, duration));
        }

        private IEnumerator _humanLookAtRoutine(Vector3 worldPosition, float duration)
        {
            // Délai de réaction aléatoire
            float delay = Random.Range(_humanLookMinDelay, _humanLookMaxDelay);
            yield return new WaitForSeconds(delay);

            Quaternion from = _gameCamera.transform.rotation;
            Vector3 dir = (worldPosition - _gameCamera.transform.position).normalized;
            Quaternion to = Quaternion.LookRotation(dir);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float humanT = HumanCurve(t);
                _gameCamera.transform.rotation = Quaternion.Slerp(from, to, humanT);
                yield return null;
            }

            _gameCamera.transform.rotation = to;
        }

        /// <summary>
        /// Courbe de mouvement humain : saccade rapide (0→70%) avec overshoot,
        /// puis correction douce (70%→100%) vers la cible exacte.
        /// </summary>
        private float HumanCurve(float t)
        {
            const float split = 0.7f;

            if (t < split)
            {
                // Phase 1 : saccade vers 1 + overshoot
                float phaseT = t / split;
                return (1f + _humanLookOvershoot) * SmoothStep(phaseT);
            }
            else
            {
                // Phase 2 : correction de l'overshoot vers 1.0
                float phaseT = (t - split) / (1f - split);
                float start = 1f + _humanLookOvershoot;
                return Mathf.Lerp(start, 1f, SmoothStep(phaseT));
            }
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        // =======================================================================
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && _gameCamera != null)
                ForceUpdate();
        }
#endif
    }
}
