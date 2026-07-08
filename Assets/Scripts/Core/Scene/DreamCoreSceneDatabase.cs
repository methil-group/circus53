using Framework.ScriptableObjects;
using UnityEngine;

namespace Core.Scene
{
    [CreateAssetMenu(fileName = "DreamCoreSceneDatabase", menuName = "Dream Core/Dream Core Scene Database")]
    public class DreamCoreSceneDatabase : SingletonScriptableDatabase<DreamCoreSceneDatabase, DreamCoreScene>
    {
        public DreamCoreScene startScene;
    }
}