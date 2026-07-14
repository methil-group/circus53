using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Core
{
    public class IrmaBehaviour : MonoBehaviour
    {
        public static IrmaBehaviour Instance { get; private set; }

        public enum LightShowMode
        {
            Off,
            Sequential,
            Flicker,
            Looping
        }

        [Header("Trigger")]
        [SerializeField] private Collider _triggerZone;

        [Header("Timeline")]
        [SerializeField] private PlayableDirector _timeline;
        [SerializeField] private bool _playOnce = true;

        [Header("Light Bulbs")]
        [SerializeField] private GameObject[] _bulbs;
        [SerializeField] private LightShowMode _mode = LightShowMode.Looping;

        [Header("Emission")]
        [SerializeField] private float _emissionIntensity = 3f;
        [SerializeField] private Color _emissionColor = Color.white;

        [Header("Point Lights")]
        [SerializeField] private float _pointLightIntensity = 1f;
        [SerializeField] private float _pointLightRange = 5f;
        [SerializeField] private Color _pointLightColor = Color.white;

        [Header("Timing")]
        [SerializeField] private float _lightDelay = 0.5f;
        [SerializeField] private float _flickerMinInterval = 0.05f;
        [SerializeField] private float _flickerMaxInterval = 0.3f;

        private bool _hasTriggered;
        private System.Collections.Generic.List<BulbState> _bulbStates;
        private Coroutine _showRoutine;

        private class BulbState
        {
            public GameObject go;
            public Material material;
            public Light pointLight;
            public bool on;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (_triggerZone != null && _triggerZone.gameObject != gameObject)
            {
                var proxy = _triggerZone.gameObject.AddComponent<IrmaTriggerProxy>();
                proxy.target = this;
            }

            SetupBulbs();
            _showRoutine = StartCoroutine(LightShowRoutine());
        }

        // ===================================================================
        // Public
        // ===================================================================

        /// <summary>Joue un tour de looping (chase + flicker) puis s'arrête.</summary>
        [ContextMenu("Play Looping Once")]
        public void PlayLoopingOnce()
        {
            if (_bulbStates == null || _bulbStates.Count == 0) return;
            if (_showRoutine != null) StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(LoopingOnceRoutine());
        }

        // ===================================================================

        internal void HandleTrigger(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (_playOnce && _hasTriggered) return;
            _hasTriggered = true;
            if (_timeline != null) _timeline.Play();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggerZone == null || _triggerZone.gameObject == gameObject)
                HandleTrigger(other);
        }

        private void SetupBulbs()
        {
            if (_bulbStates != null) return;
            _bulbStates = new System.Collections.Generic.List<BulbState>();
            foreach (var bulb in _bulbs)
            {
                if (bulb == null) continue;
                var state = new BulbState { go = bulb };
                var renderer = bulb.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    state.material = new Material(renderer.material);
                    state.material.DisableKeyword("_EMISSION");
                    renderer.material = state.material;
                }
                var lightGo = new GameObject("BulbPointLight");
                lightGo.transform.SetParent(bulb.transform);
                lightGo.transform.localPosition = Vector3.zero;
                lightGo.transform.localRotation = Quaternion.identity;
                state.pointLight = lightGo.AddComponent<Light>();
                state.pointLight.type = LightType.Point;
                state.pointLight.intensity = _pointLightIntensity;
                state.pointLight.range = _pointLightRange;
                state.pointLight.color = _pointLightColor;
                state.pointLight.enabled = false;
                state.on = false;
                _bulbStates.Add(state);
            }
        }

        private void SetBulb(BulbState state, bool on)
        {
            state.on = on;
            if (state.material != null)
            {
                if (on)
                {
                    state.material.EnableKeyword("_EMISSION");
                    state.material.SetColor("_EmissionColor", _emissionColor * _emissionIntensity);
                }
                else state.material.DisableKeyword("_EMISSION");
            }
            if (state.pointLight != null) state.pointLight.enabled = on;
        }

        private void AllBulbsOff()
        {
            foreach (var state in _bulbStates) SetBulb(state, false);
        }

        private System.Collections.IEnumerator LightShowRoutine()
        {
            switch (_mode)
            {
                case LightShowMode.Off:
                    AllBulbsOff();
                    yield break;

                case LightShowMode.Sequential:
                    while (true) yield return SequentialChaseRoutine();

                case LightShowMode.Flicker:
                    while (true) yield return FlickerRoutine();

                case LightShowMode.Looping:
                    while (true)
                    {
                        yield return SequentialChaseRoutine();
                        yield return FlickerRoutine();
                    }
            }
        }

        private System.Collections.IEnumerator LoopingOnceRoutine()
        {
            yield return SequentialChaseRoutine();
            yield return FlickerRoutine();
        }

        // Effet cirque : une seule ampoule allumee a la fois (chase)
        private System.Collections.IEnumerator SequentialChaseRoutine()
        {
            for (int i = 0; i < _bulbStates.Count; i++)
            {
                if (i > 0) SetBulb(_bulbStates[i - 1], false);
                SetBulb(_bulbStates[i], true);
                yield return new WaitForSeconds(_lightDelay);
            }
            // Eteint la derniere a la fin du tour
            if (_bulbStates.Count > 0) SetBulb(_bulbStates[_bulbStates.Count - 1], false);
        }

        private System.Collections.IEnumerator FlickerRoutine()
        {
            float duration = Random.Range(1.5f, 3f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                foreach (var state in _bulbStates) SetBulb(state, Random.value > 0.5f);
                float wait = Random.Range(_flickerMinInterval, _flickerMaxInterval);
                yield return new WaitForSeconds(wait);
                elapsed += wait;
            }
            foreach (var state in _bulbStates) SetBulb(state, true);
        }
    }

    public class IrmaTriggerProxy : MonoBehaviour
    {
        internal IrmaBehaviour target;
        private void OnTriggerEnter(Collider other) { if (target != null) target.HandleTrigger(other); }
    }
}
