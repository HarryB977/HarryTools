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
            EditorGUILayout.HelpBox("Escribe <icon=nombre> (set por defecto) o <icon=set:nombre> en el texto.", MessageType.None);

            if (GUILayout.Button("Abrir buscador de iconos"))
                IconBrowserWindow.Open();
        }
    }
}
