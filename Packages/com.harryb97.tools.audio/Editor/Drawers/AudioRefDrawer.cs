using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Audio.Editor {
    /// <summary> Searchable picker for the id-based ref structs. Writes the asset id and flags ids that no longer resolve. The asset type comes from the serialized type name, so it works without fieldInfo (SerializeReference instances). </summary>
    [CustomPropertyDrawer(typeof(SoundRef))]
    [CustomPropertyDrawer(typeof(MusicRef))]
    [CustomPropertyDrawer(typeof(VoiceRef))]
    [CustomPropertyDrawer(typeof(ConversationRef))]
    internal sealed class AudioRefDrawer : PropertyDrawer {
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            SerializedProperty _id = _property.FindPropertyRelative("id");
            if (_id == null) return;
            Type _assetType = AudioPickerField.AssetTypeOfRef(_property.type);
            if (_assetType == null) {
                // Never PropertyField on the property itself: that re-enters this drawer.
                EditorGUI.PropertyField(_position, _id, _label);
                return;
            }
            EditorGUI.BeginProperty(_position, _label, _property);
            AudioPickerField.Draw(_position, _property, _label, _assetType, _id);
            EditorGUI.EndProperty();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty _property) => AudioPickerField.CreateGUI(_property, OnGUI);
    }
}
