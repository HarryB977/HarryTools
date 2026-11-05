using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Audio.Editor {
    /// <summary> Searchable picker for fields of an audio asset type. </summary>
    [CustomPropertyDrawer(typeof(AudioCatalogItem), true)]
    internal sealed class AudioAssetDrawer : PropertyDrawer {
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            if (_property.propertyType != SerializedPropertyType.ObjectReference) {
                EditorGUI.PropertyField(_position, _property, _label, true);
                return;
            }
            EditorGUI.BeginProperty(_position, _label, _property);
            AudioPickerField.Draw(_position, _property, _label, AudioPickerField.ElementType(fieldInfo.FieldType), null);
            EditorGUI.EndProperty();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty _property) {
            SerializedProperty _copy = _property.Copy();
            var _label = new GUIContent(_property.displayName, _property.tooltip);
            return new IMGUIContainer(() => {
                _copy.serializedObject.Update();
                Rect _rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                OnGUI(_rect, _copy, _label);
                _copy.serializedObject.ApplyModifiedProperties();
            });
        }
    }
}
