using Core.Scene;
using Framework.Controller;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core.UI.MainMenu
{
    public class MainMenuController : BaseController<MainMenuController>
    {
        [Header("Splash Screen")]
        [SerializeField] private Image _splashImage;
        [SerializeField] private float _splashDelay = 4f;
        [SerializeField] private float _splashFadeDuration = 1f;

        private void Start()
        {
            if (_splashImage != null)
                LeanTween.value(_splashImage.gameObject, 1f, 0f, _splashFadeDuration)
                    .setDelay(_splashDelay)
                    .setOnUpdate(alpha =>
                    {
                        Color c = _splashImage.color;
                        c.a = alpha;
                        _splashImage.color = c;
                    })
                    .setOnComplete(() => _splashImage.gameObject.SetActive(false));
        }

        public void LaunchGame()
        {
            SceneManager.LoadScene(DreamCoreSceneDatabase.Instance.startScene.scene.BuildIndex);
        }
    }
}
