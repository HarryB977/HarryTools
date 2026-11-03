using HarryTools.UI;
using UnityEditor;
using UnityEngine;

namespace HarryTools.UI.Editor
{
    [CustomPropertyDrawer(typeof(ColorInfo))]
    public class ColorInfoDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var rect = EditorGUI.PrefixLabel(position, label);

                var enabledProp = property.FindPropertyRelative("enabled");
                var colorProp = property.FindPropertyRelative("color");
                var contrastProp = property.FindPropertyRelative("contrast");

                int previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                const float enableWidth = 16f;
                const float toggleWidth = 80f;
                const float spacing = 4f;

                var enableRect = new Rect(rect.x, rect.y, enableWidth, rect.height);
                var dropdownRect = new Rect(
                    enableRect.xMax + spacing, rect.y,
                    rect.width - enableWidth - toggleWidth - spacing * 2f, rect.height);
                var toggleRect = new Rect(rect.xMax - toggleWidth, rect.y, toggleWidth, rect.height);

                EditorGUI.PropertyField(enableRect, enabledProp, GUIContent.none);

                bool slotActive = !enabledProp.hasMultipleDifferentValues && enabledProp.boolValue;
                using (new EditorGUI.DisabledScope(!slotActive))
                {
                    EditorGUI.PropertyField(dropdownRect, colorProp, GUIContent.none);

                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = contrastProp.hasMultipleDifferentValues;
                    bool newContrast = EditorGUI.ToggleLeft(toggleRect, "Contrast", contrastProp.boolValue);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck())
                        contrastProp.boolValue = newContrast;
                }

                EditorGUI.indentLevel = previousIndent;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
