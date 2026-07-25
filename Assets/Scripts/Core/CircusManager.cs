using System.Collections;
using System.Collections.Generic;
using Core.Player;
using UnityEngine;

namespace Core
{
    [DisallowMultipleComponent]
    public class CircusManager : MonoBehaviour
    {
        [SerializeField] private List<Place> _places = new();

        /// <summary>True si le joueur possède la clé.</summary>
        public bool HasKey { get; set; }

        // =======================================================================

        [Header("Random Ambient")]
        [SerializeField, Tooltip("Volume des sons d'ambiance aléatoires.")]
        [Range(0f, 1f)]
        private float _randomAmbientVolume = 0.4f;

        private AudioSource _globalAmbientSource;
        private AudioSource _randomAmbientSource;

        private void Start()
        {
            // Ambiance globale
            var clip = PlayerSounds.Instance?.GlobalAmbientSound;
            if (clip != null)
            {
                _globalAmbientSource = gameObject.AddComponent<AudioSource>();
                _globalAmbientSource.playOnAwake = false;
                _globalAmbientSource.loop = true;
                _globalAmbientSource.spatialBlend = 0f;
                _globalAmbientSource.clip = clip;
                _globalAmbientSource.volume = 0.5f;
                _globalAmbientSource.Play();
                Debug.Log($"[CircusManager] 🌍 Ambiance globale : {clip.name}");
            }

            // Ambiances aléatoires
            var randomSounds = PlayerSounds.Instance?.RandomAmbientSounds;
            if (randomSounds != null && randomSounds.Length > 0)
            {
                _randomAmbientSource = gameObject.AddComponent<AudioSource>();
                _randomAmbientSource.playOnAwake = false;
                _randomAmbientSource.loop = false;
                _randomAmbientSource.spatialBlend = 0f;
                _randomAmbientSource.volume = _randomAmbientVolume;

                StartCoroutine(RandomAmbientRoutine(randomSounds));
            }
        }

        private IEnumerator RandomAmbientRoutine(AudioClip[] sounds)
        {
            while (true)
            {
                // Intervalle aléatoire entre 1m30s et 2m
                float interval = Random.Range(90f, 120f);
                yield return new WaitForSeconds(interval);

                var clip = sounds[Random.Range(0, sounds.Length)];
                _randomAmbientSource.clip = clip;
                _randomAmbientSource.Play();
                Debug.Log($"[CircusManager] 🎲 Ambiance aléatoire : {clip.name}");
            }
        }

        // =======================================================================

        public void SelectPlace(Place selectedPlace)
        {
            if (selectedPlace == null) return;
            Debug.Log($"[CircusManager] Endroit sélectionné : {selectedPlace.name}");
        }

        public void AddMissingPlaces(IEnumerable<Place> places)
        {
            foreach (Place place in places)
            {
                if (place != null && !_places.Contains(place))
                    _places.Add(place);
            }
        }

        [ContextMenu("Rebuild All Places From Scene")]
        private void RebuildAllPlacesFromScene()
        {
            Place[] allPlaces = FindObjectsByType<Place>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            System.Array.Sort(allPlaces, (a, b) =>
                string.CompareOrdinal(a.name, b.name));

            _places.Clear();
            _places.AddRange(allPlaces);

            Debug.Log($"[CircusManager] Liste reconstruite : {_places.Count} Place(s) trié(s) par nom.");
        }
    }
}
