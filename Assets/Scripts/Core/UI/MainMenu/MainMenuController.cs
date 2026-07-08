using Core.Scene;
using Framework.Controller;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.UI.MainMenu
{
    public class MainMenuController : BaseController<MainMenuController>
    {
        public void LaunchGame()
        {
            SceneManager.LoadScene(DreamCoreSceneDatabase.Instance.startScene.scene.BuildIndex);
        }
    }
}