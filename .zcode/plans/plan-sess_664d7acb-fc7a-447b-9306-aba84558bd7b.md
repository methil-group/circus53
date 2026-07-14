### 1. Créer `PlayerSounds` (ScriptableObject singleton)

**Fichier** : `Assets/Scripts/Core/Player/PlayerSounds.cs`

- Hérite de `SingletonScriptableObject<PlayerSounds>` → chargé auto depuis `Resources/`
- Contient `[field: SerializeField] public AudioClip CallMomSound { get; private set; }`
- Extensible : on pourra y ajouter d'autres sons plus tard

**Asset** : `Assets/Resources/ScriptableObjects/PlayerSounds.asset` → à créer une fois le script compilé (clic droit → Create → Player Sounds)

### 2. Mettre à jour `PlayerInteraction`

- Garde la référence `_callMomAudioSource` (AudioSource)
- Récupère le clip via `PlayerSounds.Instance.CallMomSound` au lieu de `_audioSource.clip`