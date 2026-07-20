using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    /// <summary>
    /// Garantit que le blocking reste jouable même si les overrides de scène
    /// n'ont pas encore été générés dans l'éditeur.
    /// </summary>
    public class CameraClickPointRuntimeSetup : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var bootstrap = new GameObject("[CameraClickPoint] Runtime Setup");
            DontDestroyOnLoad(bootstrap);
            bootstrap.AddComponent<CameraClickPointRuntimeSetup>();
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += ConfigureScene;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= ConfigureScene;
        }

        private static void ConfigureScene(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!root.name.Contains("blocking-with-cams")) continue;

                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.GetComponent<CameraClickPoint>() == null)
                        camera.gameObject.AddComponent<CameraClickPoint>();

                    // Évite que plusieurs caméras du FBX rendent simultanément.
                    camera.enabled = false;
                }
            }
        }
    }
}
