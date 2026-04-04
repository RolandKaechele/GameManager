using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace GameManager.Runtime
{
    // -------------------------------------------------------------------------
    // GameState
    // -------------------------------------------------------------------------

    /// <summary>High-level state of the game loop.</summary>
    public enum GameState { MainMenu, Loading, Playing, Paused, GameOver, Victory }

    // -------------------------------------------------------------------------
    // ChapterDefinition
    // -------------------------------------------------------------------------

    /// <summary>
    /// Describes a single chapter or mission.
    /// Serializable so it can be defined in the Inspector and loaded from JSON.
    /// </summary>
    [Serializable]
    public class ChapterDefinition
    {
        [Tooltip("Unique identifier (e.g. \"chapter_01\").")]
        public string id;

        [Tooltip("Human-readable name shown in the UI.")]
        public string displayName;

        [Tooltip("Unity scene name to load for this chapter.")]
        public string sceneName;

        [Tooltip("Ordering index (1-based). Used for sorting and LoadNextChapter().")]
        public int index;

        [Tooltip("Save flags that must all be set before this chapter is considered unlocked (requires GAMEMANAGER_SM).")]
        public string[] requiredFlags;
    }

    // -------------------------------------------------------------------------
    // JSON wrapper — JsonUtility cannot deserialise top-level arrays
    // -------------------------------------------------------------------------

    [Serializable]
    internal class ChapterManifestJson
    {
        public ChapterDefinition[] chapters;
    }

    // -------------------------------------------------------------------------
    // GameManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// <b>GameManager</b> is the central game-state coordinator.
    ///
    /// <para><b>Responsibilities:</b>
    /// <list type="number">
    ///   <item>Drive the high-level game state machine (<see cref="GameState"/>).</item>
    ///   <item>Orchestrate chapter/mission transitions with scene loading.</item>
    ///   <item>Manage pause/resume and <c>Time.timeScale</c>.</item>
    ///   <item>Maintain a chapter registry loaded from the Inspector and/or a JSON manifest.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Modding / JSON:</b> Enable <c>loadChaptersFromJson</c> and place a
    /// <c>game_config.json</c> in <c>StreamingAssets/</c>.
    /// JSON entries are <b>merged by id</b>: JSON overrides Inspector entries with the same id and can add new ones.</para>
    ///
    /// <para><b>Optional integration defines:</b>
    /// <list type="bullet">
    ///   <item><c>GAMEMANAGER_SM</c>  — SaveManager: chapter unlock flag evaluation; auto-save on StartNewGame.</item>
    ///   <item><c>GAMEMANAGER_MLF</c> — MapLoaderFramework: chapter scene loads routed through MapLoader.</item>
    ///   <item><c>GAMEMANAGER_EM</c>  — EventManager: state changes and chapter loads broadcast as named GameEvents.</item>
    /// </list>
    /// </para>
    /// </summary>
    [AddComponentMenu("GameManager/Game Manager")]
    [DisallowMultipleComponent]
#if ODIN_INSPECTOR
    public class GameManager : SerializedMonoBehaviour
#else
    public class GameManager : MonoBehaviour
#endif
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("State")]
        [Tooltip("Game state set on Awake.")]
        [SerializeField] private GameState initialState = GameState.MainMenu;

        [Header("Chapters")]
        [Tooltip("All chapter definitions for this game.")]
        [SerializeField] private ChapterDefinition[] chapters = Array.Empty<ChapterDefinition>();

        [Tooltip("ID of the first chapter loaded by StartNewGame().")]
        [SerializeField] private string startingChapterId = "chapter_01";

        [Header("Modding / JSON")]
        [Tooltip("When enabled, merge chapter definitions from a JSON file in StreamingAssets/ at startup.")]
        [SerializeField] private bool loadChaptersFromJson = false;

        [Tooltip("Path relative to StreamingAssets/ (e.g. 'game_config.json' or 'Mods/game_config.json').")]
        [SerializeField] private string chaptersJsonPath = "game_config.json";

        [Header("Pause")]
        [Tooltip("Set Time.timeScale to 0 when state enters Paused and back to 1 on resume.")]
        [SerializeField] private bool controlTimeScale = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired when the game state changes. Parameter: new state.</summary>
        public event Action<GameState> OnStateChanged;

        /// <summary>Fired just before a chapter scene is loaded. Parameter: chapter id.</summary>
        public event Action<string> OnBeforeChapterLoad;

        /// <summary>Fired when the caller signals the chapter is done loading. Parameter: chapter id.</summary>
        public event Action<string> OnAfterChapterLoad;

        /// <summary>Fired when the game reaches the Game Over state.</summary>
        public event Action OnGameOver;

        /// <summary>Fired when the game reaches the Victory state.</summary>
        public event Action OnVictory;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------

        private GameState _state;
        private string _currentChapterId;

        private readonly List<ChapterDefinition> _chapters = new();
        private readonly Dictionary<string, ChapterDefinition> _index = new();

        /// <summary>Current high-level game state.</summary>
        public GameState CurrentState => _state;

        /// <summary>ID of the currently active chapter (null if not in a chapter).</summary>
        public string CurrentChapterId => _currentChapterId;

        /// <summary>True when the game is actively playing (state == <see cref="GameState.Playing"/>).</summary>
        public bool IsPlaying => _state == GameState.Playing;

        /// <summary>Read-only chapter list (merged Inspector + JSON, sorted by index).</summary>
        public IReadOnlyList<ChapterDefinition> Chapters => _chapters;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            BuildIndex();
            if (loadChaptersFromJson) LoadChaptersJson();
            SetStateInternal(initialState);
        }

        // -------------------------------------------------------------------------
        // Index
        // -------------------------------------------------------------------------

        private void BuildIndex()
        {
            _chapters.Clear();
            _index.Clear();
            foreach (var c in chapters)
            {
                if (c == null || string.IsNullOrEmpty(c.id)) continue;
                _chapters.Add(c);
                _index[c.id] = c;
            }
        }

        private void LoadChaptersJson()
        {
            string path = Path.Combine(Application.streamingAssetsPath, chaptersJsonPath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[GameManager] JSON not found: {path}");
                return;
            }
            try
            {
                var wrapper = JsonUtility.FromJson<ChapterManifestJson>(File.ReadAllText(path));
                if (wrapper?.chapters == null) return;
                foreach (var c in wrapper.chapters)
                {
                    if (c == null || string.IsNullOrEmpty(c.id)) continue;
                    if (_index.ContainsKey(c.id))
                    {
                        int i = _chapters.FindIndex(x => x.id == c.id);
                        if (i >= 0) _chapters[i] = c;
                        _index[c.id] = c;
                    }
                    else
                    {
                        _chapters.Add(c);
                        _index[c.id] = c;
                    }
                }
                _chapters.Sort((a, b) => a.index.CompareTo(b.index));
                Debug.Log($"[GameManager] Chapter manifest merged from {path}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Failed to load chapters JSON: {ex.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // State machine
        // -------------------------------------------------------------------------

        private void SetStateInternal(GameState s)
        {
            _state = s;
            if (controlTimeScale)
                Time.timeScale = (s == GameState.Paused) ? 0f : 1f;
        }

        /// <summary>Transition the game to <paramref name="newState"/>. Fires <see cref="OnStateChanged"/>.</summary>
        public void ChangeState(GameState newState)
        {
            if (_state == newState) return;
            SetStateInternal(newState);
            OnStateChanged?.Invoke(newState);
#if GAMEMANAGER_EM
            FindFirstObjectByType<EventManager.Runtime.EventManager>()?.Fire("GameStateChanged", newState.ToString());
#endif
            Debug.Log($"[GameManager] State → {newState}");
        }

        /// <summary>Pause the game. Sets <see cref="GameState.Paused"/> and Time.timeScale = 0 (if enabled).</summary>
        public void Pause()  => ChangeState(GameState.Paused);

        /// <summary>Resume gameplay. Sets <see cref="GameState.Playing"/> and restores Time.timeScale.</summary>
        public void Resume() => ChangeState(GameState.Playing);

        /// <summary>Trigger the Game Over state and fire <see cref="OnGameOver"/>.</summary>
        public void TriggerGameOver()
        {
            ChangeState(GameState.GameOver);
            OnGameOver?.Invoke();
#if GAMEMANAGER_EM
            FindFirstObjectByType<EventManager.Runtime.EventManager>()?.Fire("GameOver");
#endif
        }

        /// <summary>Trigger the Victory state and fire <see cref="OnVictory"/>.</summary>
        public void TriggerVictory()
        {
            ChangeState(GameState.Victory);
            OnVictory?.Invoke();
#if GAMEMANAGER_EM
            FindFirstObjectByType<EventManager.Runtime.EventManager>()?.Fire("Victory");
#endif
        }

        // -------------------------------------------------------------------------
        // Chapter loading
        // -------------------------------------------------------------------------

        /// <summary>
        /// Load the chapter identified by <paramref name="chapterId"/>.
        /// Fires <see cref="OnBeforeChapterLoad"/>, transitions to <see cref="GameState.Loading"/>, then loads the scene.
        /// </summary>
        public void LoadChapter(string chapterId)
        {
            if (!_index.TryGetValue(chapterId, out var chapter))
            {
                Debug.LogWarning($"[GameManager] Unknown chapter id '{chapterId}'.");
                return;
            }
            _currentChapterId = chapterId;
            OnBeforeChapterLoad?.Invoke(chapterId);
            ChangeState(GameState.Loading);
#if GAMEMANAGER_EM
            FindFirstObjectByType<EventManager.Runtime.EventManager>()?.Fire("ChapterLoad", chapterId);
#endif
#if GAMEMANAGER_MLF
            var mlm = FindFirstObjectByType<MapLoaderFramework.Runtime.MapLoaderManager>();
            if (mlm != null) mlm.LoadMap(chapter.sceneName);
            else SceneManager.LoadScene(chapter.sceneName);
#else
            SceneManager.LoadScene(chapter.sceneName);
#endif
        }

        /// <summary>Load the chapter at the given zero-based <paramref name="listIndex"/>.</summary>
        public void LoadChapter(int listIndex)
        {
            if (listIndex < 0 || listIndex >= _chapters.Count)
            {
                Debug.LogWarning($"[GameManager] Chapter list index {listIndex} out of range.");
                return;
            }
            LoadChapter(_chapters[listIndex].id);
        }

        /// <summary>
        /// Load the next chapter in sequence after the current one.
        /// Calls <see cref="TriggerVictory"/> if there is no next chapter.
        /// </summary>
        public void LoadNextChapter()
        {
            int idx = _chapters.FindIndex(c => c.id == _currentChapterId);
            if (idx < 0 || idx + 1 >= _chapters.Count)
            {
                TriggerVictory();
                return;
            }
            LoadChapter(_chapters[idx + 1].id);
        }

        /// <summary>
        /// Start a new game from <see cref="startingChapterId"/>.
        /// With <c>GAMEMANAGER_SM</c>, saves the current slot before loading.
        /// </summary>
        public void StartNewGame()
        {
#if GAMEMANAGER_SM
            var sm = FindFirstObjectByType<SaveManager.Runtime.SaveManager>();
            sm?.Save(sm.ActiveSlot);
#endif
            LoadChapter(startingChapterId);
        }

        /// <summary>
        /// Call this from scene initialization code once the chapter scene is fully ready.
        /// Transitions to <see cref="GameState.Playing"/> and fires <see cref="OnAfterChapterLoad"/>.
        /// </summary>
        public void NotifyChapterLoaded()
        {
            ChangeState(GameState.Playing);
            OnAfterChapterLoad?.Invoke(_currentChapterId);
#if GAMEMANAGER_EM
            FindFirstObjectByType<EventManager.Runtime.EventManager>()?.Fire("ChapterLoaded", _currentChapterId);
#endif
        }

        // -------------------------------------------------------------------------
        // Chapter queries
        // -------------------------------------------------------------------------

        /// <summary>Returns the <see cref="ChapterDefinition"/> for <paramref name="id"/>, or null if not found.</summary>
        public ChapterDefinition GetChapter(string id) =>
            _index.TryGetValue(id, out var c) ? c : null;

        /// <summary>
        /// Returns true if <paramref name="chapterId"/> is unlocked.
        /// Without <c>GAMEMANAGER_SM</c>, all chapters with no required flags are considered unlocked.
        /// </summary>
        public bool IsChapterUnlocked(string chapterId)
        {
            if (!_index.TryGetValue(chapterId, out var chapter)) return false;
            if (chapter.requiredFlags == null || chapter.requiredFlags.Length == 0) return true;
#if GAMEMANAGER_SM
            var sm = FindFirstObjectByType<SaveManager.Runtime.SaveManager>();
            if (sm == null) return true;
            foreach (var flag in chapter.requiredFlags)
                if (!sm.IsSet(flag)) return false;
            return true;
#else
            return true;
#endif
        }
    }
}
