using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>Centralise les états de décor associés aux différents Place du circus.</summary>
    [DisallowMultipleComponent]
    public class CircusManager : MonoBehaviour
    {
        [SerializeField] private List<Place> _places = new();
        [SerializeField, Tooltip("Désactive les objets activables des autres endroits avant d'activer le nouvel endroit.")]
        private bool _exclusivePlaceObjects = true;

        public void SelectPlace(Place selectedPlace)
        {
            if (selectedPlace == null || !_places.Contains(selectedPlace)) return;

            if (_exclusivePlaceObjects)
            {
                foreach (Place place in _places)
                {
                    if (place == null || place == selectedPlace) continue;
                    foreach (GameObject gameObject in place.ObjectsToActivate)
                    {
                        if (gameObject != null)
                            gameObject.SetActive(false);
                    }
                }
            }

            selectedPlace.Apply();
            Debug.Log($"[CircusManager] Endroit sélectionné : {selectedPlace.name}");
        }

        /// <summary>Ajoute les Place manquants sans modifier la configuration existante.</summary>
        public void AddMissingPlaces(IEnumerable<Place> places)
        {
            foreach (Place place in places)
            {
                if (place != null && !_places.Contains(place))
                    _places.Add(place);
            }
        }

        [ContextMenu("Add All Places From Scene")]
        private void AddAllPlacesFromScene()
        {
            Place[] allPlaces = FindObjectsByType<Place>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            int added = 0;
            foreach (Place place in allPlaces)
            {
                if (!_places.Contains(place))
                {
                    _places.Add(place);
                    added++;
                }
            }

            Debug.Log($"[CircusManager] {added} Place(s) ajouté(s) à la liste ({_places.Count} total).");
        }
    }
}
