#if !VOL_FX

using UnityEditor;
using UnityEngine;

namespace VolFx.Editor
{
    /// <summary>
    /// PropertyDrawer pour le type <see cref="CurveValue"/>.
    /// Affiche une courbe d'animation dans l'inspecteur Unity et convertit
    /// automatiquement ses valeurs en tableau de pixels 32px pour le GPU.
    /// </summary>
    /// <remarks>
    /// La courbe est échantillonnée en 32 points (défini par <see cref="GradientValue.k_Width"/>)
    /// et chaque point est converti en couleur en niveaux de gris.
    /// Ce tableau de pixels est ensuite utilisé par le shader comme texture 1D.
    /// </remarks>
    [CustomPropertyDrawer(typeof(CurveValue))]
    public class CurveValueDraver : PropertyDrawer
    {
        /// <summary>
        /// Retourne la hauteur fixe d'une ligne pour ce drawer.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return UnityEditor.EditorGUIUtility.singleLineHeight;
        }

        /// <summary>
        /// Dessine le champ de courbe et met à jour les pixels lors des modifications.
        /// </summary>
        /// <remarks>
        /// À chaque modification de la courbe, les 32 pixels du tableau <c>_pixels</c>
        /// sont recalculés en évaluant la courbe à intervalles réguliers.
        /// </remarks>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var curve = property.FindPropertyRelative("_curve");
            EditorGUI.BeginChangeCheck();
            EditorGUI.CurveField(position, curve, Color.green, new Rect(0, 0, 1, 1), label);
            if (EditorGUI.EndChangeCheck())
            {
                var pixels = property.FindPropertyRelative("_pixels");
                var val    = curve.animationCurveValue;
                for (var n = 0; n < GradientValue.k_Width; n++)
                {
                    var c = val.Evaluate(n / (float)(GradientValue.k_Width - 1));
                    pixels.GetArrayElementAtIndex(n).colorValue = new Color(c, c, c, c);
                }
            }
        }
    }
}

#endif
