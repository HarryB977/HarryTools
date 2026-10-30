using UnityEditor;
using UnityEngine;

namespace HarryTools.MultiScene.Editor {
    [CustomPropertyDrawer(typeof(SceneReference))]
    internal class SceneReferenceDrawer : PropertyDrawer {
        private const float StateWidth = 80f;
        private const float Spacing = 2f;

        // ==================== METHODS ====================
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            EditorGUI.BeginProperty(_position, _label, _property);

            // Se sincroniza al dibujar, así cualquier referencia visible está siempre al día
            SceneReferenceUtility.Sync(_property);

            SerializedProperty _guid = _property.FindPropertyRelative(SceneReferenceUtility.GuidField);
            SceneAsset _current = string.IsNullOrEmpty(_guid.stringValue) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(_guid.stringValue));

            Rect _fieldRect = new(_position.x, _position.y, _position.width - StateWidth - Spacing, EditorGUIUtility.singleLineHeight);
            Rect _stateRect = new(_fieldRect.xMax + Spacing, _position.y, StateWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            SceneAsset _selected = (SceneAsset)EditorGUI.ObjectField(_fieldRect, _label, _current, typeof(SceneAsset), false);

            if (EditorGUI.EndChangeCheck()) {
                _guid.stringValue = _selected == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_selected));
                SceneReferenceUtility.Sync(_property);
            }

            DrawState(_stateRect, _property);
            EditorGUI.EndProperty();
        }

        // =================================================

        private static void DrawState(Rect _rect, SerializedProperty _property) {
            if (_property.serializedObject.isEditingMultipleObjects || _property.boxedValue is not SceneReference _reference) return;

            GUIContent _content = _reference.State switch {
                SceneReferenceState.Regular => new GUIContent("Build", "La escena está en la lista de escenas de la build"),
                SceneReferenceState.Addressable => new GUIContent("Addressable", $"Dirección: {_reference.Address}"),
                SceneReferenceState.Unsafe => new GUIContent("Not loadable", EditorGUIUtility.IconContent("console.warnicon.sml").image, "La escena no está en la build ni es un Addressable"),
                _ => GUIContent.none
            };

            EditorGUI.LabelField(_rect, _content, EditorStyles.miniLabel);
        }
    }
}
