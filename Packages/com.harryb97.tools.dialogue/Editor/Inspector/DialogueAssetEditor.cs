using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Inspector for dialogues: the dialogue options, a summary and a button that opens the graph window, where the nodes are edited </summary>
    [CustomEditor(typeof(DialogueAsset))]
    internal sealed class DialogueAssetEditor : UnityEditor.Editor {
        // ==================== VARIABLES ===================
        #region Private Variables
        private SerializedProperty overrideTime;
        private SerializedProperty time;
        private SerializedProperty overrideAmbientPolicy;
        private SerializedProperty ambientPolicy;
        private SerializedProperty allowSkip;
        private readonly List<DialogueIssue> issues = new List<DialogueIssue>();
        private int issuesDirtyCount = -1;
        #endregion

        // ==================== UNITY ====================
        #region Unity
        private void OnEnable() {
            overrideTime = serializedObject.FindProperty("overrideTime");
            time = serializedObject.FindProperty("time");
            overrideAmbientPolicy = serializedObject.FindProperty("overrideAmbientPolicy");
            ambientPolicy = serializedObject.FindProperty("ambientPolicy");
            allowSkip = serializedObject.FindProperty("allowSkip");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            DrawSettings();
            EditorGUILayout.Space();

            if (!serializedObject.isEditingMultipleObjects) {
                DialogueAsset _asset = (DialogueAsset) target;
                EditorGUILayout.LabelField("Nodes", NodeCount(_asset).ToString());
                EditorGUILayout.LabelField("Issues", IssueSummary(_asset));
                EditorGUILayout.Space();
                if (GUILayout.Button("Open Graph")) DialogueGraphWindow.Open(_asset);
                if (GUILayout.Button("Clean orphan entries")) CleanOrphans(_asset);
            }

            serializedObject.ApplyModifiedProperties();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void DrawSettings() {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(overrideTime);
            using (new EditorGUI.DisabledScope(!overrideTime.boolValue && !overrideTime.hasMultipleDifferentValues)) {
                EditorGUILayout.PropertyField(time);
            }
            EditorGUILayout.PropertyField(overrideAmbientPolicy);
            using (new EditorGUI.DisabledScope(!overrideAmbientPolicy.boolValue && !overrideAmbientPolicy.hasMultipleDifferentValues)) {
                EditorGUILayout.PropertyField(ambientPolicy);
            }
            EditorGUILayout.PropertyField(allowSkip);
        }

        // Checked again only after the asset changed. Prefabs are not scanned here, so ambient launches are unknown
        private string IssueSummary(DialogueAsset _asset) {
            int _dirty = EditorUtility.GetDirtyCount(_asset);
            if (_dirty != issuesDirtyCount) {
                issuesDirtyCount = _dirty;
                issues.Clear();
                DialogueValidator.Validate(_asset, null, issues);
            }
            return DialogueIssueList.Summary(issues);
        }

        private static void CleanOrphans(DialogueAsset _asset) {
            int _count = DialogueTables.Orphans(_asset).Count;
            if (_count == 0) {
                Debug.Log("Dialogue " + _asset.name + ": no orphan table entries.", _asset);
                return;
            }
            string _message = "Remove " + _count + (_count == 1 ? " entry" : " entries") + " that no node or option of " + _asset.name + " uses, from every table of its collection? Undo restores them.";
            if (!EditorUtility.DisplayDialog("Clean orphan entries", _message, "Remove", "Cancel")) return;
            int _removed = DialogueTables.CleanOrphans(_asset);
            Debug.Log("Dialogue " + _asset.name + ": removed " + _removed + " orphan table entries.", _asset);
        }

        private static int NodeCount(DialogueAsset _asset) {
            int _count = 0;
            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                if (_asset.Nodes[_i] != null) _count++;
            }
            return _count;
        }
        #endregion
    }
}
