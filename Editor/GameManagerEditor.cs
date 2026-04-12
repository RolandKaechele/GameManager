#if UNITY_EDITOR
using GameManager.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameManager.Editor
{
    /// <summary>
    /// Custom Inspector for <see cref="GameManager.Runtime.GameManager"/>.
    /// Validates configuration, shows the live state machine, and provides per-chapter load buttons at runtime.
    /// </summary>
    [CustomEditor(typeof(GameManager.Runtime.GameManager))]
    public class GameManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open JSON Editor")) GameConfigJsonEditorWindow.ShowWindow();

            EditorGUILayout.Space(6);

            // ── Validation ──────────────────────────────────────────────────────

            var startingProp  = serializedObject.FindProperty("startingChapterId");
            var chaptersProp  = serializedObject.FindProperty("chapters");

            if (startingProp != null && string.IsNullOrEmpty(startingProp.stringValue))
                EditorGUILayout.HelpBox(
                    "Starting Chapter ID is empty — StartNewGame() will not load any chapter.",
                    MessageType.Warning);

            if (chaptersProp != null && chaptersProp.arraySize == 0)
                EditorGUILayout.HelpBox(
                    "No chapters defined. Add chapters in the Inspector or enable JSON loading.",
                    MessageType.Info);

            // ── Runtime controls (Play Mode only) ───────────────────────────────

            if (!Application.isPlaying) return;

            var mgr = (GameManager.Runtime.GameManager)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State",   mgr.CurrentState.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Chapter", mgr.CurrentChapterId ?? "(none)");

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Pause"))      mgr.Pause();
            if (GUILayout.Button("Resume"))     mgr.Resume();
            if (GUILayout.Button("Game Over"))  mgr.TriggerGameOver();
            if (GUILayout.Button("Victory"))    mgr.TriggerVictory();
            EditorGUILayout.EndHorizontal();

            // ── Chapter list ────────────────────────────────────────────────────

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Chapters", EditorStyles.miniBoldLabel);

            var chapters = mgr.Chapters;
            if (chapters.Count == 0)
            {
                EditorGUILayout.LabelField("  (none)");
            }
            else
            {
                foreach (var c in chapters)
                {
                    if (c == null) continue;
                    bool unlocked = mgr.IsChapterUnlocked(c.id);
                    bool current  = mgr.CurrentChapterId == c.id;
                    EditorGUILayout.BeginHorizontal();
                    string label = $"  [{c.index:D2}]  {c.displayName ?? c.id}";
                    EditorGUILayout.LabelField(label + (current ? "  ►" : ""));
                    EditorGUILayout.LabelField(unlocked ? "✓" : "—", GUILayout.Width(20));
                    if (GUILayout.Button("Load", GUILayout.Width(50))) mgr.LoadChapter(c.id);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start New Game"))    mgr.StartNewGame();
            if (GUILayout.Button("Load Next Chapter")) mgr.LoadNextChapter();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
