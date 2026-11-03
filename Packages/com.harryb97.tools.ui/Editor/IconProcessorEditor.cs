using UnityEditor;
using UnityEngine;

namespace HarryTools.UI.Editor
{
    [CustomEditor(typeof(IconProcessor))]
    public class IconProcessorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Write <icon=name> (default set) or <icon=set:name> in the text.", MessageType.None);

            if (GUILayout.Button("Open Icon Browser"))
                IconBrowserWindow.Open();
        }
    }
}
