using System.Collections;
using Core.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Core
{
    /// <summary>
    /// Porte : clic → vérifie si le joueur a la clé.
    /// - Sans clé : joue une chaîne de dialogues (texte + son).
    /// - Avec clé : charge la scène cible.
    /// Nécessite un Collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(AudioSource))]
    public class DoorInteraction : MonoBehaviour
    {
        [Header("Panneau de dialogue")]
        [SerializeField, Tooltip("GameObject contenant le TMP_Text, activé/désactivé.")]
        private GameObject _dialogPanel;

        [SerializeField, Tooltip("TMP_Text à remplir avec le texte du dialogue.")]
        private TMP_Text _dialogText;

        [Header("Dialogues — sans clé")]
        [SerializeField, Tooltip("Dialogues joués à la suite quand le joueur n'a pas la clé.")]
        private DialogEntry[] _lockedDialogues;

        [Header("Son global")]
        [SerializeField, Tooltip("Volume global pour les voix.")]
        [Range(0f, 1f)]
        private float _volume = 1f;

        [Header("Scène — avec clé")]
        [SerializeField, Tooltip("Build index de la scène à charger si le joueur a la clé.")]
        private int _targetSceneBuildIndex = 3;

        [Header("Place requise")]
        [SerializeField, Tooltip("Place dans laquelle le joueur doit se trouver pour pouvoir cliquer. Null = accessible depuis n'importe où.")]
        private Place _requiredPlace;

        [Header("Outline")]
        [SerializeField] private Outline _outline;
        [SerializeField] private Outline[] _extraOutlines;

        [Header("Settings")]
        [SerializeField, Tooltip("Si coché, jouable une seule fois (dialogues sans clé).")]
        private bool _playOnce;

        [SerializeField, Tooltip("Cooldown entre deux clics (secondes).")]
        private float _cooldown;

        [SerializeField, Tooltip("Si coché, bloque les déplacements pendant l'interaction.")]
        private bool _blockMovement = true;

        [Header("Typing Sound")]
        [SerializeField, Tooltip("Qui parle ? Other = autres personnages.")]
        private TypingVoice _typingVoice = TypingVoice.Other;

        // ===================================================================

        private AudioSource _audioSource;
        private AudioSource _typingAudioSource;
        private Coroutine _routine;
        private bool _isPlaying;
        private bool _wasHovered;
        private bool _wasAvailable;
        private bool _hasPlayedLocked;
        private Collider _collider;
        private float _lastInteractTime = float.MinValue;

        // ===================================================================

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _collider = GetComponent<Collider>();

            // AudioSource secondaire pour le typing sound
            _typingAudioSource = gameObject.AddComponent<AudioSource>();
            _typingAudioSource.playOnAwake = false;
            _typingAudioSource.loop = false;
            _typingAudioSource.spatialBlend = 0f;

            if (_dialogPanel != null)
                _dialogPanel.SetActive(false);

            SetOutlineActive(false);
        }

        private void Update()
        {
            if (_isPlaying && _requiredPlace != null)
            {
                if (PlaceManager.Instance == null || PlaceManager.Instance.CurrentPlace != _requiredPlace)
                    StopInteraction();
            }

            if (!_isPlaying)
                UpdateHoverAndClick();
        }

        // ===================================================================
        // Hover & Click
        // ===================================================================

        private void UpdateHoverAndClick()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (_wasHovered) { _wasHovered = false; SetOutlineActive(false); }
                return;
            }

            Camera cam = Camera.main;
            if (cam == null || _collider == null || Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);
            bool hitThis = Physics.Raycast(ray, out RaycastHit hit, 100f) && hit.collider == _collider;

            if (hitThis != _wasHovered)
            {
                _wasHovered = hitThis;
                if (hitThis) { _wasAvailable = IsInteractionAvailable(); SetOutlineActive(_wasAvailable); }
                else SetOutlineActive(false);
            }

            if (_wasHovered)
            {
                bool isAvailable = IsInteractionAvailable();
                if (_wasAvailable != isAvailable) { _wasAvailable = isAvailable; SetOutlineActive(isAvailable); }
            }

            if (_wasHovered && Mouse.current.leftButton.wasPressedThisFrame)
                Interact();
        }

        // ===================================================================
        // Public
        // ===================================================================

        public void Interact()
        {
            if (_isPlaying) return;

            if (_requiredPlace != null)
            {
                if (PlaceManager.Instance == null || PlaceManager.Instance.CurrentPlace != _requiredPlace)
                    return;
            }

            if (PlaceManager.Instance != null && !PlaceManager.Instance.CanNavigate) return;
            if (_cooldown > 0f && Time.time - _lastInteractTime < _cooldown) return;

            _lastInteractTime = Time.time;
            _isPlaying = true;
            SetOutlineActive(false);

            if (_blockMovement)
                PlaceManager.Instance?.SetBlocked(true);

            // Vérifie la clé
            var cm = FindAnyObjectByType<CircusManager>();
            bool hasKey = cm != null && cm.HasKey;

            if (hasKey)
            {
                _routine = StartCoroutine(OpenDoorRoutine());
            }
            else
            {
                if (_playOnce && _hasPlayedLocked) return;
                _hasPlayedLocked = true;
                _routine = StartCoroutine(LockedDialogRoutine());
            }
        }

        [ContextMenu("Stop")]
        public void StopInteraction()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_audioSource != null) _audioSource.Stop();
            StopTypingLoop();
            if (_dialogPanel != null) _dialogPanel.SetActive(false);
            if (_blockMovement) PlaceManager.Instance?.SetBlocked(false);
            _isPlaying = false;

            if (_wasHovered && IsInteractionAvailable()) { _wasAvailable = true; SetOutlineActive(true); }
        }

        // ===================================================================
        // Outline
        // ===================================================================

        private void SetOutlineActive(bool active)
        {
            if (_outline != null) _outline.enabled = active;
            if (_extraOutlines != null)
                foreach (var o in _extraOutlines) if (o != null) o.enabled = active;
        }

        private bool IsInteractionAvailable()
        {
            if (_isPlaying) return false;
            if (PlaceManager.Instance != null && !PlaceManager.Instance.CanNavigate) return false;
            if (_cooldown > 0f && Time.time - _lastInteractTime < _cooldown) return false;
            if (_requiredPlace != null)
            {
                if (PlaceManager.Instance == null || PlaceManager.Instance.CurrentPlace != _requiredPlace)
                    return false;
            }
            return true;
        }

        // ===================================================================
        // Routines
        // ===================================================================

        private IEnumerator LockedDialogRoutine()
        {
            if (_dialogPanel != null) _dialogPanel.SetActive(true);

            if (_lockedDialogues != null && _lockedDialogues.Length > 0)
            {
                for (int i = 0; i < _lockedDialogues.Length; i++)
                {
                    DialogEntry entry = _lockedDialogues[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Text)) continue;

                    PlayVoice(entry.VoiceClip);
                    yield return StartCoroutine(RevealTextRoutine(entry));

                    if (entry.DisplayDuration > 0f)
                        yield return new WaitForSeconds(entry.DisplayDuration);
                }
            }
            else
            {
                // Fallback : un dialogue vide de 2 secondes
                yield return new WaitForSeconds(2f);
            }

            if (_dialogPanel != null) _dialogPanel.SetActive(false);
            if (_blockMovement) PlaceManager.Instance?.SetBlocked(false);

            _isPlaying = false;
            _routine = null;

            if (_wasHovered && IsInteractionAvailable()) { _wasAvailable = true; SetOutlineActive(true); }
        }

        private IEnumerator OpenDoorRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            SceneTransitionManager.LoadScene(_targetSceneBuildIndex);
        }

        private IEnumerator RevealTextRoutine(DialogEntry entry)
        {
            _dialogText.text = "";

            StartTypingLoop();

            switch (entry.RevealMode)
            {
                case DialogReveal.Instant:
                    _dialogText.text = entry.Text;
                    break;

                case DialogReveal.CharByChar:
                    foreach (char c in entry.Text) { _dialogText.text += c; yield return new WaitForSeconds(entry.RevealSpeed); }
                    break;

                case DialogReveal.WordByWord:
                    string[] words = entry.Text.Split(' ');
                    for (int i = 0; i < words.Length; i++) { _dialogText.text += (i > 0 ? " " : "") + words[i]; yield return new WaitForSeconds(entry.RevealSpeed); }
                    break;
            }

            StopTypingLoop();
        }

        private void PlayVoice(AudioClip clip)
        {
            if (_audioSource == null || clip == null) return;
            _audioSource.PlayOneShot(clip, _volume);
        }

        private void StartTypingLoop()
        {
            var sounds = _typingVoice == TypingVoice.Player
                ? PlayerSounds.Instance?.PlayerTypingSounds
                : PlayerSounds.Instance?.OtherTypingSounds;

            if (_typingAudioSource == null || sounds == null || sounds.Length == 0)
            {
                Debug.LogWarning($"[DoorInteraction] StartTypingLoop ignoré : source={_typingAudioSource != null}, sounds={sounds?.Length ?? 0}, voice={_typingVoice}");
                return;
            }

            var clip = sounds[Random.Range(0, sounds.Length)];
            Debug.Log($"[DoorInteraction] ▶ Typing loop : {clip.name}");
            _typingAudioSource.clip = clip;
            _typingAudioSource.loop = true;
            _typingAudioSource.volume = 0.6f;
            _typingAudioSource.Play();
        }

        private void StopTypingLoop()
        {
            if (_typingAudioSource == null) return;
            Debug.Log("[DoorInteraction] ⏹ Typing loop stopped");
            _typingAudioSource.Stop();
            _typingAudioSource.clip = null;
        }

        // ===================================================================
        // Editor
        // ===================================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_outline == null) _outline = GetComponent<Outline>();
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col == null) gameObject.AddComponent<BoxCollider>().isTrigger = true;
            else col.isTrigger = true;

            if (_outline == null) _outline = GetComponent<Outline>();
            if (_outline == null) _outline = gameObject.AddComponent<Outline>();
        }
#endif
    }
}
