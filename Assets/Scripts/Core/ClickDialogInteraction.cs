using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Core
{
    /// <summary>
    /// Entrée de dialogue : un texte, ses réglages d'affichage et un son optionnel.
    /// </summary>
    [Serializable]
    public class DialogEntry
    {
        [TextArea(2, 6)]
        public string Text;

        [Tooltip("Mode d'apparition : instantané, lettre par lettre, ou mot par mot.")]
        public DialogReveal RevealMode = DialogReveal.CharByChar;

        [Tooltip("Vitesse d'apparition (secondes par caractère ou par mot).")]
        public float RevealSpeed = 0.05f;

        [Tooltip("Durée d'affichage une fois le texte complet (secondes).")]
        public float DisplayDuration = 4f;

        [Tooltip("Clip vocal à jouer pendant ce dialogue.")]
        public AudioClip VoiceClip;
    }

    /// <summary>
    /// Interaction click → dialogues en chaîne.
    /// Détecte le clic via raycast manuel (Update) pour éviter les conflits avec l'UI.
    /// Active un panneau TMP_Text, joue chaque DialogEntry l'une après l'autre
    /// (texte + son optionnel), puis désactive le panneau à la fin.
    /// 
    /// - Outline au survol quand l'interaction est disponible.
    /// - Empêche le re-déclenchement si déjà en cours.
    /// - Stoppe tout (texte + son) si le joueur quitte la Place requise.
    /// - Bloque les déplacements pendant l'interaction.
    /// Nécessite un Collider sur ce GameObject ou un enfant.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(AudioSource))]
    public class ClickDialogInteraction : MonoBehaviour
    {
        [Header("Panneau de dialogue")]
        [SerializeField, Tooltip("GameObject contenant le TMP_Text, activé/désactivé.")]
        private GameObject _dialogPanel;

        [SerializeField, Tooltip("TMP_Text à remplir avec le texte du dialogue.")]
        private TMP_Text _dialogText;

        [Header("Dialogues")]
        [SerializeField, Tooltip("Liste des dialogues joués à la suite.")]
        private DialogEntry[] _dialogues;

        [Header("Son global")]
        [SerializeField, Tooltip("Volume global pour toutes les voix.")]
        [Range(0f, 1f)]
        private float _volume = 1f;

        [Header("Place requise")]
        [SerializeField, Tooltip("Place dans laquelle le joueur doit se trouver pour pouvoir cliquer. Null = accessible depuis n'importe où.")]
        private Place _requiredPlace;

        [Header("Outline")]
        [SerializeField, Tooltip("Composant Outline (QuickOutline) à activer au survol quand l'interaction est disponible.")]
        private Outline _outline;

        [SerializeField, Tooltip("GameObject(s) portant un composant Outline (si différent de ce GameObject).")]
        private Outline[] _extraOutlines;

        [Header("Settings")]
        [SerializeField, Tooltip("Si coché, jouable une seule fois.")]
        private bool _playOnce = true;

        [SerializeField, Tooltip("Cooldown entre deux clics (secondes).")]
        private float _cooldown;

        // ===================================================================

        private AudioSource _audioSource;
        private Coroutine _routine;
        private bool _hasPlayed;
        private bool _isPlaying;
        private bool _wasHovered;
        private bool _wasAvailable;
        private Collider _collider;
        private float _lastInteractTime = float.MinValue;

        /// <summary>True si une interaction est en cours.</summary>
        public bool IsPlaying => _isPlaying;

        // ===================================================================

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _collider = GetComponent<Collider>();

            if (_dialogPanel != null)
                _dialogPanel.SetActive(false);

            // Outline off au démarrage
            SetOutlineActive(false);
        }

        private void Update()
        {
            // Si le joueur quitte la Place requise, on coupe tout
            if (_isPlaying && _requiredPlace != null)
            {
                if (PlaceManager.Instance == null || PlaceManager.Instance.CurrentPlace != _requiredPlace)
                {
                    StopInteraction();
                }
            }

            if (!_isPlaying)
            {
                UpdateHoverAndClick();
            }
        }

        // ===================================================================
        // Hover & Click (gérés manuellement pour éviter les conflits UI)
        // ===================================================================

        private void UpdateHoverAndClick()
        {
            // Vérifie si la souris est au-dessus d'un élément UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // Souris sur l'UI → pas de hover, pas de clic
                if (_wasHovered)
                {
                    _wasHovered = false;
                    SetOutlineActive(false);
                }
                return;
            }

            // Raycast depuis la caméra vers la souris
            Camera cam = Camera.main;
            if (cam == null || _collider == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            bool hitThis = Physics.Raycast(ray, out RaycastHit hit, 100f) && hit.collider == _collider;

            // Hover enter / exit
            if (hitThis != _wasHovered)
            {
                _wasHovered = hitThis;
                if (hitThis)
                {
                    _wasAvailable = IsInteractionAvailable();
                    SetOutlineActive(_wasAvailable);
                }
                else
                {
                    SetOutlineActive(false);
                }
            }

            // Rafraîchir l'outline si la disponibilité change pendant le hover
            if (_wasHovered)
            {
                bool isAvailable = IsInteractionAvailable();
                if (_wasAvailable != isAvailable)
                {
                    _wasAvailable = isAvailable;
                    SetOutlineActive(isAvailable);
                }
            }

            // Clic
            if (_wasHovered && Input.GetMouseButtonDown(0))
            {
                Interact();
            }
        }

        // ===================================================================
        // Public
        // ===================================================================

        /// <summary>Déclenche la chaîne de dialogues. Appelable depuis le code.</summary>
        public void Interact()
        {
            // Déjà en train de jouer → on ignore
            if (_isPlaying)
                return;

            // Place requise ?
            if (_requiredPlace != null)
            {
                if (PlaceManager.Instance == null || PlaceManager.Instance.CurrentPlace != _requiredPlace)
                    return;
            }

            // Cooldown
            if (_cooldown > 0f && Time.time - _lastInteractTime < _cooldown)
                return;

            // Play once
            if (_playOnce && _hasPlayed)
                return;

            _hasPlayed = true;
            _lastInteractTime = Time.time;
            _isPlaying = true;

            // Désactiver l'outline pendant l'interaction
            SetOutlineActive(false);

            // Bloquer les déplacements
            PlaceManager.Instance?.SetBlocked(true);

            _routine = StartCoroutine(InteractRoutine());
        }

        /// <summary>Stoppe immédiatement l'interaction en cours.</summary>
        [ContextMenu("Stop")]
        public void StopInteraction()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            // Couper le son
            if (_audioSource != null)
                _audioSource.Stop();

            // Désactiver le panneau
            if (_dialogPanel != null)
                _dialogPanel.SetActive(false);

            // Débloquer les déplacements
            PlaceManager.Instance?.SetBlocked(false);

            _isPlaying = false;

            // Réactiver l'outline si toujours en hover et dispo
            if (_wasHovered && IsInteractionAvailable())
            {
                _wasAvailable = true;
                SetOutlineActive(true);
            }
        }

        /// <summary>Réinitialise l'état pour rejouer.</summary>
        [ContextMenu("Reset")]
        public void ResetInteraction()
        {
            StopInteraction();
            _hasPlayed = false;
            _lastInteractTime = float.MinValue;
        }

        // ===================================================================
        // Outline
        // ===================================================================

        private void SetOutlineActive(bool active)
        {
            if (_outline != null)
                _outline.enabled = active;

            if (_extraOutlines != null)
            {
                foreach (var o in _extraOutlines)
                {
                    if (o != null) o.enabled = active;
                }
            }
        }

        private bool IsInteractionAvailable()
        {
            if (_isPlaying)
                return false;
            if (_playOnce && _hasPlayed)
                return false;
            if (_cooldown > 0f && Time.time - _lastInteractTime < _cooldown)
                return false;
            if (_requiredPlace != null)
            {
                if (PlaceManager.Instance == null || PlaceManager.Instance.CurrentPlace != _requiredPlace)
                    return false;
            }
            return true;
        }

        // ===================================================================
        // Routine
        // ===================================================================

        private IEnumerator InteractRoutine()
        {
            // 1. Activer le panneau
            if (_dialogPanel != null)
                _dialogPanel.SetActive(true);

            // 2. Jouer chaque dialogue à la suite
            if (_dialogues != null && _dialogues.Length > 0)
            {
                for (int i = 0; i < _dialogues.Length; i++)
                {
                    DialogEntry entry = _dialogues[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Text))
                        continue;

                    // Son
                    PlayVoice(entry.VoiceClip);

                    // Révéler le texte
                    yield return StartCoroutine(RevealTextRoutine(entry));

                    // Attendre la durée d'affichage
                    if (entry.DisplayDuration > 0f)
                        yield return new WaitForSeconds(entry.DisplayDuration);
                }
            }

            // 3. Fin normale : désactiver le panneau
            if (_dialogPanel != null)
                _dialogPanel.SetActive(false);

            // Débloquer les déplacements
            PlaceManager.Instance?.SetBlocked(false);

            _isPlaying = false;
            _routine = null;

            // Réactiver l'outline si toujours en hover et dispo
            if (_wasHovered && IsInteractionAvailable())
            {
                _wasAvailable = true;
                SetOutlineActive(true);
            }
        }

        private IEnumerator RevealTextRoutine(DialogEntry entry)
        {
            _dialogText.text = "";

            switch (entry.RevealMode)
            {
                case DialogReveal.Instant:
                    _dialogText.text = entry.Text;
                    break;

                case DialogReveal.CharByChar:
                    foreach (char c in entry.Text)
                    {
                        _dialogText.text += c;
                        yield return new WaitForSeconds(entry.RevealSpeed);
                    }
                    break;

                case DialogReveal.WordByWord:
                    string[] words = entry.Text.Split(' ');
                    for (int i = 0; i < words.Length; i++)
                    {
                        _dialogText.text += (i > 0 ? " " : "") + words[i];
                        yield return new WaitForSeconds(entry.RevealSpeed);
                    }
                    break;
            }
        }

        private void PlayVoice(AudioClip clip)
        {
            if (_audioSource == null || clip == null) return;
            _audioSource.PlayOneShot(clip, _volume);
        }

        // ===================================================================
        // Editor
        // ===================================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
            if (_outline == null)
                _outline = GetComponent<Outline>();
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }
            else
            {
                col.isTrigger = true;
            }

            // Ajoute automatiquement un composant Outline (QuickOutline)
            if (_outline == null)
                _outline = GetComponent<Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<Outline>();
        }
#endif
    }
}
