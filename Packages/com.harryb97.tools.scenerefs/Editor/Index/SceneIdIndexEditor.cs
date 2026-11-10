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
        private readonly HashSet<string> conflictGuids = new HashSet<string>();
        private readonly List<string> conflictReports = new List<string>();

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

            DrawConflicts(_index);

            EditorGUILayout.Space();
            search = searchField.OnGUI(search);
            Filter(_index);
            EditorGUILayout.LabelField($"{filtered.Count} of {_index.Entries.Count} ids", EditorStyles.miniLabel);

            DrawTableHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(400f));
            for (int _i = 0; _i < filtered.Count; _i++) DrawRow(filtered[_i], conflictGuids.Contains(filtered[_i].Guid));
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

        private void DrawConflicts(SceneIdIndex _index)
        {
            conflictGuids.Clear();
            conflictReports.Clear();
            List<SceneIdConflict> _conflicts = SceneIdValidator.Validate(_index.Entries, _index.PrefabsWithIds);
            for (int _i = 0; _i < _conflicts.Count; _i++)
            {
                conflictReports.Add(_conflicts[_i].ToReport());
                for (int _j = 0; _j < _conflicts[_i].Entries.Count; _j++) conflictGuids.Add(_conflicts[_i].Entries[_j].Guid);
            }

            for (int _i = 0; _i < conflictReports.Count; _i++)
                EditorGUILayout.HelpBox(conflictReports[_i], MessageType.Error);
        }

        private static void DrawRow(SceneIdIndexEntry _entry, bool _conflict)
        {
            Color _previous = GUI.contentColor;
            if (_conflict) GUI.contentColor = new Color(1f, 0.35f, 0.35f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(_entry.Id, GUILayout.Width(EditorGUIUtility.labelWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.LabelField(_entry.SceneName, GUILayout.Width(SceneColumnWidth));
                EditorGUILayout.LabelField(new GUIContent(_entry.HierarchyPath, _entry.HierarchyPath));
            }

            GUI.contentColor = _previous;
        }
    }
}
