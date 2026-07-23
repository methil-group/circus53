using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    /// <summary>
    /// Au bout de X secondes, charge la scène dont le build index est donné.
    /// Se place sur n'importe quel GameObject. Au Start(), le compteur démarre.
    /// </summary>
    public class SceneSwitch : MonoBehaviour
    {
        [SerializeField, Tooltip("Délai en secondes avant de changer de scène.")]
        private float _delay = 4f;

        [SerializeField, Tooltip("Build index de la scène cible.")]
        private int _targetSceneBuildIndex = 1;

        private void Start()
        {
            StartCoroutine(SwitchRoutine());
        }

        private IEnumerator SwitchRoutine()
        {
            if (_delay > 0f)
                yield return new WaitForSeconds(_delay);

            SceneManager.LoadScene(_targetSceneBuildIndex);
        }
    }
}
