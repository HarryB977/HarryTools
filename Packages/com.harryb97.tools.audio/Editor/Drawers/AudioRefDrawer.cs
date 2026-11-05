using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Audio.Editor {
    /// <summary> Searchable picker for the id-based ref structs. Writes the asset id and flags ids that no longer resolve. </summary>
    [CustomPropertyDrawer(typeof(SoundRef))]
    [CustomPropertyDrawer(typeof(MusicRef))]
    [CustomPropertyDrawer(typeof(VoiceRef))]
    [CustomPropertyDrawer(typeof(ConversationRef))]
    internal sealed class AudioRefDrawer : PropertyDrawer {
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            SerializedProperty _id = _property.FindPropertyRelative("id");
            Type _assetType = AssetTypeOf(AudioPickerField.ElementType(fieldInfo.FieldType));
            if (_id == null || _assetType == null) {
                EditorGUI.PropertyField(_position, _property, _label, true);
                return;
            }
            EditorGUI.BeginProperty(_position, _label, _property);
            AudioPickerField.Draw(_position, _property, _label, _assetType, _id);
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

        private static Type AssetTypeOf(Type _refType) {
            if (_refType == typeof(SoundRef)) return typeof(SoundAsset);
            if (_refType == typeof(MusicRef)) return typeof(MusicAsset);
            if (_refType == typeof(VoiceRef)) return typeof(VoiceAsset);
            if (_refType == typeof(ConversationRef)) return typeof(ConversationAsset);
            return null;
        }
    }
}
