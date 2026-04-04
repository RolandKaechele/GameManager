# GameManager

A modular game-state coordinator for Unity.  
Drives the high-level state machine, orchestrates chapter/mission transitions, and manages pause/resume.  
Supports JSON chapter manifests for modding.


## Features

- **State machine** — `MainMenu`, `Loading`, `Playing`, `Paused`, `GameOver`, `Victory` with `ChangeState()` transitions
- **Pause / Resume** — optional `Time.timeScale` control via `Pause()` / `Resume()`
- **Chapter registry** — define chapters (id, display name, scene name, ordering index, required flags) in the Inspector
- **Chapter loading** — `LoadChapter(id)`, `LoadChapter(index)`, `LoadNextChapter()`, `StartNewGame()`
- **Unlock checks** — `IsChapterUnlocked(id)` respects `requiredFlags` evaluated against SaveManager flags
- **JSON / Modding** — load and merge chapter definitions from `StreamingAssets/game_config.json` at startup; JSON entries override Inspector entries by id and can add new ones
- **SaveManager integration** — flag-based chapter unlock evaluation; auto-save on `StartNewGame()` (activated via `GAMEMANAGER_SM`)
- **MapLoaderFramework integration** — chapter transitions routed through MapLoader (activated via `GAMEMANAGER_MLF`)
- **EventManager integration** — state changes and chapter loads broadcast as named GameEvents (activated via `GAMEMANAGER_EM`)
- **StateManager integration** — `GameState` changes are automatically mapped to `AppState` by StateManager's `GameManagerBridge` (consumed via `STATEMANAGER_GM`)
- **Custom Inspector** — live state display, per-chapter load buttons, state controls


## Installation

### Option A — Unity Package Manager (Git URL)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Enter:

   ```
   https://github.com/RolandKaechele/GameManager.git
   ```

### Option B — Clone into Assets

```bash
git clone https://github.com/RolandKaechele/GameManager.git Assets/GameManager
```

### Option C — npm / postinstall

```bash
cd Assets/GameManager
npm install
```

`postinstall.js` creates the required `StreamingAssets/` folder under `Assets/` and optionally copies example JSON files.


## Scene Setup

1. Create a persistent manager GameObject (or reuse an existing one).
2. Attach `GameManager`.
3. Define chapters in the Inspector or enable JSON loading.
4. Set `startingChapterId` to the first chapter's id.
5. Add `DontDestroyOnLoad(gameObject)` if the manager should persist across scenes.


## Quick Start

### 1. Add GameManager to your scene

| Field | Default | Description |
| ----- | ------- | ----------- |
| `initialState` | `MainMenu` | State set on Awake |
| `chapters` | *(empty)* | Chapter definitions array |
| `startingChapterId` | `"chapter_01"` | ID loaded by `StartNewGame()` |
| `loadChaptersFromJson` | `false` | Merge from JSON on Awake |
| `chaptersJsonPath` | `"game_config.json"` | Path relative to `StreamingAssets/` |
| `controlTimeScale` | `true` | Set `Time.timeScale` on Pause/Resume |

### 2. Wire chapter transitions

```csharp
var gm = FindFirstObjectByType<GameManager.Runtime.GameManager>();

// Start a new game
gm.StartNewGame();

// Load a specific chapter
gm.LoadChapter("chapter_05");

// Load by list index
gm.LoadChapter(4);

// Called from chapter scene init code when the scene is fully ready
gm.NotifyChapterLoaded();

// Advance to the next chapter
gm.LoadNextChapter();
```

### 3. React to events

```csharp
gm.OnStateChanged       += state   => Debug.Log($"State: {state}");
gm.OnBeforeChapterLoad  += id      => Debug.Log($"Loading: {id}");
gm.OnAfterChapterLoad   += id      => Debug.Log($"Ready:   {id}");
gm.OnGameOver           += ()      => ShowGameOverScreen();
gm.OnVictory            += ()      => ShowVictoryScreen();
```

### 4. Pause / Resume

```csharp
gm.Pause();            // GameState.Paused, Time.timeScale = 0
gm.Resume();           // GameState.Playing, Time.timeScale = 1
gm.TriggerGameOver();  // GameState.GameOver
gm.TriggerVictory();   // GameState.Victory
```


## JSON / Modding

