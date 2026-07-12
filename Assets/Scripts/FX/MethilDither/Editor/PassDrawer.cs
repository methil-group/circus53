#if !VOL_FX

using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VolFx.Editor
{
    /// <summary>
    /// PropertyDrawer principal pour les passes de rendu (<see cref="VolFx.Pass"/>).
    /// Gère la création et l'affichage inline de ScriptableObjects en tant que sous-assets
    /// dans l'inspecteur Unity.
    /// </summary>
    /// <remarks>
    /// Comportements clés :
    /// <list type="bullet">
    ///   <item>Si la référence est nulle, crée automatiquement une instance du type concret
    ///       via <see cref="ScriptableObject.CreateInstance"/> et l'ajoute comme sous-asset.</item>
    ///   <item>Les propriétés <c>m_Script</c> et <c>_active</c> sont masquées de l'affichage.</item>
    ///   <item>Expose des méthodes statiques publiques pour la réutilisation par d'autres drawers.</item>
    ///   <item>Le paramètre <c>decorativeBox</c> permet d'encadrer visuellement les propriétés.</item>
    ///   <item>Un prédicat <c>filter</c> optionnel permet de masquer certaines propriétés.</item>
    /// </list>
    /// </remarks>
    [CustomPropertyDrawer(typeof(VolFx.Pass), true)]
    public class PassDrawer : PropertyDrawer
    {
        /// <summary>
        /// Retourne la hauteur totale nécessaire pour afficher le ScriptableObject inline.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetObjectReferenceHeight(property);
        }

        /// <summary>
        /// Assure l'existence de l'instance du Pass et affiche ses propriétés.
        /// Si la référence est nulle, crée automatiquement le ScriptableObject
        /// et l'enregistre comme sous-asset de l'asset propriétaire.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var pass = property.objectReferenceValue;
            if (pass == null)
            {
                pass = ScriptableObject.CreateInstance(fieldInfo.FieldType);
                pass.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;

                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(property.serializedObject.targetObject)) == false)
                {
                    AssetDatabase.AddObjectToAsset(pass, property.serializedObject.targetObject);
                    property.objectReferenceValue = pass;
                    
                    EditorUtility.SetDirty(property.serializedObject.targetObject);
                    EditorUtility.SetDirty(pass);
                    
                    property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
            }
            
            DrawObjectReference(property, position);
        }
        
        /// <summary>
        /// Calcule la hauteur d'affichage d'un objet référencé par une SerializedProperty.
        /// </summary>
        /// <param name="element">La propriété sérialisée contenant la référence.</param>
        /// <returns>La hauteur en pixels.</returns>
        public static float GetObjectReferenceHeight(SerializedProperty element)
        {
            return GetObjectReferenceHeight(element.objectReferenceValue, element.isExpanded);
        }

        /// <summary>
        /// Calcule la hauteur d'affichage d'un objet en itérant ses propriétés visibles.
        /// </summary>
        /// <param name="obj">L'objet dont on mesure la hauteur.</param>
        /// <param name="isExpanded">État de repliement (non utilisé actuellement).</param>
        /// <param name="filter">Prédicat optionnel pour exclure certaines propriétés.</param>
        /// <returns>La hauteur totale en pixels.</returns>
        /// <remarks>
        /// Ignore les propriétés <c>m_Script</c> (géré par Unity) et <c>_active</c> (interne).
        /// </remarks>
        public static float GetObjectReferenceHeight(Object obj, bool isExpanded, Predicate<SerializedProperty> filter = null)
        {
            if (obj == null)
                return EditorGUIUtility.singleLineHeight;

            using var so          = new SerializedObject(obj);
            var       totalHeight = 0f;

            using (var iterator = so.GetIterator())
            {
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        var childProperty = so.FindProperty(iterator.name);
                        
                        if (childProperty.name.Equals("m_Script", System.StringComparison.Ordinal))
                            continue;
                        
                        if (childProperty.name.Equals("_active", System.StringComparison.Ordinal))
                            continue;
                        
                        if (filter != null && filter.Invoke(childProperty) == false)
                            continue;

                        totalHeight += EditorGUI.GetPropertyHeight(childProperty);
                    }
                    while (iterator.NextVisible(false));
                }
            }

            totalHeight += EditorGUIUtility.standardVerticalSpacing;
            return totalHeight;
        }
        
        /// <summary>
        /// Affiche les propriétés d'un objet référencé dans une zone rectangulaire donnée.
        /// Surcharge de commodité qui délègue à la version avec Objet.
        /// </summary>
        public static void DrawObjectReference(SerializedProperty element, Rect position)
        {
            DrawObjectReference(element.objectReferenceValue, element.isExpanded, position);
        }

        /// <summary>
        /// Affiche toutes les propriétés visibles d'un objet inline dans l'inspecteur.
        /// </summary>
        /// <param name="obj">L'objet à afficher.</param>
        /// <param name="isExpanded">État de repliement.</param>
        /// <param name="position">Rectangle d'affichage.</param>
        /// <param name="decorativeBox">
        /// Si <c>true</c>, dessine une boîte décorative autour des propriétés,
        /// décalée pour tenir compte de la première ligne.
        /// </param>
        /// <param name="filter">Prédicat optionnel pour filtrer les propriétés à afficher.</param>
        /// <remarks>
        /// Utilise <see cref="EditorGUI.BeginChangeCheck"/> pour n'appliquer les
        /// modifications que lorsque l'utilisateur a modifié une valeur.
        /// </remarks>
        public static void DrawObjectReference(Object obj, bool isExpanded, Rect position, bool decorativeBox = false, Predicate<SerializedProperty> filter = null)
        {
            if (obj == null)
                return;

            using var so = new SerializedObject(obj);

            EditorGUI.BeginChangeCheck();

            using (var iterator = so.GetIterator())
            {
                var yOffset =  EditorGUIUtility.standardVerticalSpacing;
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        var childProperty = so.FindProperty(iterator.name);
                        if (filter != null && filter.Invoke(childProperty) == false)
                            continue;

                        if (childProperty.name.Equals("m_Script", StringComparison.Ordinal))
                            continue;
                        
                        if (childProperty.name.Equals("_active", StringComparison.Ordinal))
                            continue;

                        var childHeight = EditorGUI.GetPropertyHeight(childProperty);
                        var childRect = new Rect()
                        {
                            x      = position.x,
                            y      = position.y + yOffset,
                            width  = position.width,
                            height = childHeight
                        };

                        EditorGUI.PropertyField(childRect, iterator, true);
                        
                        yOffset += childHeight + EditorGUIUtility.standardVerticalSpacing;
                    }
                    while (iterator.NextVisible(false));
                }

                if (decorativeBox)
                {
                    var pos = position;
                    pos.x = 0f;
                    pos.y += EditorGUIUtility.singleLineHeight;
                    pos.width += 100f;
                    pos.height = yOffset - EditorGUIUtility.singleLineHeight;

                    GUI.Box(pos, GUIContent.none);
                }

                if (EditorGUI.EndChangeCheck())
                    so.ApplyModifiedProperties();
            }
        }
    }
}

#endif
