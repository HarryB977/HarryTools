using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Read-only view of the index: search, table of ids and the Rebuild Index button.
    /// </summary>
    [CustomEditor(typeof(SceneIdIndex))]
    internal sealed class SceneIdIndexEditor : UnityEditor.Editor
    {
        // ==================== VARIABLES ====================

        private const float SceneColumnWidth = 140f;

        private SearchField searchField;
        private string search = string.Empty;
        private Vector2 scroll;
        private readonly List<SceneIdIndexEntry> filtered = new List<SceneIdIndexEntry>();

        // ==================== LIFECYCLE ====================

        private void OnEnable()
        {
            searchField = new SearchField();
            SceneIdIndexMaintainer.Rebuilt += Repaint;
        }

        private void OnDisable() => SceneIdIndexMaintainer.Rebuilt -= Repaint;

        // ==================== GUI ====================

        public override void OnInspectorGUI()
        {
            SceneIdIndex _index = (SceneIdIndex)target;

            EditorGUILayout.HelpBox("Cache of every scene id in the project. It is updated automatically; Rebuild Index reads every scene and prefab again.", MessageType.None);
            // Rebuild opens scenes and shows dialogs, so it runs outside this GUI pass.
            if (GUILayout.Button("Rebuild Index"))
            {
                EditorApplication.delayCall += SceneIdIndexMaintainer.Rebuild;
                GUIUtility.ExitGUI();
            }

            if (_index.StaleScenes.Count > 0)
                EditorGUILayout.HelpBox($"{_index.StaleScenes.Count} scene(s) waiting to be read again.", MessageType.Info);

            EditorGUILayout.Space();
            search = searchField.OnGUI(search);
            Filter(_index);
            EditorGUILayout.LabelField($"{filtered.Count} of {_index.Entries.Count} ids", EditorStyles.miniLabel);

            DrawTableHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(400f));
            for (int _i = 0; _i < filtered.Count; _i++) DrawRow(filtered[_i]);
            EditorGUILayout.EndScrollView();

            if (_index.PrefabsWithIds.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Prefabs with ids", EditorStyles.boldLabel);
                for (int _i = 0; _i < _index.PrefabsWithIds.Count; _i++)
                    EditorGUILayout.LabelField(_index.PrefabsWithIds[_i], EditorStyles.miniLabel);
            }
        }

        // ==================== PRIVATE ====================

        private void Filter(SceneIdIndex _index)
        {
            filtered.Clear();
            for (int _i = 0; _i < _index.Entries.Count; _i++)
            {
                SceneIdIndexEntry _entry = _index.Entries[_i];
                if (string.IsNullOrEmpty(search) || Matches(_entry.Id) || Matches(_entry.SceneName) || Matches(_entry.HierarchyPath))
                    filtered.Add(_entry);
            }
        }

        private bool Matches(string _value) =>
            !string.IsNullOrEmpty(_value) && _value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void DrawTableHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("ID", EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
                EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel, GUILayout.Width(SceneColumnWidth));
                EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
            }
        }

        private static void DrawRow(SceneIdIndexEntry _entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(_entry.Id, GUILayout.Width(EditorGUIUtility.labelWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.LabelField(_entry.SceneName, GUILayout.Width(SceneColumnWidth));
                EditorGUILayout.LabelField(new GUIContent(_entry.HierarchyPath, _entry.HierarchyPath));
            }
        }
    }
}
