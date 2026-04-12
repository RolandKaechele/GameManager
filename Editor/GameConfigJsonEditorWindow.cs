#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using GameManager.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameManager.Editor
{
    // ────────────────────────────────────────────────────────────────────────────
    // Chapter / Game Config JSON Editor Window
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Editor window for creating and editing <c>game_config.json</c> in StreamingAssets.
    /// Open via <b>JSON Editors → Game Manager</b> or via the Manager Inspector button.
    /// </summary>
    public class GameConfigJsonEditorWindow : EditorWindow
    {
        private const string JsonFileName = "game_config.json";

        private ChapterEditorBridge      _bridge;
        private UnityEditor.Editor       _bridgeEditor;
        private Vector2                  _scroll;
        private string                   _status;
        private bool                     _statusError;

        [MenuItem("JSON Editors/Game Manager")]
        public static void ShowWindow() =>
            GetWindow<GameConfigJsonEditorWindow>("Game Config JSON");

        private void OnEnable()
        {
            _bridge = CreateInstance<ChapterEditorBridge>();
            Load();
        }

        private void OnDisable()
        {
            if (_bridgeEditor != null) DestroyImmediate(_bridgeEditor);
            if (_bridge      != null) DestroyImmediate(_bridge);
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _statusError ? MessageType.Error : MessageType.Info);

            if (_bridge == null) return;
            if (_bridgeEditor == null)
                _bridgeEditor = UnityEditor.Editor.CreateEditor(_bridge);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _bridgeEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(
                Path.Combine("StreamingAssets", JsonFileName),
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50))) Load();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50))) Save();
            EditorGUILayout.EndHorizontal();
        }

        private void Load()
        {
            var path = Path.Combine(Application.streamingAssetsPath, JsonFileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, JsonUtility.ToJson(new ChapterEditorWrapper(), true));
                    AssetDatabase.Refresh();
                }

                var w = JsonUtility.FromJson<ChapterEditorWrapper>(File.ReadAllText(path));
                _bridge.chapters = new List<ChapterDefinition>(
                    w.chapters ?? Array.Empty<ChapterDefinition>());

                if (_bridgeEditor != null) { DestroyImmediate(_bridgeEditor); _bridgeEditor = null; }

                _status     = $"Loaded {_bridge.chapters.Count} chapters.";
                _statusError = false;
            }
            catch (Exception e)
            {
                _status     = $"Load error: {e.Message}";
                _statusError = true;
            }
        }

        private void Save()
        {
            try
            {
                var w    = new ChapterEditorWrapper { chapters = _bridge.chapters.ToArray() };
                var path = Path.Combine(Application.streamingAssetsPath, JsonFileName);
                File.WriteAllText(path, JsonUtility.ToJson(w, true));
                AssetDatabase.Refresh();
                _status     = $"Saved {_bridge.chapters.Count} chapters to {JsonFileName}.";
                _statusError = false;
            }
            catch (Exception e)
            {
                _status     = $"Save error: {e.Message}";
                _statusError = true;
            }
        }
    }

    // ── ScriptableObject bridge ──────────────────────────────────────────────
    internal class ChapterEditorBridge : ScriptableObject
    {
        public List<ChapterDefinition> chapters = new List<ChapterDefinition>();
    }

    // ── Local wrapper mirrors the internal ChapterManifestJson ───────────────
    [Serializable]
    internal class ChapterEditorWrapper
    {
        public ChapterDefinition[] chapters = Array.Empty<ChapterDefinition>();
    }
}
#endif
