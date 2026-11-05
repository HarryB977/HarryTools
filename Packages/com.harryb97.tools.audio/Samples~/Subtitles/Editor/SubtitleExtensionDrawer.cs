using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Audio.Subtitles.Editor {
    /// <summary> Draws the extension fields (the TypeSelector provides the header and foldout) and warns about override clips the asset does not play. </summary>
    [CustomPropertyDrawer(typeof(SubtitleExtension))]
    internal sealed class SubtitleExtensionDrawer : PropertyDrawer {
        // ==================== VARIABLES ===================
        private static readonly List<AudioClip> missing = new List<AudioClip>();

        // ==================== OVERRIDES ===================
        public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label) {
            float _height = 0f;
            SerializedProperty _child = _property.Copy();
            SerializedProperty _end = _property.GetEndProperty();
            bool _enter = true;
            while (_child.NextVisible(_enter) && !SerializedProperty.EqualContents(_child, _end)) {
                _enter = false;
                _height += EditorGUI.GetPropertyHeight(_child, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            CollectMissing(_property);
            _height += missing.Count * (HelpHeight + EditorGUIUtility.standardVerticalSpacing);
            return _height;
        }

        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            float _y = _position.y;
            EditorGUI.indentLevel++;
            SerializedProperty _child = _property.Copy();
            SerializedProperty _end = _property.GetEndProperty();
            bool _enter = true;
            while (_child.NextVisible(_enter) && !SerializedProperty.EqualContents(_child, _end)) {
                _enter = false;
                float _h = EditorGUI.GetPropertyHeight(_child, true);
                EditorGUI.PropertyField(new Rect(_position.x, _y, _position.width, _h), _child, true);
                _y += _h + EditorGUIUtility.standardVerticalSpacing;
            }
            CollectMissing(_property);
            for (int _i = 0; _i < missing.Count; _i++) {
                string _name = missing[_i] != null ? missing[_i].name : "None";
                EditorGUI.HelpBox(new Rect(_position.x, _y, _position.width, HelpHeight), "Override clip '" + _name + "' is not one of the clips of this asset, so it will never play.", MessageType.Warning);
                _y += HelpHeight + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.indentLevel--;
        }

        // ==================== PRIVATE ===================
        private static float HelpHeight => EditorGUIUtility.singleLineHeight * 2f;

        private static void CollectMissing(SerializedProperty _property) {
            missing.Clear();
            SerializedObject _so = _property.serializedObject;
            if (_so == null || _so.targetObject == null) return;
            if (_property.managedReferenceValue is SubtitleExtension _extension && _so.targetObject is AudioAsset _owner) {
                _extension.FindMissingClips(_owner, missing);
            }
        }
    }
}
