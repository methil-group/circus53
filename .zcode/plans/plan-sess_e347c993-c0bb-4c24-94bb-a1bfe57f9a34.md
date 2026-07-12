# Plan : Renommage DitherFX → MethilDither + Documentation française

## Phase 1 — Renommage des identifiants code (6 fichiers C# + 1 shader)

### Runtime/DitherFx.cs
- `DitherFx` → `MethilDither` (classe)
- `DitherPass` → `MethilDitherPass` (type du champ `_pass`)
- `DitherFx` → `MethilDither` (type dans `PassExecution._owner`)

### Runtime/Dither/DitherPass.cs
- `DitherPass` → `MethilDitherPass` (classe + toutes les refs internes)
- `[ShaderName("Hidden/VolFx/Dither")]` → `[ShaderName("Hidden/VolFx/MethilDither")]`
- Tous les `Shader.PropertyToID` : `_DitherTex` → `_MethilDitherTex`, `_Dither` → `_MethilDither`, `_DitherMad` → `_MethilDitherMad`
- Variables locales : `_dither` → `_dither` (inchangé, c'est une variable locale), `_ditherMad` → idem
- Keywords shader : `"DITHER"` → `"METHILDITHER"`
- Messages Debug.LogError : `[DitherFx]` → `[MethilDither]`

### Runtime/Dither/DitherVol.cs
- `DitherVol` → `MethilDitherVol` (classe)
- `[VolumeComponentMenu("VolFx/Dither")]` → `[VolumeComponentMenu("VolFx/MethilDither")]`
- `DitherPass.Mode` → `MethilDitherPass.Mode`

### Runtime/Dither/Dither.shader
- `Shader "Hidden/VolFx/Dither"` → `Shader "Hidden/VolFx/MethilDither"`
- `Name "Dither"` → `Name "MethilDither"`
- `#pragma multi_compile_local DITHER NOISE` → `METHILDITHER NOISE`
- `#if DITHER` → `#if METHILDITHER`
- Toutes les props : `_DitherTex` → `_MethilDitherTex`, `_Dither` → `_MethilDither`, `_DitherMad` → `_MethilDitherMad`

### Runtime/Common/VolFx.Pass.cs
- `public DitherFx _owner` → `public MethilDither _owner`

### Editor (4 fichiers) — aucun changement nécessaire (pas de refs directes à DitherFx)

## Phase 2 — Fichiers de configuration

### package.json
- `"name": "com.ditherfx.ditherfx"` → `"com.methildither.core"`
- `"displayName": "DitherFx"` → `"MethilDither"`
- `"description": "Palette Dithering for Unity Urp and VolFx"` → `"Effet de post-processing de dithering par palette pour Unity URP et VolFx"`

### Masking.Runtime.asmdef + Masking.Editor.asmdef
- `"name": "com.ditherfx.volfx"` → `"name": "com.methildither.volfx"`

### Assets/Settings/PC_Renderer.asset
- `m_Name: DitherFx` → `m_Name: MethilDither`
- `m_EditorClassIdentifier: Masking.Runtime::VolFx.DitherFx` → `...::VolFx.MethilDither`
- `m_EditorClassIdentifier: Masking.Runtime::VolFx.DitherPass` → `...::VolFx.MethilDitherPass`

### Assets/Settings/SampleSceneProfile.asset
- `m_Name: DitherVol` → `m_Name: MethilDitherVol`
- `m_EditorClassIdentifier: ...::VolFx.DitherVol` → `...::VolFx.MethilDitherVol`

### Assets/Settings/Scenes/GameVolume.asset + MainMenuVolume.asset
- Idem : `DitherVol` → `MethilDitherVol`

### Assets/Resources/Assets/Materials/DitherMaterial.mat
- `m_Name: DitherMaterial` → `m_Name: MethilDitherMaterial`

## Phase 3 — Documentation française sur TOUS les fichiers .cs

Chaque fichier reçoit :
- Un en-tête de fichier avec description du module
- Des commentaires `///` (XMLdoc) sur chaque classe, méthode, propriété, champ public
- Des commentaires `//` inline sur la logique non-triviale

### Fichiers Editor (4)
- **CurveValueDraver.cs** — PropertyDrawer qui affiche une CurveValue, conversion curve→texture 32px
- **GradientDrawer.cs** — PropertyDrawer qui affiche un GradientValue, conversion gradient→texture 32px, fallback réflexion Unity <2022
- **OptionalDrawer.cs** — PropertyDrawer pour Optional<T> avec toggle activer/désactiver + cas spécial LayerMask
- **PassDrawer.cs** — PropertyDrawer principal, crée un ScriptableObject inline comme sous-asset, méthodes statiques publiques pour la réutilisation

### Fichiers Runtime (12)
- **DitherFx.cs** — ScriptableRendererFeature principal, point d'entrée URP, RenderGraph API
- **DitherPass.cs** — Passe de dithering, génération LUT, cache de palettes, modes Dither/Noise, WebGL-safe
- **DitherVol.cs** — VolumeComponent exposant les paramètres aux Volume Profiles
- **VolFx.Pass.cs** — Classe de base abstraite pour les passes (Init/Validate/Invoke/Cleanup)
- **CurveParameter.cs** — VolumeParameter<T> pour CurveValue
- **CurveValue.cs** — Valeur de courbe stockée en courbe + texture 32px
- **GradientParameter.cs** — VolumeParameter<T> pour GradientValue
- **GradientValue.cs** — Valeur de dégradé stockée en dégradé + texture 32px
- **Optional.cs** — Wrapper générique Optional<T> avec enable/disable
- **RenderTarget.cs** — Wrapper RTHandle pour render textures temporaires
- **ShaderNameAttribute.cs** — Attribut liant une classe de passe à son shader
- **Utils.cs** — Utilitaires statiques (Blit, FullscreenMesh, extensions math)

## Phase 4 — Vérification finale
- grep complet pour s'assurer qu'aucune référence à "DitherFx" ou "NullTale" ne persiste
- Vérification que les .asset et .mat sont bien mis à jour