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
    /// - Sans clé : joue un dialogue (texte + son).
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

        [Header("Dialogue — sans clé")]
        [SerializeField, TextArea(2, 6), Tooltip("Texte affiché quand le joueur n'a pas la clé.")]
        private string _lockedText = "La porte est verrouillée... Il me faut une clé.";

        [SerializeField, Tooltip("Mode d'apparition du texte.")]
        private DialogReveal _revealMode = DialogReveal.CharByChar;

        [SerializeField, Tooltip("Vitesse d'apparition (secondes par caractère ou par mot).")]
        private float _revealSpeed = 0.05f;

        [SerializeField, Tooltip("Durée d'affichage une fois le texte complet (secondes).")]
        private float _displayDuration = 4f;

        [SerializeField, Tooltip("Clip vocal à jouer.")]
        private AudioClip _lockedVoiceClip;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1f;

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
        [SerializeField, Tooltip("Si coché, jouable une seule fois (dialogue sans clé).")]
        private bool _playOnce;

        [SerializeField, Tooltip("Cooldown entre deux clics (secondes).")]
        private float _cooldown;

        [SerializeField, Tooltip("Si coché, bloque les déplacements pendant l'interaction.")]
        private bool _blockMovement = true;

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

            PlayVoice(_lockedVoiceClip);
            yield return StartCoroutine(RevealTextRoutine(_lockedText));

            if (_displayDuration > 0f)
                yield return new WaitForSeconds(_displayDuration);

            if (_dialogPanel != null) _dialogPanel.SetActive(false);
            if (_blockMovement) PlaceManager.Instance?.SetBlocked(false);

            _isPlaying = false;
            _routine = null;

            if (_wasHovered && IsInteractionAvailable()) { _wasAvailable = true; SetOutlineActive(true); }
        }

        private IEnumerator OpenDoorRoutine()
        {
            // Petit délai pour le feedback
            yield return new WaitForSeconds(0.3f);
            SceneManager.LoadScene(_targetSceneBuildIndex);
        }

        private IEnumerator RevealTextRoutine(string text)
        {
            _dialogText.text = "";

            // Démarrer le son de typing en boucle (pick aléatoire dans PlayerSounds.TypingSounds)
            StartTypingLoop();

            switch (_revealMode)
            {
                case DialogReveal.Instant:
                    _dialogText.text = text;
                    break;

                case DialogReveal.CharByChar:
                    foreach (char c in text) { _dialogText.text += c; yield return new WaitForSeconds(_revealSpeed); }
                    break;

                case DialogReveal.WordByWord:
                    string[] words = text.Split(' ');
                    for (int i = 0; i < words.Length; i++) { _dialogText.text += (i > 0 ? " " : "") + words[i]; yield return new WaitForSeconds(_revealSpeed); }
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
            var sounds = PlayerSounds.Instance?.TypingSounds;
            if (_typingAudioSource == null || sounds == null || sounds.Length == 0)
            {
                Debug.LogWarning($"[DoorInteraction] StartTypingLoop ignoré : source={_typingAudioSource != null}, sounds={sounds?.Length ?? 0}");
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
