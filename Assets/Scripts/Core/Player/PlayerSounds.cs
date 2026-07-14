using Framework.ScriptableObjects;
using UnityEngine;

namespace Core.Player
{
    /// <summary>
    /// Singleton ScriptableObject contenant tous les sons du joueur.
    /// L'asset doit être placé dans un dossier Resources/ (ex: Resources/ScriptableObjects/).
    /// </summary>
    [CreateAssetMenu(menuName = "Dream/Jam/Player Sounds")]
    public class PlayerSounds : SingletonScriptableObject<PlayerSounds>
    {
        [Header("Call Mom")]
        [field: SerializeField] public AudioClip CallMomSound { get; private set; }

        [Header("Footsteps")]
        [field: SerializeField] public AudioClip WalkOnGrass    { get; private set; }
        [field: SerializeField] public AudioClip WalkOnDryGrass { get; private set; }
        [field: SerializeField] public AudioClip WalkOnWetGrass { get; private set; }
    }
}
