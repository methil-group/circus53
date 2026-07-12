#if !VOL_FX

using UnityEditor;
using UnityEngine;

namespace VolFx.Editor
{
    /// <summary>
    /// PropertyDrawer pour le type <see cref="GradientValue"/>.
    /// Affiche un dégradé dans l'inspecteur Unity et convertit
    /// automatiquement ses couleurs en tableau de pixels 32px pour le GPU.
    /// </summary>
    /// <remarks>
    /// Le dégradé est échantillonné en 32 points et chaque point est converti
    /// en couleur via <c>Gradient.Evaluate()</c>. Le tableau résultant est
    /// ensuite uploadé comme texture 1D pour le shader.
    /// Sur Unity 2021 et inférieur, utilise la réflexion pour accéder à la
    /// propriété <c>gradientValue</c> qui n'est pas exposée publiquement.
    /// </remarks>
    [CustomPropertyDrawer(typeof(GradientValue))]
    public class GradientValueDraver : PropertyDrawer
    {
        /// <summary>
        /// Retourne la hauteur fixe d'une ligne pour ce drawer.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return UnityEditor.EditorGUIUtility.singleLineHeight;
        }

        /// <summary>
        /// Dessine le champ de dégradé et met à jour les pixels lors des modifications.
        /// </summary>
        /// <remarks>
        /// Utilise <c>EditorGUI.BeginChangeCheck</c> pour détecter les modifications
        /// et ne regénérer le tableau de pixels que lorsque c'est nécessaire.
        /// </remarks>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var grad = property.FindPropertyRelative("_grad");
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, grad, label);
            if (EditorGUI.EndChangeCheck())
            {
                var pixels = property.FindPropertyRelative("_pixels");
                var val = _getGradient(grad);
                for (var n = 0; n < GradientValue.k_Width; n++)
                    pixels.GetArrayElementAtIndex(n).colorValue = val.Evaluate(n / (float)(GradientValue.k_Width - 1));
            }

            // =======================================================================
            /// <summary>
            /// Extrait l'objet Gradient depuis un SerializedProperty.
            /// </summary>
            /// <param name="gradientProperty">La propriété sérialisée contenant le Gradient.</param>
            /// <returns>Le Gradient extrait.</returns>
            /// <remarks>
            /// Sur Unity 2022.1+, utilise <c>SerializedProperty.gradientValue</c> directement.
            /// Sur les versions antérieures, utilise la réflexion car la propriété n'est pas publique.
            /// </remarks>
            Gradient _getGradient(SerializedProperty gradientProperty)
            {
#if UNITY_2022_1_OR_NEWER
                return grad.gradientValue;
#else
                System.Reflection.PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty("gradientValue",
                                                                                                     System.Reflection.BindingFlags.Public |
                                                                                                     System.Reflection.BindingFlags.NonPublic |
                                                                                                     System.Reflection.BindingFlags.Instance);
                
                return propertyInfo.GetValue(gradientProperty, null) as Gradient;
#endif
            }
        }
    }
}

#endif
