using Framework.ScriptableObjects;
using UnityEngine;

namespace Core.Player
{
    [CreateAssetMenu(menuName = "Dream/Jam/Player Sounds")]
    public class PlayerSounds : SingletonScriptableObject<PlayerSounds>
    {
        [Header("Call Mom")]
        [field: SerializeField] public AudioClip CallMomSound { get; private set; }

        [Header("Footsteps")]
        [field: SerializeField, Tooltip("Sons de pas (un est choisi au hasard, joué en boucle quand le joueur marche).")]
        public AudioClip[] WalkSounds { get; private set; }

        [Header("Dialogue — Typing")]
        [field: SerializeField, Tooltip("Sons de machine à écrire pour le joueur.")]
        public AudioClip[] PlayerTypingSounds { get; private set; }

        [field: SerializeField, Tooltip("Sons de machine à écrire pour les autres personnages.")]
        public AudioClip[] OtherTypingSounds { get; private set; }

        [Header("Ambiance")]
        [field: SerializeField, Tooltip("Ambiance globale jouée en boucle dans la scène du circus.")]
        public AudioClip GlobalAmbientSound { get; private set; }

        [field: SerializeField, Tooltip("Sons d'ambiance aléatoires joués à intervalles irréguliers.")]
        public AudioClip[] RandomAmbientSounds { get; private set; }
    }
}
