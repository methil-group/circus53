using UnityEngine;

namespace Framework.ScriptableObjects
{
    public abstract class SingletonScriptableObject<T> : ScriptableObject where T : SingletonScriptableObject<T>
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    var assets = Resources.LoadAll<T>("");
                    if (assets.Length > 0) instance = assets[0];
                }
                return instance;
            }
        }
    }

}