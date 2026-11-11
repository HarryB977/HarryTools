using HarryTools.DomainReload;
using UnityEditor;
using UnityEngine;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Draws the id row below Tag and Layer in the GameObject inspector header.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneIdHeader
    {
        // ==================== VARIABLES ====================

        private static readonly GUIContent label = new GUIContent("ID", "Readable id used by scene references.");
        private static readonly GUIContent addContent = new GUIContent("Add ID", "Adds an id based on the object's name.");
        private static readonly GUIContent regenerateContent = new GUIContent("⟳", "Regenerate the internal id. The visible id stays.");
        private static readonly GUIContent removeContent = new GUIContent("✕", "Remove the id.");
        private const string MixedText = "—";

        // Conflict lookup cache: recomputed only when the target or the text changes.
        [ClearOnReload] private static int cachedTarget;
        [ClearOnReload] private static string cachedText;
        [ClearOnReload] private static string cachedConflict;
        [ClearOnReload] private static bool cacheValid;

        // ==================== SETUP ====================

        static SceneIdHeader()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= OnHeaderGUI;
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnHeaderGUI;
            Selection.selectionChanged -= InvalidateCache;
            Selection.selectionChanged += InvalidateCache;
            Undo.undoRedoPerformed -= InvalidateCache;
            Undo.undoRedoPerformed += InvalidateCache;
        }

        // ==================== GUI ====================

        private static void OnHeaderGUI(UnityEditor.Editor _editor)
        {
            if (!(_editor.target is GameObject)) return;

            Object[] _objects = _editor.targets;
            GameObject[] _targets = new GameObject[_objects.Length];
            for (int _i = 0; _i < _objects.Length; _i++)
            {
                _targets[_i] = _objects[_i] as GameObject;
                if (_targets[_i] == null || !SceneRefSync.IsSceneObject(_targets[_i])) return;
            }

            if (_targets.Length == 1) DrawSingle(_targets[0]);
            else DrawMulti(_targets);
        }

        private static void DrawSingle(GameObject _target)
        {
            SceneId _id = _target.GetComponent<SceneId>();
            if (_id == null || string.IsNullOrEmpty(_id.Guid))
            {
                DrawAddRow(new[] { _target });
                return;
            }

            string _conflict = ConflictFor(_target, _id.Id);
            Color _previous = GUI.contentColor;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(18f));
            if (_conflict != null) GUI.contentColor = new Color(1f, 0.4f, 0.4f);
            EditorGUI.BeginChangeCheck();
            string _text = EditorGUILayout.TextField(_id.Id);
            GUI.contentColor = _previous;
            bool _edited = EditorGUI.EndChangeCheck();

            bool _regenerate = GUILayout.Button(regenerateContent, EditorStyles.miniButtonLeft, GUILayout.Width(24f));
            bool _remove = GUILayout.Button(removeContent, EditorStyles.miniButtonRight, GUILayout.Width(24f));
            EditorGUILayout.EndHorizontal();

            if (_conflict != null) EditorGUILayout.HelpBox($"Used by {_conflict}", MessageType.Error);

            // An empty id is not saved: the field keeps the previous one.
            if (_edited && !string.IsNullOrWhiteSpace(_text))
            {
                SceneIdHeaderActions.TrySetId(_target, _text, out _);
                InvalidateCache();
            }
            else if (_regenerate)
            {
                if (EditorUtility.DisplayDialog("Regenerate internal id", "References to this object will break.", "Regenerate", "Cancel"))
                    SceneIdHeaderActions.RegenerateGuid(_target);
            }
            else if (_remove)
            {
                SceneIdHeaderActions.Remove(_target);
                InvalidateCache();
            }
        }

        private static void DrawMulti(GameObject[] _targets)
        {
            bool _anyMissing = false;
            bool _mixed = false;
            string _first = null;
            for (int _i = 0; _i < _targets.Length; _i++)
            {
                SceneId _id = _targets[_i].GetComponent<SceneId>();
                if (_id == null || string.IsNullOrEmpty(_id.Guid))
                {
                    _anyMissing = true;
                    continue;
                }

                if (_first == null) _first = _id.Id;
                else if (!SceneIdNaming.Comparer.Equals(_first, _id.Id)) _mixed = true;
            }

            if (_anyMissing)
            {
                DrawAddRow(_targets);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(18f));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(_mixed ? MixedText : _first);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawAddRow(GameObject[] _targets)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(18f));
            if (GUILayout.Button(addContent, GUILayout.Width(70f)))
            {
                SceneIdHeaderActions.Add(_targets);
                InvalidateCache();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ==================== CACHE ====================

        private static string ConflictFor(GameObject _target, string _text)
        {
            int _key = _target.GetInstanceID();
            if (cacheValid && cachedTarget == _key && cachedText == _text) return cachedConflict;

            cachedConflict = SceneIdHeaderActions.FindConflict(_target, _text);
            cachedTarget = _key;
            cachedText = _text;
            cacheValid = true;
            return cachedConflict;
        }

        private static void InvalidateCache() => cacheValid = false;
    }
}
