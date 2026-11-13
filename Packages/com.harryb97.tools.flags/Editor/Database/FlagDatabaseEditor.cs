using UnityEditor;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>Read-only inspector for the <see cref="FlagDatabase"/>. Flags are edited from the Flags window.</summary>
    [CustomEditor(typeof(FlagDatabase))]
    internal sealed class FlagDatabaseEditor : UnityEditor.Editor
    {
        // ==================== UNITY ====================

        public override void OnInspectorGUI()
        {
            var _database = (FlagDatabase)target;

            EditorGUILayout.HelpBox("Flags are edited from the Flags window.", MessageType.Info);

            if (GUILayout.Button("Open Flags Window")) FlagsWindow.Open();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Flags ({_database.Flags.Count})", EditorStyles.boldLabel);
            foreach (FlagDefinition _flag in _database.Flags)
            {
                EditorGUILayout.LabelField(_flag.DisplayPath, _flag.InitialValue ? "initially on" : "initially off");
            }
        }
    }
}
