using UnityEditor;
using UnityEngine;

namespace HarryTools.Conditions.Editor {
    /// <summary> Draws 'TargetGameObject' and 'TargetComponent&lt;T&gt;' on a single line: [Source][SceneRef] </summary>
    [CustomPropertyDrawer(typeof(TargetReference), true)]
    public class TargetReferenceDrawer : PropertyDrawer {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const float SourceWidth = 70f;
        #endregion

        // ==================== METHODS ====================
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            SerializedProperty _source = _property.FindPropertyRelative("source");
            SerializedProperty _custom = _property.FindPropertyRelative("custom");

            _label = EditorGUI.BeginProperty(_position, _label, _property);

            Rect _fieldRect = EditorGUI.PrefixLabel(_position, _label);

            // PrefixLabel does not reset the indent, so it is disabled for the inner fields
            int _indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            bool _isCustom = _source.enumValueIndex == (int)TargetSource.Custom;
            Rect _sourceRect = new(_fieldRect.x, _fieldRect.y, _isCustom ? SourceWidth : _fieldRect.width, _fieldRect.height);

            EditorGUI.PropertyField(_sourceRect, _source, GUIContent.none);

            if (_isCustom && _custom != null) {
                Rect _customRect = new(_sourceRect.xMax + 2, _fieldRect.y, _fieldRect.width - SourceWidth - 2, _fieldRect.height);
                EditorGUI.PropertyField(_customRect, _custom, GUIContent.none);
            }

            EditorGUI.indentLevel = _indent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label) => EditorGUIUtility.singleLineHeight;
    }
}