Enable `loadChaptersFromJson` and place `game_config.json` in `StreamingAssets/`.

```json
{
  "chapters": [
    {
      "id": "chapter_01",
      "displayName": "Angriff der grünen Spinnen",
      "sceneName": "Chapter01",
      "index": 1,
      "requiredFlags": []
    },
    {
      "id": "chapter_02",
      "displayName": "Tödlicher Nebel",
      "sceneName": "Chapter02",
      "index": 2,
      "requiredFlags": ["chapter_01_complete"]
    }
  ]
}
```

JSON and Inspector entries are merged by `id`. JSON entries override Inspector entries with the same id.  
New ids in JSON are appended to the list and sorted by `index`.


## SaveManager Integration (`GAMEMANAGER_SM`)

Add `GAMEMANAGER_SM` to **Edit → Project Settings → Player → Scripting Define Symbols**.

- `IsChapterUnlocked(id)` evaluates each `requiredFlags` entry against `SaveManager.IsSet(flag)`.
- `StartNewGame()` triggers a save on the active slot before loading the starting chapter.

Requires [SaveManager](https://github.com/RolandKaechele/SaveManager) in the project.


## Runtime API

| Member | Description |
| ------ | ----------- |
| `ChangeState(state)` | Transition to a new `GameState` |
| `Pause()` | Transition to `Paused`; sets `Time.timeScale = 0` if enabled |
| `Resume()` | Transition to `Playing`; restores `Time.timeScale` |
| `TriggerGameOver()` | Transition to `GameOver`; fires `OnGameOver` |
| `TriggerVictory()` | Transition to `Victory`; fires `OnVictory` |
| `StartNewGame()` | Load `startingChapterId` (saves first if `GAMEMANAGER_SM`) |
| `LoadChapter(id)` | Load chapter by id |
| `LoadChapter(index)` | Load chapter by list index |
| `LoadNextChapter()` | Load the chapter after the current one; triggers Victory if at end |
| `NotifyChapterLoaded()` | Call from scene init — transitions to `Playing`, fires `OnAfterChapterLoad` |
| `GetChapter(id)` | Returns `ChapterDefinition` or null |
| `IsChapterUnlocked(id)` | True if all required flags are satisfied |
| `CurrentState` | Current `GameState` |
| `CurrentChapterId` | Id of the active chapter (null if none) |
| `IsPlaying` | True when state is `Playing` |
| `Chapters` | `IReadOnlyList<ChapterDefinition>` (merged) |
| `OnStateChanged` | `event Action<GameState>` |
| `OnBeforeChapterLoad` | `event Action<string>` — chapter id |
| `OnAfterChapterLoad` | `event Action<string>` — chapter id |
| `OnGameOver` | `event Action` |
| `OnVictory` | `event Action` |


## Optional Integrations

### SaveManager (`GAMEMANAGER_SM`)

Requires `GAMEMANAGER_SM` define and [SaveManager](https://github.com/RolandKaechele/SaveManager).

### MapLoaderFramework (`GAMEMANAGER_MLF`)

Requires `GAMEMANAGER_MLF` define. Chapter scene transitions call `MapLoaderFramework.Runtime.MapLoader.LoadScene(sceneName)`.

### EventManager (`GAMEMANAGER_EM`)

Requires `GAMEMANAGER_EM` define. The following named GameEvents are fired:

| Event name | When |
| ---------- | ---- |
| `GameStateChanged` | State machine transitions; value = state name |
| `ChapterLoad` | Before chapter scene loads; value = chapter id |
| `ChapterLoaded` | After `NotifyChapterLoaded()`; value = chapter id |
| `GameOver` | TriggerGameOver() |
| `Victory` | TriggerVictory() |


## Dependencies

| Dependency | Required | Notes |
| ---------- | -------- | ----- |
| Unity 2022.3+ | ✓ | |
| SaveManager | optional | Required when `GAMEMANAGER_SM` is defined |
| MapLoaderFramework | optional | Required when `GAMEMANAGER_MLF` is defined |
| EventManager | optional | Required when `GAMEMANAGER_EM` is defined |


## Repository

[https://github.com/RolandKaechele/GameManager](https://github.com/RolandKaechele/GameManager)


## License

MIT — see [LICENSE](LICENSE).
