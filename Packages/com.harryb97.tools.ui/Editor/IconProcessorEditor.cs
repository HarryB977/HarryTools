using System.Collections.Generic;
using HarryTools.UI;
using UnityEditor;
using UnityEngine;

namespace HarryTools.UI.Editor
{
    [CustomEditor(typeof(IconProcessor))]
    public class IconProcessorEditor : UnityEditor.Editor
    {
        string _search = "";
        Vector2 _scroll;
        readonly List<KeyValuePair<string, string>> _results = new();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Icon Search", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _search = EditorGUILayout.TextField("Search", _search);
            if (EditorGUI.EndChangeCheck())
                UpdateResults();

            if (_results.Count == 0 && !string.IsNullOrEmpty(_search))
            {
                EditorGUILayout.HelpBox("No icons found.", MessageType.Info);
                return;
            }

            if (_results.Count <= 0)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(200));

            foreach (var kvp in _results)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(kvp.Key, GUILayout.Height(18));

                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                    EditorGUIUtility.systemCopyBuffer = $"<icon={kvp.Key}>";

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void UpdateResults()
        {
            _results.Clear();

            if (string.IsNullOrEmpty(_search))
                return;

            var lower = _search.ToLowerInvariant();

            foreach (var kvp in MaterialIcons.ICONS_MAP)
            {
                if (kvp.Key.Contains(lower))
                    _results.Add(kvp);

                if (_results.Count >= 50)
                    break;
            }
        }
    }
}
