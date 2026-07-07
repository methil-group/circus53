using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Framework.ScriptableObjects
{
    public class SingletonScriptableDatabase<T, TData> : SingletonScriptableObject<T> 
        where T : SingletonScriptableObject<T> where TData : ScriptableObject
    {
        [SerializeField]
        public List<TData> database;

#if UNITY_EDITOR 
        // Crazy function, cannot work without editor functions in Editor/GenericSingletonDatabaseEditor
        public void GetAllData()
        {
            database = new List<TData>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(TData).Name}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TData asset = AssetDatabase.LoadAssetAtPath<TData>(path);
                if (asset != null && !database.Contains(asset)) database.Add(asset);
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
#endif
    }
}