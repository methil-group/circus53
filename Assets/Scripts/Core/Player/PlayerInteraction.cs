using System;
using Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Player
{
    /// <summary>
    /// Gestion des interactions du joueur.
    /// Pour l'instant : appeler sa maman avec la touche E.
    /// </summary>
    [Serializable]
    public class PlayerInteraction : Updatable<PlayerController>
    {
        [Header("Call Mom")]
        [SerializeField] private AudioSource _callMomAudioSource;

        [Header("Inputs Reference")]
        [SerializeField] private InputActionReference _callMomAction;

        // =======================================================================

        public override void Start(PlayerController controller)
        {
            if (_callMomAction != null && _callMomAction.action != null)
            {
                _callMomAction.action.Enable();
            }
        }

        public override void Update(PlayerController controller)
        {
            if (_callMomAudioSource == null) return;

            bool pressed = false;

            if (_callMomAction != null && _callMomAction.action != null)
            {
                pressed = _callMomAction.action.triggered;
            }
            else if (Keyboard.current != null)
            {
                pressed = Keyboard.current.eKey.wasPressedThisFrame;
            }

            if (pressed)
            {
                var sound = PlayerSounds.Instance != null ? PlayerSounds.Instance.CallMomSound : null;
                if (sound != null)
                    _callMomAudioSource.PlayOneShot(sound);
            }
        }
    }
}
