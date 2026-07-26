namespace Core.UI.MainMenu
{
    public class MainMenuController : Framework.Controller.BaseController<MainMenuController>
    {
        public void LaunchGame()
        {
            SceneTransitionManager.LoadScene(2);
        }
    }
}
