using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HarryTools.ServiceLocator.Editor {
    /// <summary> Inspector de 'ServiceBinding': desplegable con los campos [ServiceBindable] y referencia filtrada por su tipo </summary>
    [CustomEditor(typeof(ServiceBinding))]
    public class ServiceBindingEditor : UnityEditor.Editor {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private SerializedProperty serviceType;
        private SerializedProperty fieldName;
        private SerializedProperty targetObject;
        #endregion

        // ==================== START ====================
        private void OnEnable() {
            serviceType = serializedObject.FindProperty("serviceType");
            fieldName = serializedObject.FindProperty("fieldName");
            targetObject = serializedObject.FindProperty("target");
        }

        // ==================== METHODS ====================
        public override void OnInspectorGUI() {
            serializedObject.Update();

            FieldInfo _field = ResolveField(serviceType.stringValue, fieldName.stringValue);
            bool _hasSelection = !string.IsNullOrEmpty(serviceType.stringValue);

            // Desplegable de campos
            Rect _rect = EditorGUILayout.GetControlRect();
            _rect = EditorGUI.PrefixLabel(_rect, new GUIContent("Service Field"));

            string _label = _field != null ? GetLabel(_field) : _hasSelection ? $"(Missing) {fieldName.stringValue}" : "None";
            if (EditorGUI.DropdownButton(_rect, new GUIContent(_label), FocusType.Keyboard)) ShowMenu(_field);

            // Referencia filtrada por el tipo del campo
            Type _objectType = _field != null ? _field.FieldType : typeof(UnityEngine.Object);
            using (new EditorGUI.DisabledScope(_field == null)) {
                targetObject.objectReferenceValue = EditorGUILayout.ObjectField("Target", targetObject.objectReferenceValue, _objectType, true);
            }

            // Avisos
            if (_hasSelection && _field == null) {
                EditorGUILayout.HelpBox($"No se encuentra '{fieldName.stringValue}' con [ServiceBindable] en '{serviceType.stringValue}'", MessageType.Error);

            } else if (_field != null && targetObject.objectReferenceValue == null) {
                EditorGUILayout.HelpBox("Falta la referencia que se asignará al servicio", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ShowMenu(FieldInfo _current) {
            GenericMenu _menu = new();
            _menu.AddItem(new GUIContent("None"), _current == null, () => Select(null));

            FieldInfo[] _fields = TypeCache.GetFieldsWithAttribute<ServiceBindableAttribute>()
                .Where(_f => !_f.IsStatic && typeof(UnityEngine.Object).IsAssignableFrom(_f.FieldType))
                .OrderBy(_f => _f.DeclaringType.Name)
                .ThenBy(_f => _f.Name)
                .ToArray();

            if (_fields.Length == 0) _menu.AddDisabledItem(new GUIContent("No hay campos con [ServiceBindable]"));

            foreach (FieldInfo _field in _fields) {
                // '/' crea un submenú por servicio
                GUIContent _content = new($"{_field.DeclaringType.Name}/{GetLabel(_field)}");
                _menu.AddItem(_content, _field == _current, () => Select(_field));
            }

            _menu.ShowAsContext();
        }

        private void Select(FieldInfo _field) {
            serializedObject.Update();

            serviceType.stringValue = _field != null ? GetTypeName(_field.DeclaringType) : string.Empty;
            fieldName.stringValue = _field != null ? _field.Name : string.Empty;
            targetObject.objectReferenceValue = _field != null ? FindTarget(_field.FieldType) : null;

            serializedObject.ApplyModifiedProperties();
        }

        // Propone el GameObject o el primer componente compatible del propio objeto
        private UnityEngine.Object FindTarget(Type _type) {
            GameObject _go = ((Component)target).gameObject;

            if (_type == typeof(GameObject)) return _go;
            if (typeof(Component).IsAssignableFrom(_type)) return _go.GetComponent(_type);

            return null;
        }

        private static FieldInfo ResolveField(string _typeName, string _fieldName) {
            if (string.IsNullOrEmpty(_typeName) || string.IsNullOrEmpty(_fieldName)) return null;

            Type _type = Type.GetType(_typeName);
            FieldInfo _field = _type?.GetField(_fieldName, FieldFlags);

            return _field != null && _field.IsDefined(typeof(ServiceBindableAttribute)) ? _field : null;
        }

        private static string GetLabel(FieldInfo _field) => $"{_field.Name} ({_field.FieldType.Name})";

        // Nombre con ensamblado pero sin versión, para que 'Type.GetType' lo resuelva sin depender de ella
        private static string GetTypeName(Type _type) => $"{_type.FullName}, {_type.Assembly.GetName().Name}";
    }
}
