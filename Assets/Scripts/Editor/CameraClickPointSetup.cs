using Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EditorTools
{
    /// <summary>Outil d'édition pour convertir les caméras d'un blocking FBX en points cliquables.</summary>
    public static class CameraClickPointSetup
    {
        public static void ConfigureCircusScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Liminal/CircusScene.unity", OpenSceneMode.Single);
            ConfigureActiveScene();
        }

        [MenuItem("Tools/Dreamcore/Configure Camera Click Points")]
        public static void ConfigureActiveScene()
        {
            int configured = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root.name.Contains("blocking-with-cams")) continue;

                CircusManager manager = root.GetComponent<CircusManager>();
                if (manager == null)
                    manager = Undo.AddComponent<CircusManager>(root);

                var places = new System.Collections.Generic.List<Place>();

                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    CameraClickPoint point = camera.GetComponent<CameraClickPoint>();
                    if (point == null)
                    {
                        point = Undo.AddComponent<CameraClickPoint>(camera.gameObject);
                        configured++;
                    }

                    point.SetClickAreaSize(0.05f);
                    Place place = camera.GetComponent<Place>();
                    if (place == null)
                        place = Undo.AddComponent<Place>(camera.gameObject);
                    places.Add(place);
                }

                Undo.RecordObject(manager, "Configure circus places");
                manager.AddMissingPlaces(places);
                EditorUtility.SetDirty(manager);
            }

            if (configured > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }

            Debug.Log($"[CameraClickPoint] {configured} point(s) cliquable(s) configuré(s).");
        }
    }
}
