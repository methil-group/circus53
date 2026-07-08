using UnityEngine;
using Framework.Scene;

namespace Core.Scene
{
    [CreateAssetMenu(fileName = "NewDreamCoreScene", menuName = "Dream Core/DreamCore Scene")]
    public class DreamCoreScene : ScriptableObject
    {
        [SerializeField]
        public SceneReference scene;
    }
}