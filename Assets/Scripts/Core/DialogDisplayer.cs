using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

namespace Core
{
    public class DialogDisplayer : MonoBehaviour
    {
        public static DialogDisplayer Instance { get; private set; }

        [SerializeField] private TMP_Text _text;

        public bool IsPlaying { get; private set; }
        public event Action OnDialogComplete;

        private Coroutine _routine;

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

        public void Play(DialogLine line)
        {
            if (_text == null || line == null || string.IsNullOrWhiteSpace(line.Text)) return;

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(PlayRoutine(line));
        }

        public void Skip()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            IsPlaying = false;
            Clear();
            OnDialogComplete?.Invoke();
        }

        private IEnumerator PlayRoutine(DialogLine line)
        {
            IsPlaying = true;
            Clear();

            // Délai avant apparition
            if (line.Delay > 0f)
                yield return new WaitForSeconds(line.Delay);

            _text.gameObject.SetActive(true);

            switch (line.Reveal)
            {
                case DialogReveal.Instant:
                    _text.text = line.Text;
                    break;

                case DialogReveal.CharByChar:
                    _text.text = "";
                    foreach (char c in line.Text)
                    {
                        _text.text += c;
                        RefreshLayout();
                        yield return new WaitForSeconds(line.RevealSpeed);
                    }
                    break;

                case DialogReveal.WordByWord:
                    _text.text = "";
                    string[] words = line.Text.Split(' ');
                    for (int i = 0; i < words.Length; i++)
                    {
                        _text.text += (i > 0 ? " " : "") + words[i];
                        RefreshLayout();
                        yield return new WaitForSeconds(line.RevealSpeed);
                    }
                    break;
            }

            // Affichage après le typewriter
            if (line.DisplayDuration > 0f)
                yield return new WaitForSeconds(line.DisplayDuration);

            IsPlaying = false;
            Clear();
            OnDialogComplete?.Invoke();
        }

        private void Clear()
        {
            if (_text != null)
            {
                _text.text = string.Empty;
                _text.gameObject.SetActive(false);
            }
        }

        private void RefreshLayout()
        {
            // Force le rebuild du Content Size Fitter / Vertical Layout Group
            LayoutRebuilder.ForceRebuildLayoutImmediate(_text.rectTransform);
        }
    }
}
