using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Framework.UI.FX
{
    /// <summary>
    /// Per-character glitch effect for TMP text.
    /// Each character glitches independently — twitch, drift, corruption, waves.
    /// Supports grouped glitches (multiple adjacent chars triggered together),
    /// a color palette, and floating visual artefacts.
    /// Every subsystem can be toggled on/off.
    /// </summary>
    public class GlitchText : MonoBehaviour
    {
        // ═══════════════════════════════════════
        // Toggles
        // ═══════════════════════════════════════

        [Header("── Toggles ──")]
        [SerializeField] bool enableMicroJitter = true;
        [SerializeField] bool enableVerticalCreep = true;
        [SerializeField] bool enableTwitch = true;
        [SerializeField] bool enableDrift = true;
        [SerializeField] bool enableCorrupt = true;
        [SerializeField] bool enableWave = true;
        [SerializeField] bool enableGroupGlitch = false;
        [SerializeField] bool enableArtefacts = false;

        // ═══════════════════════════════════════
        // Micro-jitter
        // ═══════════════════════════════════════

        [Header("Micro-jitter (always active)")]
        [SerializeField, Range(0f, 3f)] float microJitter = 0.6f;
        [SerializeField, Range(0f, 20f)] float microJitterSpeed = 8f;

        // ═══════════════════════════════════════
        // Twitch
        // ═══════════════════════════════════════

        [Header("Twitch (quick snap glitch)")]
        [SerializeField, Range(0f, 1f)] float twitchChancePerSecond = 0.6f;
        [SerializeField, Range(0f, 30f)] float twitchDisplacement = 12f;

        // ═══════════════════════════════════════
        // Drift
        // ═══════════════════════════════════════

        [Header("Drift (slow unsettling float)")]
        [SerializeField, Range(0f, 1f)] float driftChancePerSecond = 0.08f;
        [SerializeField, Range(0f, 15f)] float driftDistance = 6f;
        [SerializeField, Range(0.2f, 5f)] float driftDuration = 1.5f;

        // ═══════════════════════════════════════
        // Corruption
        // ═══════════════════════════════════════

        [Header("Corruption (intense burst)")]
        [SerializeField, Range(0f, 1f)] float corruptChancePerSecond = 0.04f;
        [SerializeField, Range(0f, 50f)] float corruptDisplacement = 35f;
        [SerializeField, Range(0f, 1f)] float corruptColorSplit = 0.8f;

        // ═══════════════════════════════════════
        // Wave
        // ═══════════════════════════════════════

        [Header("Wave (glitch propagation across characters)")]
        [SerializeField, Range(0f, 1f)] float waveChancePerSecond = 0.15f;
        [SerializeField, Range(0.01f, 0.3f)] float waveStepDelay = 0.04f;
        [SerializeField, Range(0f, 40f)] float waveDisplacement = 20f;

        // ═══════════════════════════════════════
        // Glitch frequency
        // ═══════════════════════════════════════

        [Header("Glitch frequency")]
        [SerializeField, Range(0.5f, 30f), Tooltip("Minimum seconds between two glitches for a character")]
        float minGlitchCooldown = 2f;
        [SerializeField, Range(0.5f, 30f), Tooltip("Maximum seconds between two glitches for a character")]
        float maxGlitchCooldown = 6f;

        // ═══════════════════════════════════════
        // Group glitch
        // ═══════════════════════════════════════

        [Header("Group glitch (adjacent chars)")]
        [SerializeField, Range(0f, 1f), Tooltip("Chance that a glitch becomes a group glitch (0 = never, 1 = always)")]
        float groupGlitchChance = 0.4f;
        [SerializeField, Range(1, 10)] int minGroupSize = 1;
        [SerializeField, Range(1, 10)] int maxGroupSize = 3;

        // ═══════════════════════════════════════
        // Color palette
        // ═══════════════════════════════════════

        [Header("Color scrambling")]
        [SerializeField, Range(0f, 1f)] float colorScrambleAmount = 0.5f;
        [SerializeField, Tooltip("If set, glitch colors are picked exclusively from this palette.")]
        Color[] glitchPalette;

        // ═══════════════════════════════════════
        // Vertical creep
        // ═══════════════════════════════════════

        [Header("Vertical creep")]
        [SerializeField, Range(0f, 3f)] float verticalCreep = 1.2f;
        [SerializeField, Range(0.01f, 2f)] float verticalCreepSpeed = 0.3f;

        // ═══════════════════════════════════════
        // Artefacts
        // ═══════════════════════════════════════

        [Header("Artefacts (floating glitch rectangles)")]
        [SerializeField, Range(1, 30)] int maxArtefacts = 10;
        [SerializeField, Range(2f, 100f)] float artefactMinSize = 4f;
        [SerializeField, Range(2f, 200f)] float artefactMaxSize = 30f;
        [SerializeField, Range(0.05f, 2f)] float artefactMinDuration = 0.3f;
        [SerializeField, Range(0.05f, 3f)] float artefactMaxDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] float artefactSpawnChance = 0.5f;

        // ═══════════════════════════════════════
        // Internal state
        // ═══════════════════════════════════════

        TMP_Text _text;
        RectTransform _textRect;
        Canvas _canvas;
        bool _hasTextChanged;

        struct CharData
        {
            public int meshIndex;
            public int vertexBase;
            public Vector3[] restVerts;
            public Color32 restColor;
            public Vector3 restCenter;
            public float glitchTimer;
            public GlitchState state;
            public float stateTimer;
            public float stateDuration;
            public Vector3 offset;
            public Color32 colorShift;
            public Vector3 driftTarget;
            public float seedA;
            public float seedB;
        }

        enum GlitchState { Idle, Twitch, Drift, Corrupt, Wave }

        List<CharData> _chars = new();
        bool _isWaving;
        System.Random _rng;

        // Artefact pool
        List<Artefact> _artefactPool = new();
        int _artefactPoolIndex;

        class Artefact
        {
            public GameObject go;
            public Image image;
            public RectTransform rt;
            public float lifetime;
            public float maxLifetime;
        }

        // ═══════════════════════════════════════
        // Unity lifecycle
        // ═══════════════════════════════════════

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _textRect = _text.rectTransform;
            _canvas = GetComponentInParent<Canvas>();
            _rng = new System.Random(Random.Range(1, int.MaxValue));
        }

        void Start()
        {
            CacheCharacters();
            BuildArtefactPool();
        }

        void OnEnable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        void OnDestroy()
        {
            foreach (var a in _artefactPool)
                if (a.go != null) Destroy(a.go);
            _artefactPool.Clear();
        }

        void OnTextChanged(Object obj)
        {
            if (obj == _text) _hasTextChanged = true;
        }

        void Update()
        {
            if (_text == null) return;

            if (_hasTextChanged)
            {
                CacheCharacters();
                _hasTextChanged = false;
            }

            float dt = Time.deltaTime;

            if (enableWave && !_isWaving && _chars.Count > 0 && Random.value < waveChancePerSecond * dt)
                StartCoroutine(WaveRoutine());

            ProcessAllCharacters(dt);
            ApplyAllVertices();

            if (enableArtefacts)
                UpdateArtefacts(dt);
        }

        // ═══════════════════════════════════════
        // Caching
        // ═══════════════════════════════════════

        void CacheCharacters()
        {
            _text.ForceMeshUpdate();
            var ti = _text.textInfo;
            _chars.Clear();

            for (int i = 0; i < ti.characterCount; i++)
            {
                var ci = ti.characterInfo[i];
                if (!ci.isVisible) continue;

                int meshIdx = ci.materialReferenceIndex;
                int vertBase = ci.vertexIndex;
                var mesh = ti.meshInfo[meshIdx];

                var cd = new CharData
                {
                    meshIndex = meshIdx,
                    vertexBase = vertBase,
                    restVerts = new Vector3[4],
                    restColor = mesh.colors32 != null && mesh.colors32.Length > vertBase
                        ? mesh.colors32[vertBase]
                        : new Color32(255, 255, 255, 255),
                    restCenter = Vector3.zero,
                    glitchTimer = RandomRange(0.1f, 2f),
                    state = GlitchState.Idle,
                    seedA = (float)_rng.NextDouble(),
                    seedB = (float)_rng.NextDouble(),
                };

                for (int v = 0; v < 4; v++)
                {
                    cd.restVerts[v] = mesh.vertices[vertBase + v];
                    cd.restCenter += cd.restVerts[v];
                }
                cd.restCenter *= 0.25f;
                cd.colorShift = cd.restColor;
                _chars.Add(cd);
            }
        }

        // ═══════════════════════════════════════
        // Per-frame processing
        // ═══════════════════════════════════════

        void ProcessAllCharacters(float dt)
        {
            if (_chars.Count == 0) return;
            var ti = _text.textInfo;

            for (int c = 0; c < _chars.Count; c++)
            {
                var cd = _chars[c];
                var mesh = ti.meshInfo[cd.meshIndex];

                cd.glitchTimer -= dt;
                cd.stateTimer -= dt;

                switch (cd.state)
                {
                    case GlitchState.Idle:
                        ProcessIdle(ref cd, dt, c);
                        break;
                    case GlitchState.Twitch:
                        ProcessTwitch(ref cd, dt);
                        break;
                    case GlitchState.Drift:
                        ProcessDrift(ref cd, dt);
                        break;
                    case GlitchState.Corrupt:
                        ProcessCorrupt(ref cd, dt);
                        break;
                    case GlitchState.Wave:
                        ProcessWave(ref cd, dt);
                        break;
                }

                ApplyCharToMesh(cd, mesh);
                _chars[c] = cd;
            }
        }

        // ═══════════════════════════════════════
        // Idle
        // ═══════════════════════════════════════

        void ProcessIdle(ref CharData cd, float dt, int charIndex)
        {
            float jx = 0f, jy = 0f, creep = 0f;

            if (enableMicroJitter)
            {
                jx = (Mathf.PerlinNoise(Time.time * microJitterSpeed + cd.seedA * 100f, charIndex * 0.7f) - 0.5f) * 2f * microJitter;
                jy = (Mathf.PerlinNoise(Time.time * microJitterSpeed * 1.3f + cd.seedB * 100f, charIndex * 0.7f + 50f) - 0.5f) * 2f * microJitter;
            }

            if (enableVerticalCreep)
                creep = Mathf.Sin(Time.time * verticalCreepSpeed + cd.seedA * 10f) * verticalCreep;

            cd.offset = new Vector3(jx, jy + creep, 0f);
            cd.colorShift = cd.restColor;

            if (cd.glitchTimer <= 0f)
            {
                if (enableGroupGlitch && maxGroupSize > 1 && (float)_rng.NextDouble() < groupGlitchChance)
                {
                    int size = _rng.Next(minGroupSize, maxGroupSize + 1);
                    TriggerGroupGlitch(charIndex, size);
                }
                else
                {
                    PickNextGlitchState(ref cd);
                }
            }
        }

        // ═══════════════════════════════════════
        // Group glitch
        // ═══════════════════════════════════════

        void TriggerGroupGlitch(int startIndex, int count)
        {
            int end = Mathf.Min(startIndex + count, _chars.Count);
            GlitchState chosenState = GlitchState.Twitch;
            float chosenDuration = 0.06f;
            bool foundState = false;

            // Decide the state once for the whole group
            float twitchW = enableTwitch ? twitchChancePerSecond : 0f;
            float driftW = enableDrift ? driftChancePerSecond : 0f;
            float corruptW = enableCorrupt ? corruptChancePerSecond : 0f;
            float totalW = twitchW + driftW + corruptW;

            if (totalW > 0f)
            {
                float roll = (float)_rng.NextDouble() * totalW;
                if (roll < corruptW)
                {
                    chosenState = GlitchState.Corrupt;
                    chosenDuration = RandomRange(0.06f, 0.25f);
                }
                else if (roll < corruptW + driftW)
                {
                    chosenState = GlitchState.Drift;
                    chosenDuration = driftDuration * RandomRange(0.5f, 1.5f);
                }
                else
                {
                    chosenState = GlitchState.Twitch;
                    chosenDuration = RandomRange(0.03f, 0.12f);
                }
                foundState = true;
            }

            for (int i = startIndex; i < end; i++)
            {
                var cd = _chars[i];
                if (cd.state != GlitchState.Idle) continue;

                if (foundState)
                {
                    cd.state = chosenState;
                    cd.stateDuration = chosenDuration;
                    cd.stateTimer = chosenDuration;
                }
                else
                {
                    cd.glitchTimer = RandomRange(minGlitchCooldown, maxGlitchCooldown);
                }

                cd.offset = chosenState == GlitchState.Corrupt
                    ? RandomInsideCircle(corruptDisplacement)
                    : chosenState == GlitchState.Drift
                        ? Vector3.zero
                        : RandomInsideCircle(twitchDisplacement);

                cd.driftTarget = chosenState == GlitchState.Drift
                    ? RandomInsideCircle(driftDistance)
                    : Vector3.zero;

                _chars[i] = cd;
            }
        }

        // ═══════════════════════════════════════
        // State picker (single character)
        // ═══════════════════════════════════════

        void PickNextGlitchState(ref CharData cd)
        {
            if (_isWaving) return;

            float twitchW = enableTwitch ? twitchChancePerSecond : 0f;
            float driftW = enableDrift ? driftChancePerSecond : 0f;
            float corruptW = enableCorrupt ? corruptChancePerSecond : 0f;
            float totalW = twitchW + driftW + corruptW;

            if (totalW <= 0f)
            {
                cd.state = GlitchState.Idle;
                cd.glitchTimer = RandomRange(minGlitchCooldown, maxGlitchCooldown);
                return;
            }

            float roll = (float)_rng.NextDouble() * totalW;

            if (roll < corruptW)
            {
                cd.state = GlitchState.Corrupt;
                cd.stateDuration = RandomRange(0.06f, 0.25f);
                cd.stateTimer = cd.stateDuration;
                cd.offset = RandomInsideCircle(corruptDisplacement);
            }
            else if (roll < corruptW + driftW)
            {
                cd.state = GlitchState.Drift;
                cd.stateDuration = driftDuration * RandomRange(0.5f, 1.5f);
                cd.stateTimer = cd.stateDuration;
                cd.driftTarget = RandomInsideCircle(driftDistance);
                cd.offset = Vector3.zero;
            }
            else
            {
                cd.state = GlitchState.Twitch;
                cd.stateDuration = RandomRange(0.03f, 0.12f);
                cd.stateTimer = cd.stateDuration;
                cd.offset = RandomInsideCircle(twitchDisplacement);
            }
        }

        // ═══════════════════════════════════════
        // Twitch
        // ═══════════════════════════════════════

        void ProcessTwitch(ref CharData cd, float dt)
        {
            if (cd.stateTimer <= 0f)
            {
                cd.state = GlitchState.Idle;
                cd.glitchTimer = RandomRange(minGlitchCooldown, maxGlitchCooldown);
                cd.offset = Vector3.zero;
                cd.colorShift = cd.restColor;
            }
            else
            {
                float fade = cd.stateTimer / cd.stateDuration;
                cd.offset *= fade;
                cd.colorShift = PickGlitchColor(cd.restColor, colorScrambleAmount * fade);
            }
        }

        // ═══════════════════════════════════════
        // Drift
        // ═══════════════════════════════════════

        void ProcessDrift(ref CharData cd, float dt)
        {
            float t = 1f - (cd.stateTimer / cd.stateDuration);

            if (cd.stateTimer <= 0f)
            {
                cd.state = GlitchState.Idle;
                cd.glitchTimer = RandomRange(minGlitchCooldown, maxGlitchCooldown);
                cd.offset = Vector3.zero;
                cd.colorShift = cd.restColor;
            }
            else
            {
                float driftCurve = t < 0.7f
                    ? Mathf.SmoothStep(0f, 1f, t / 0.7f)
                    : Mathf.SmoothStep(1f, 0f, (t - 0.7f) / 0.3f);

                cd.offset = Vector3.Lerp(Vector3.zero, cd.driftTarget, driftCurve);

                float desat = driftCurve * 0.3f;
                cd.colorShift = DesaturateColor(cd.restColor, desat);
            }
        }

        // ═══════════════════════════════════════
        // Corruption
        // ═══════════════════════════════════════

        void ProcessCorrupt(ref CharData cd, float dt)
        {
            if (cd.stateTimer <= 0f)
            {
                cd.state = GlitchState.Idle;
                cd.glitchTimer = RandomRange(minGlitchCooldown, maxGlitchCooldown);
                cd.offset = Vector3.zero;
                cd.colorShift = cd.restColor;
            }
            else
            {
                float intensity = cd.stateTimer / cd.stateDuration;
                cd.offset = RandomInsideCircle(corruptDisplacement * intensity);
                cd.colorShift = PickGlitchColor(cd.restColor, corruptColorSplit * intensity);

                if ((float)_rng.NextDouble() < 0.3f)
                    cd.offset.y *= 1.5f;

                // Spawn artefacts during corruption
                if (enableArtefacts && (float)_rng.NextDouble() < artefactSpawnChance * intensity)
                    SpawnArtefact();
            }
        }

        // ═══════════════════════════════════════
        // Wave
        // ═══════════════════════════════════════

        void ProcessWave(ref CharData cd, float dt)
        {
            if (cd.stateTimer <= 0f)
            {
                cd.state = GlitchState.Idle;
                cd.glitchTimer = RandomRange(minGlitchCooldown, maxGlitchCooldown);
                cd.offset = Vector3.zero;
                cd.colorShift = cd.restColor;
            }
            else
            {
                float fade = cd.stateTimer / cd.stateDuration;
                cd.offset = new Vector3(
                    (Mathf.PerlinNoise(Time.time * 30f, cd.seedA) - 0.5f) * waveDisplacement * fade,
                    (Mathf.PerlinNoise(Time.time * 25f, cd.seedB) - 0.5f) * waveDisplacement * 0.5f * fade,
                    0f);
                cd.colorShift = PickGlitchColor(cd.restColor, 0.6f * fade);

                // Spawn artefacts during wave
                if (enableArtefacts && (float)_rng.NextDouble() < artefactSpawnChance * fade)
                    SpawnArtefact();
            }
        }

        // ═══════════════════════════════════════
        // Apply to mesh
        // ═══════════════════════════════════════

        void ApplyCharToMesh(CharData cd, TMP_MeshInfo mesh)
        {
            var vertices = mesh.vertices;
            var colors = mesh.colors32;

            for (int v = 0; v < 4; v++)
            {
                int idx = cd.vertexBase + v;
                if (idx >= vertices.Length) continue;
                vertices[idx] = cd.restVerts[v] + cd.offset;
            }

            if (colors != null && colors.Length > cd.vertexBase)
            {
                for (int v = 0; v < 4; v++)
                {
                    int idx = cd.vertexBase + v;
                    if (idx >= colors.Length) continue;
                    colors[idx] = cd.colorShift;
                }
            }
        }

        void ApplyAllVertices()
        {
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        // ═══════════════════════════════════════
        // Wave coroutine
        // ═══════════════════════════════════════

        public void TriggerWave()
        {
            if (_chars.Count == 0 || _isWaving) return;
            StartCoroutine(WaveRoutine());
        }

        System.Collections.IEnumerator WaveRoutine()
        {
            _isWaving = true;
            int dir = _rng.Next(0, 3);

            for (int i = 0; i < _chars.Count; i++)
            {
                int idx = dir switch
                {
                    0 => i,
                    1 => _chars.Count - 1 - i,
                    _ => (i % 2 == 0)
                        ? _chars.Count / 2 + i / 2
                        : _chars.Count / 2 - 1 - i / 2
                };

                if (idx >= 0 && idx < _chars.Count)
                {
                    var cd = _chars[idx];
                    cd.state = GlitchState.Wave;
                    cd.stateDuration = RandomRange(0.08f, 0.2f);
                    cd.stateTimer = cd.stateDuration;
                    _chars[idx] = cd;
                }

                yield return new WaitForSeconds(waveStepDelay);
            }

            yield return new WaitForSeconds(0.3f);
            _isWaving = false;
        }

        // ═══════════════════════════════════════
        // Artefact pool
        // ═══════════════════════════════════════

        void BuildArtefactPool()
        {
            Transform parent = _canvas != null ? _canvas.transform : transform;
            var palette = GetArtefactPalette();

            for (int i = 0; i < maxArtefacts; i++)
            {
                var go = new GameObject("artefact_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                go.layer = parent.gameObject.layer;

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = palette.Length > 0
                    ? palette[_rng.Next(palette.Length)]
                    : new Color(Random.value, Random.value, Random.value, 1f);

                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                go.SetActive(false);

                _artefactPool.Add(new Artefact { go = go, image = img, rt = rt, lifetime = 0f, maxLifetime = 0f });
            }
        }

        void SpawnArtefact()
        {
            if (_artefactPool.Count == 0) return;

            // Find an inactive artefact
            int start = _artefactPoolIndex;
            Artefact a;
            do
            {
                _artefactPoolIndex = (_artefactPoolIndex + 1) % _artefactPool.Count;
                a = _artefactPool[_artefactPoolIndex];
            } while (a.go.activeSelf && _artefactPoolIndex != start);

            if (a.go.activeSelf) return; // all busy

            // Get text bounds in canvas space
            Vector3[] corners = new Vector3[4];
            _textRect.GetWorldCorners(corners);

            RectTransform canvasRt = _canvas != null ? _canvas.transform as RectTransform : null;
            Vector2 canvasSize = canvasRt != null ? canvasRt.rect.size : new Vector2(Screen.width, Screen.height);

            // Convert world corners to canvas-local
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            for (int i = 0; i < 4; i++)
            {
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt, RectTransformUtility.WorldToScreenPoint(cam, corners[i]), cam, out local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            // Random position near or on the text
            float expand = artefactMaxSize * 2f;
            float x = RandomRange(min.x - expand, max.x + expand);
            float y = RandomRange(min.y - expand, max.y + expand);

            float w = RandomRange(artefactMinSize, artefactMaxSize);
            float h = RandomRange(artefactMinSize * 0.5f, artefactMaxSize * 0.3f); // artefacts are wide & flat (scanline vibe)

            a.rt.anchoredPosition = new Vector2(x, y);
            a.rt.sizeDelta = new Vector2(w, h);

            // Randomize color from palette or random
            var palette = GetArtefactPalette();
            Color col = palette.Length > 0
                ? palette[_rng.Next(palette.Length)]
                : new Color(Random.value, Random.value, Random.value, 0.8f);
            col.a = RandomRange(0.6f, 1f);
            a.image.color = col;

            a.maxLifetime = RandomRange(artefactMinDuration, artefactMaxDuration);
            a.lifetime = a.maxLifetime;

            // Force render on top of everything
            a.go.transform.SetAsLastSibling();
            a.go.SetActive(true);
        }

        void UpdateArtefacts(float dt)
        {
            for (int i = 0; i < _artefactPool.Count; i++)
            {
                var a = _artefactPool[i];
                if (!a.go.activeSelf) continue;

                a.lifetime -= dt;
                if (a.lifetime <= 0f)
                {
                    a.go.SetActive(false);
                    continue;
                }

                // Stay fully visible most of the time, fade out at the end
                float t = a.lifetime / a.maxLifetime;
                float alpha = t > 0.3f ? 1f : t / 0.3f; // fade only in last 30%
                var col = a.image.color;
                col.a = alpha * 0.85f;
                a.image.color = col;

                // Slight horizontal drift
                a.rt.anchoredPosition += new Vector2(dt * 40f * (i % 2 == 0 ? 1 : -1), 0f);
            }
        }

        Color[] GetArtefactPalette()
        {
            // Use the glitch palette for artefacts too if defined
            if (glitchPalette != null && glitchPalette.Length > 0)
                return glitchPalette;
            return System.Array.Empty<Color>();
        }

        // ═══════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════

        float RandomRange(float min, float max) =>
            min + (float)_rng.NextDouble() * (max - min);

        Vector3 RandomInsideCircle(float radius)
        {
            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)_rng.NextDouble()) * radius;
            return new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
        }

        Color32 PickGlitchColor(Color32 original, float intensity)
        {
            if (intensity <= 0.001f) return original;

            if (glitchPalette != null && glitchPalette.Length > 0)
            {
                Color picked = glitchPalette[_rng.Next(glitchPalette.Length)];
                return Color32.Lerp(original, picked, intensity);
            }

            return ScrambleColor(original, intensity);
        }

        Color32 ScrambleColor(Color32 original, float amount)
        {
            if (amount <= 0.001f) return original;
            return new Color32(
                (byte)Mathf.Clamp(original.r + (int)(((float)_rng.NextDouble() - 0.5f) * 2f * 255f * amount), 0, 255),
                (byte)Mathf.Clamp(original.g + (int)(((float)_rng.NextDouble() - 0.5f) * 2f * 255f * amount), 0, 255),
                (byte)Mathf.Clamp(original.b + (int)(((float)_rng.NextDouble() - 0.5f) * 2f * 255f * amount), 0, 255),
                original.a
            );
        }

        Color32 DesaturateColor(Color32 original, float amount)
        {
            float gray = original.r * 0.299f + original.g * 0.587f + original.b * 0.114f;
            return new Color32(
                (byte)Mathf.Lerp(original.r, gray, amount),
                (byte)Mathf.Lerp(original.g, gray, amount),
                (byte)Mathf.Lerp(original.b, gray, amount),
                original.a
            );
        }
    }
}
