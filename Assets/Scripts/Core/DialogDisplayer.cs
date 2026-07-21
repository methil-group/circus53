using TMPro;
using UnityEngine;
using System.Collections;

namespace Core
{
    /// <summary>
    /// Affiche un texte de dialogue sur un TMP_Text (style sous-titre).
    /// À brancher sur le PlaceManager.OnDialog.
    /// </summary>
    public class DialogDisplayer : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField, Tooltip("Durée d'affichage en secondes (0 = reste affiché jusqu'au prochain).")]
        private float _displayDuration = 4f;

        private Coroutine _hideRoutine;

        /// <summary>Affiche le dialogue. Appelé par PlaceManager.OnDialog.</summary>
        public void Show(string message)
        {
            if (_text == null) return;

            if (_hideRoutine != null)
                StopCoroutine(_hideRoutine);

            _text.text = message;
            _text.gameObject.SetActive(true);

            if (_displayDuration > 0f)
                _hideRoutine = StartCoroutine(HideAfter(_displayDuration));
        }

        private IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_text != null)
                _text.gameObject.SetActive(false);
        }
    }
}
