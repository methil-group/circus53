using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using Core.Player;

namespace Core
{
    public class DialogDisplayer : MonoBehaviour
    {
        public static DialogDisplayer Instance { get; private set; }

        [SerializeField] private TMP_Text _text;

        public bool IsPlaying { get; private set; }
        public event Action OnDialogComplete;

        private Coroutine _routine;
        private AudioSource _typingAudioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Clear();

            // AudioSource pour le typing sound
            _typingAudioSource = gameObject.AddComponent<AudioSource>();
            _typingAudioSource.playOnAwake = false;
            _typingAudioSource.loop = false;
            _typingAudioSource.spatialBlend = 0f;
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
            StopTypingLoop();
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

            // Typing sound basé sur la voix
            StartTypingLoop(line.Voice);

            switch (line.Reveal)
            {
                case DialogReveal.Instant:
                    _text.text = line.Text;
                    break;

                case DialogReveal.CharByChar:
                    _text.text = "";
                    for (int i = 0; i < line.Text.Length; i++)
                    {
                        _text.text += line.Text[i];
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

            StopTypingLoop();

            // Affichage après le typewriter
            if (line.DisplayDuration > 0f)
                yield return new WaitForSeconds(line.DisplayDuration);

            IsPlaying = false;
            Clear();
            OnDialogComplete?.Invoke();
        }

        private void StartTypingLoop(TypingVoice voice)
        {
            var sounds = voice == TypingVoice.Player
                ? PlayerSounds.Instance?.PlayerTypingSounds
                : PlayerSounds.Instance?.OtherTypingSounds;

            if (_typingAudioSource == null || sounds == null || sounds.Length == 0)
            {
                Debug.LogWarning($"[DialogDisplayer] StartTypingLoop ignoré : voice={voice}, sounds={sounds?.Length ?? 0}");
                return;
            }

            var clip = sounds[UnityEngine.Random.Range(0, sounds.Length)];
            Debug.Log($"[DialogDisplayer] ▶ Typing loop ({voice}) : {clip.name}");
            _typingAudioSource.clip = clip;
            _typingAudioSource.loop = true;
            _typingAudioSource.volume = 0.6f;
            _typingAudioSource.Play();
        }

        private void StopTypingLoop()
        {
            if (_typingAudioSource == null) return;
            _typingAudioSource.Stop();
            _typingAudioSource.clip = null;
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
            LayoutRebuilder.ForceRebuildLayoutImmediate(_text.rectTransform);
        }
    }
}
