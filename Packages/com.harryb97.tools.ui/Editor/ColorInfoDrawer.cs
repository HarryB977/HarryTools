using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.UI.Editor
{
    /// <summary> Rol (de serie o personalizado de la paleta activa), contraste y tono de un ColorInfo </summary>
    [CustomPropertyDrawer(typeof(ColorInfo))]
    public class ColorInfoDrawer : PropertyDrawer
    {
        static readonly ColorType[] BuiltInTypes = Array.FindAll((ColorType[])Enum.GetValues(typeof(ColorType)), t => t != ColorType.Custom);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                var rect = EditorGUI.PrefixLabel(line, label);

                var enabledProp = property.FindPropertyRelative("enabled");
                var colorProp = property.FindPropertyRelative("color");
                var contrastProp = property.FindPropertyRelative("contrast");
                var customIdProp = property.FindPropertyRelative("customRoleId");
                var toneProp = property.FindPropertyRelative("tone");

                int previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                const float enableWidth = 16f;
                const float toggleWidth = 80f;
                const float spacing = 4f;

                var enableRect = new Rect(rect.x, rect.y, enableWidth, rect.height);
                var dropdownRect = new Rect(enableRect.xMax + spacing, rect.y, rect.width - enableWidth - toggleWidth - spacing * 2f, rect.height);
                var toggleRect = new Rect(rect.xMax - toggleWidth, rect.y, toggleWidth, rect.height);
                var toneRect = new Rect(dropdownRect.x, rect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                    rect.xMax - dropdownRect.x, rect.height);

                EditorGUI.PropertyField(enableRect, enabledProp, GUIContent.none);

                bool slotActive = !enabledProp.hasMultipleDifferentValues && enabledProp.boolValue;
                using (new EditorGUI.DisabledScope(!slotActive))
                {
                    DrawRoleDropdown(dropdownRect, property, colorProp, customIdProp);

                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = contrastProp.hasMultipleDifferentValues;
                    bool newContrast = EditorGUI.ToggleLeft(toggleRect, "Contraste", contrastProp.boolValue);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck())
                        contrastProp.boolValue = newContrast;

                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = toneProp.hasMultipleDifferentValues;
                    int tone = EditorGUI.IntSlider(toneRect, "Tono", toneProp.intValue, ColorTones.MinTone, ColorTones.MaxTone);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck())
                        toneProp.intValue = tone;
                }

                EditorGUI.indentLevel = previousIndent;
            }
        }

        static void DrawRoleDropdown(Rect rect, SerializedProperty property, SerializedProperty colorProp, SerializedProperty customIdProp)
        {
            var palette = FindPalette(property);
            var labels = new List<string>();
            var customIds = new List<int>();

            foreach (var type in BuiltInTypes)
                labels.Add(type.ToString());

            if (palette)
            {
                foreach (var role in palette.CustomRoles)
                {
                    labels.Add("Personalizado/" + (string.IsNullOrEmpty(role.name) ? $"Rol {role.id}" : role.name));
                    customIds.Add(role.id);
                }
            }

            var current = (ColorType)colorProp.intValue;
            int selected = current == ColorType.Custom
                ? BuiltInTypes.Length + customIds.IndexOf(customIdProp.intValue)
                : Array.IndexOf(BuiltInTypes, current);

            if (current == ColorType.Custom && selected < BuiltInTypes.Length)
            {
                labels.Add($"(rol {customIdProp.intValue} no encontrado)");
                selected = labels.Count - 1;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = colorProp.hasMultipleDifferentValues || customIdProp.hasMultipleDifferentValues;
            int picked = EditorGUI.Popup(rect, selected, labels.ToArray());
            EditorGUI.showMixedValue = false;
            if (!EditorGUI.EndChangeCheck() || picked == selected)
                return;

            if (picked < BuiltInTypes.Length)
            {
                colorProp.intValue = (int)BuiltInTypes[picked];
            }
            else if (picked - BuiltInTypes.Length < customIds.Count)
            {
                colorProp.intValue = (int)ColorType.Custom;
                customIdProp.intValue = customIds[picked - BuiltInTypes.Length];
            }
        }

        /// <summary> Paleta del proveedor más cercano al objeto inspeccionado, o la paleta por defecto </summary>
        static ColorPalette FindPalette(SerializedProperty property)
        {
            if (property.serializedObject.targetObject is Component component)
                return ColorPalette.Resolve(component.GetComponentInParent<IPaletteProvider>(true));
            return ColorPalette.Default;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
