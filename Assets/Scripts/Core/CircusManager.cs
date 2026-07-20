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
            if (selectedPlace == null) return;

            if (_exclusivePlaceObjects && _places.Contains(selectedPlace))
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
            Debug.Log($"[CircusManager] Endroit sélectionné : {selectedPlace.name}" +
                (_places.Contains(selectedPlace) ? "" : " (pas dans la liste _places — appliqué sans exclusivité)"));
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
