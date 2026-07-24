using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>Centralise les états de décor associés aux différents Place du circus.</summary>
    [DisallowMultipleComponent]
    public class CircusManager : MonoBehaviour
    {
        [SerializeField] private List<Place> _places = new();

        /// <summary>True si le joueur possède la clé.</summary>
        public bool HasKey { get; set; }

        public void SelectPlace(Place selectedPlace)
        {
            if (selectedPlace == null) return;
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
