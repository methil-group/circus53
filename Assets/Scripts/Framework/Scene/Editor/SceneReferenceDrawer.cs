using UnityEditor;
using UnityEngine;

namespace Framework.Scene.Editor
{
    /// <summary>
    /// Custom property drawer for SceneReference.
    /// Shows a drag & drop SceneAsset field in the inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public class SceneReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var sceneAssetProp = property.FindPropertyRelative("_sceneAsset");
            var scenePathProp = property.FindPropertyRelative("_scenePath");

            // Show the SceneAsset object field
            EditorGUI.BeginChangeCheck();
            var asset = EditorGUI.ObjectField(
                position,
                label,
                sceneAssetProp.objectReferenceValue as SceneAsset,
                typeof(SceneAsset),
                false
            );

            if (EditorGUI.EndChangeCheck())
            {
                sceneAssetProp.objectReferenceValue = asset;
                scenePathProp.stringValue = asset != null
                    ? AssetDatabase.GetAssetPath(asset)
                    : "";
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }
    }
}
