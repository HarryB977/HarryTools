using System;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace HarryTools.SerializeReferences.Editor {
    /// <summary> Opciones del menú contextual (click derecho) de los campos con [TypeSelector] </summary>
    public static class ManagedReferenceContextMenu {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string MenuRoot = "Type Selector/";
        private const string ClipboardTypeKey = "HarryTools.SerializeReferences.ClipboardType";
        private const string ClipboardJsonKey = "HarryTools.SerializeReferences.ClipboardJson";
        #endregion

        // ==================== START ====================
        [InitializeOnLoadMethod]
        private static void Initialize() {
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
        }

        // ==================== METHODS ====================
        private static void OnContextualPropertyMenu(GenericMenu _menu, SerializedProperty _property) {
            if (_property.propertyType != SerializedPropertyType.ManagedReference) return;
            if (!_property.HasTypeSelector()) return;

            // La propiedad recibida se reutiliza internamente, hay que guardar los datos ahora
            Object[] _targets = _property.serializedObject.targetObjects;
            string _path = _property.propertyPath;

            object _value = _property.hasMultipleDifferentValues ? null : _property.managedReferenceValue;
            Type _fieldType = TypeUtility.GetManagedReferenceType(_property.managedReferenceFieldTypename);
            Type _clipboardType = GetClipboardType();

            bool _hasValue = _value != null;
            bool _canPasteReference = _clipboardType != null && _fieldType != null && TypeUtility.IsCompatible(_fieldType, _clipboardType);

            // Copiar / pegar
            AddItem(_menu, "Copiar", _hasValue, () => Copy(_value));
            AddItem(_menu, _clipboardType != null ? $"Pegar ({TypeMenuUtility.GetDisplayName(_clipboardType)})" : "Pegar", _canPasteReference,
                () => Modify(_targets, _path, "Paste Reference", _ => CreateFromClipboard()));
            AddItem(_menu, "Pegar solo valores", _hasValue && _clipboardType != null,
                () => Modify(_targets, _path, "Paste Values", _current => {
                    object _clone = ManagedReferenceTools.Clone(_current);
                    EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(ClipboardJsonKey, "{}"), _clone);
                    return _clone;
                }));

            _menu.AddSeparator(MenuRoot);

            // Instancias
            AddItem(_menu, "Hacer instancia independiente", _hasValue, () => Modify(_targets, _path, "Make Unique Instance", ManagedReferenceTools.Clone));
            AddItem(_menu, "Resetear valores", _hasValue, () => Modify(_targets, _path, "Reset Instance", _current => ManagedReferenceTools.CreateInstance(_current.GetType())));
            AddItem(_menu, "Vaciar (null)", _hasValue, () => Modify(_targets, _path, "Clear Reference", _ => null));
        }

        // =================================================

        private static void AddItem(GenericMenu _menu, string _name, bool _enabled, Action _action) {
            GUIContent _content = new(MenuRoot + _name);

            if (_enabled) _menu.AddItem(_content, false, () => _action());
            else _menu.AddDisabledItem(_content);
        }

        private static void Modify(Object[] _targets, string _path, string _undoName, Func<object, object> _modifier) {
            foreach (Object _target in _targets) {
                if (_target == null) continue;

                using SerializedObject _serializedObject = new(_target);
                SerializedProperty _property = _serializedObject.FindProperty(_path);
                if (_property == null) continue;

                _property.managedReferenceValue = _modifier(_property.managedReferenceValue);
                _serializedObject.ApplyModifiedProperties();
            }

            Undo.SetCurrentGroupName(_undoName);
        }

        // =================================================

        private static void Copy(object _value) {
            SessionState.SetString(ClipboardTypeKey, _value.GetType().AssemblyQualifiedName);
            SessionState.SetString(ClipboardJsonKey, EditorJsonUtility.ToJson(_value));
        }

        private static Type GetClipboardType() {
            string _typeName = SessionState.GetString(ClipboardTypeKey, string.Empty);
            return string.IsNullOrEmpty(_typeName) ? null : Type.GetType(_typeName, false);
        }

        private static object CreateFromClipboard() {
            object _instance = ManagedReferenceTools.CreateInstance(GetClipboardType());
            if (_instance != null) EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(ClipboardJsonKey, "{}"), _instance);

            return _instance;
        }
    }
}
