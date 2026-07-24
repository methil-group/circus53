using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// Quand un GameObject avec un composant Place est sélectionné,
    /// dessine des lignes colorées vers ses voisins avec des labels.
    /// </summary>
    [CustomEditor(typeof(Place))]
    public class PlaceEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            Place place = (Place)target;
            if (place == null) return;

            Vector3 origin = place.transform.position + Vector3.up * 0.1f;

            DrawConnection(origin, place.FrontPlace, "Front", Color.cyan);
            DrawConnection(origin, place.BackPlace,  "Back",  Color.red);
            DrawConnection(origin, place.LeftPlace,  "Left",  Color.yellow);
            DrawConnection(origin, place.RightPlace, "Right", Color.green);
        }

        private static void DrawConnection(Vector3 from, Place target, string label, Color color)
        {
            if (target == null) return;

            Vector3 to = target.transform.position + Vector3.up * 0.1f;

            Handles.color = color;
            Handles.DrawDottedLine(from, to, 4f);

            // Label au milieu de la ligne, légèrement au-dessus
            Vector3 mid = (from + to) * 0.5f + Vector3.up * 0.3f;
            Handles.Label(mid, label, new GUIStyle
            {
                normal = { textColor = color },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            });
        }
    }
}
