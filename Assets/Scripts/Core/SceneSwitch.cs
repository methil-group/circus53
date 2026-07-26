using System.Collections;
using UnityEngine;

namespace Core
{
    public class SceneSwitch : MonoBehaviour
    {
        [SerializeField] private float _delay = 4f;
        [SerializeField] private int _targetSceneBuildIndex = 1;

        private void Start()
        {
            StartCoroutine(SwitchRoutine());
        }

        private IEnumerator SwitchRoutine()
        {
            if (_delay > 0f)
                yield return new WaitForSeconds(_delay);

            SceneTransitionManager.LoadScene(_targetSceneBuildIndex);
        }
    }
}
