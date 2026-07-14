## Architecture du light show

### 1. Enum `LightShowMode`
- `Sequential` — s'allument une par une
- `Flicker` — clignotent toutes en même temps
- `Looping` — séquence → flicker → séquence → flicker...

### 2. `BulbState` (interne)
Chaque ampoule track dans une petite struct : son `GameObject`, son `Renderer`, son `Material` dupliqué, sa `Light`

### 3. `SetupBulbs()` → crée les états + démarre la coroutine
- Duplique le matériau
- Crée la Point Light enfant
- Stocke tout dans une `List<BulbState>`
- Lance `_LightShowRoutine()` selon le mode

### 4. Coroutines
- **Sequential** : pour chaque bulb → active emission + light → attend `_lightDelay` → suivant
- **Flicker** : boucle infinie → active/désactive toutes les lumières aléatoirement
- **Looping** : Sequential puis Flicker puis Sequential puis Flicker... en boucle

### Nouveaux champs
- `LightShowMode mode` — choix du mode
- `float _lightDelay = 0.5f` — délai entre chaque lampe
- `float _flickerMinInterval / _flickerMaxInterval` — rythme du clignotement