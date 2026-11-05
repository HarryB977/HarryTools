using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Audio.Editor {
    /// <summary> Searchable picker for fields of an audio asset type. </summary>
    [CustomPropertyDrawer(typeof(AudioCatalogItem), true)]
    internal sealed class AudioAssetDrawer : PropertyDrawer {
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            if (_property.propertyType != SerializedPropertyType.ObjectReference) {
                EditorGUI.LabelField(_position, _label, new GUIContent(_property.propertyType.ToString()));
                return;
            }
            EditorGUI.BeginProperty(_position, _label, _property);
            AudioPickerField.Draw(_position, _property, _label, AudioPickerField.AssetTypeOfObject(fieldInfo?.FieldType, _property.type), null);
            EditorGUI.EndProperty();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty _property) => AudioPickerField.CreateGUI(_property, OnGUI);
    }
}
