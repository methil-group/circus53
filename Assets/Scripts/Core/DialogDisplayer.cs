using TMPro;
using UnityEngine;
using System.Collections;

namespace Core
{
    /// <summary>
    /// Affiche les dialogues en sous-titres sur un TMP_Text.
    /// Singleton — PlaceManager l'appelle directement.
    /// </summary>
    public class DialogDisplayer : MonoBehaviour
    {
        public static DialogDisplayer Instance { get; private set; }

        [SerializeField] private TMP_Text _text;
        [SerializeField, Tooltip("Durée d'affichage en secondes (0 = reste affiché jusqu'au prochain).")]
        private float _displayDuration = 4f;

        private Coroutine _hideRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Clear();
        }

        public void Show(string message)
        {
            if (_text == null) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            if (_hideRoutine != null)
                StopCoroutine(_hideRoutine);

            _text.text = message;
            _text.gameObject.SetActive(true);

            if (_displayDuration > 0f)
                _hideRoutine = StartCoroutine(HideAfter(_displayDuration));
        }

        public void Hide()
        {
            if (_hideRoutine != null)
                StopCoroutine(_hideRoutine);
            Clear();
        }

        private void Clear()
        {
            if (_text != null)
            {
                _text.text = string.Empty;
                _text.gameObject.SetActive(false);
            }
        }

        private IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            Clear();
        }
    }
}
