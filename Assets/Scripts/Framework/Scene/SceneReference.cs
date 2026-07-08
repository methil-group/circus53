using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.Scene
{
    /// <summary>
    /// Serializable scene reference that can be drag & dropped in the inspector.
    /// Resolves to a Scene at runtime via path or build index.
    /// </summary>
    [Serializable]
    public class SceneReference : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        string _scenePath = "";

        /// <summary>Path relative to the project root (e.g. "Assets/Scenes/MainMenu.unity").</summary>
        public string ScenePath => _scenePath;

        /// <summary>Scene name without extension.</summary>
        public string SceneName
        {
            get
            {
                if (string.IsNullOrEmpty(_scenePath)) return "";
                return System.IO.Path.GetFileNameWithoutExtension(_scenePath);
            }
        }

        /// <summary>Scene build index (-1 if not in build settings).</summary>
        public int BuildIndex
        {
            get
            {
                if (string.IsNullOrEmpty(_scenePath)) return -1;
                return SceneUtility.GetBuildIndexByScenePath(_scenePath);
            }
        }

        /// <summary>Returns true if a scene is assigned.</summary>
        public bool IsValid => !string.IsNullOrEmpty(_scenePath);

        // Editor-only: the SceneAsset object reference (not serialized at runtime)
#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        UnityEditor.SceneAsset _sceneAsset;

        /// <summary>Used by the custom property drawer to set the scene.</summary>
        internal void SetSceneAsset(UnityEditor.SceneAsset asset)
        {
            _sceneAsset = asset;
            _scenePath = asset != null ? UnityEditor.AssetDatabase.GetAssetPath(asset) : "";
        }

        internal UnityEditor.SceneAsset GetSceneAsset() => _sceneAsset;
#endif

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
#if UNITY_EDITOR
            // Keep path in sync with the asset reference
            if (_sceneAsset != null)
                _scenePath = UnityEditor.AssetDatabase.GetAssetPath(_sceneAsset);
#endif
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize() { }

        public override string ToString() => IsValid ? SceneName : "(none)";
    }
}
