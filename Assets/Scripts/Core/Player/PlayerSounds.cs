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
        [field: SerializeField, Tooltip("Sons de pas (un est choisi au hasard, joué en boucle quand le joueur marche).")]
        public AudioClip[] WalkSounds { get; private set; }

        [Header("Dialogue")]
        [field: SerializeField, Tooltip("Sons de machine à écrire (un est choisi au hasard à chaque dialogue).")]
        public AudioClip[] TypingSounds { get; private set; }
    }
}
