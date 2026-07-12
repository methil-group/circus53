#if !VOL_FX

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VolFx.Editor
{
    /// <summary>
    /// PropertyDrawer pour le type générique <see cref="Optional{T}"/>.
    /// Affiche la valeur avec un toggle activer/désactiver à droite.
    /// </summary>
    /// <remarks>
    /// Le drawer affiche deux contrôles côte à côte :
    /// <list type="bullet">
    ///   <item>Le champ de valeur (désactivé visuellement si le toggle est off)</item>
    ///   <item>Un toggle de 18px de large pour activer/désactiver la propriété</item>
    /// </list>
    /// Gère un cas spécial pour les <b>LayerMask</b> qui nécessitent
    /// <see cref="InternalEditorUtility"/> pour un affichage correct.
    /// </remarks>
    [CustomPropertyDrawer(typeof(Optional<>))]
    public class OptionalDrawer : PropertyDrawer
    {
        /// <summary>Largeur du toggle activer/désactiver en pixels.</summary>
        private const float k_ToggleWidth = 18;

        /// <summary>
        /// Délègue le calcul de la hauteur au PropertyDrawer interne de la valeur.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var valueProperty = property.FindPropertyRelative("value");
            return EditorGUI.GetPropertyHeight(valueProperty);
        }

        /// <summary>
        /// Dessine la valeur et le toggle côte à côte.
        /// </summary>
        /// <remarks>
        /// Le niveau d'indentation est sauvegardé/restauré manuellement car Unity
        /// gère l'indentation de façon globale — placer le toggle en dehors de
        /// la zone indentée évite un décalage visuel.
        /// </remarks>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueProperty   = property.FindPropertyRelative("value");
            var enabledProperty = property.FindPropertyRelative("enabled");

            position.width -= k_ToggleWidth;
            using (new EditorGUI.DisabledGroupScope(!enabledProperty.boolValue))
            {
                // Correction pour les LayerMask : Unity récent a un bug d'affichage
                // avec EditorGUI.PropertyField, on utilise donc les méthodes internes.
                if (valueProperty.propertyType == SerializedPropertyType.LayerMask)
                    valueProperty.intValue =  InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(EditorGUI.MaskField(position, label, InternalEditorUtility.LayerMaskToConcatenatedLayersMask(valueProperty.intValue), InternalEditorUtility.layers));
                else
                    EditorGUI.PropertyField(position, valueProperty, label, true);
            }

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var togglePos = new Rect(position.x + position.width + EditorGUIUtility.standardVerticalSpacing, position.y, k_ToggleWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(togglePos, enabledProperty, GUIContent.none);

            EditorGUI.indentLevel = indent;
        }
    }
}

#endif
