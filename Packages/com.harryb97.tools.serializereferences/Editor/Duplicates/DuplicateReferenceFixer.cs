using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Object = UnityEngine.Object;

namespace HarryTools.SerializeReferences.Editor {
    /// <summary>
    /// Corrige el fallo de Unity por el que, al añadir un elemento a un array, el nuevo elemento copia la referencia
    /// del anterior en lugar de su valor, haciendo que ambos compartan instancia (y que editar uno cambie el otro).
    /// Se ejecuta al modificar un objeto y al dibujar un campo con [TypeSelector].
    /// </summary>
    [InitializeOnLoad]
    public static class DuplicateReferenceFixer {
        public readonly struct DuplicateGroup {
            public long Id { get; }
            public IReadOnlyList<string> Paths { get; }

            public DuplicateGroup(long _id, IReadOnlyList<string> _paths) {
                Id = _id;
                Paths = _paths;
            }
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        private const int MaxPasses = 8;

        private static readonly Dictionary<Object, int> CheckedDirtyCounts = new();
        private static readonly HashSet<Object> PendingTargets = new();
        private static bool isScheduled;
        #endregion

        // ==================== START ====================
        static DuplicateReferenceFixer() {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        // ==================== METHODS ====================
        #region Scheduling
        /// <summary> Programa una comprobación del objeto para el siguiente ciclo del editor (solo si ha cambiado desde la última) </summary>
        public static void Schedule(Object _target) {
            if (_target == null || !SerializeReferencesSettings.AutoFixDuplicates) return;

            int _dirtyCount = EditorUtility.GetDirtyCount(_target);
            if (CheckedDirtyCounts.TryGetValue(_target, out int _lastDirtyCount) && _lastDirtyCount == _dirtyCount) return;

            CheckedDirtyCounts[_target] = _dirtyCount;
            PendingTargets.Add(_target);

            if (isScheduled) return;

            isScheduled = true;
            EditorApplication.delayCall += ProcessPending;
        }

        private static void ProcessPending() {
            isScheduled = false;

            Object[] _targets = PendingTargets.ToArray();
            PendingTargets.Clear();

            bool _anyFixed = false;

            foreach (Object _target in _targets) {
                if (_target == null) continue;

                _anyFixed |= FixDuplicates(_target, true) > 0;
                CheckedDirtyCounts[_target] = EditorUtility.GetDirtyCount(_target);
            }

            // Limpieza de objetos destruidos
            if (CheckedDirtyCounts.Count > 512) {
                foreach (Object _key in CheckedDirtyCounts.Keys.Where(_key => _key == null).ToArray()) CheckedDirtyCounts.Remove(_key);
            }

            if (_anyFixed) UnityEditorInternalRepaint();
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream _stream) {
            if (!SerializeReferencesSettings.AutoFixDuplicates) return;

            for (int i = 0; i < _stream.length; i++) {
                switch (_stream.GetEventType(i)) {
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        _stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out ChangeGameObjectOrComponentPropertiesEventArgs _componentArgs);
                        ScheduleObject(EditorUtility.InstanceIDToObject(_componentArgs.instanceId));
                        break;

                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        _stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs _assetArgs);
                        ScheduleObject(EditorUtility.InstanceIDToObject(_assetArgs.instanceId));
                        break;
                }
            }
        }

        private static void ScheduleObject(Object _object) {
            if (_object == null) return;

            if (_object is GameObject _gameObject) {
                foreach (MonoBehaviour _behaviour in _gameObject.GetComponents<MonoBehaviour>()) {
                    if (_behaviour != null) Schedule(_behaviour);
                }
            } else {
                Schedule(_object);
            }
        }

        private static void UnityEditorInternalRepaint() {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        #endregion

        // =================================================

        #region Detection
        /// <summary> Devuelve los grupos de propiedades que apuntan a la misma instancia </summary>
        public static List<DuplicateGroup> FindDuplicates(SerializedObject _serializedObject) {
            Dictionary<long, List<string>> _pathsById = new();

            foreach (SerializedProperty _property in ManagedReferenceTools.EnumerateManagedReferences(_serializedObject)) {
                if (!_property.HasValidId()) continue;

                long _id = _property.managedReferenceId;

                if (!_pathsById.TryGetValue(_id, out List<string> _paths)) {
                    _paths = new List<string>();
                    _pathsById.Add(_id, _paths);
                }

                _paths.Add(_property.propertyPath);
            }

            return _pathsById
                .Where(_pair => _pair.Value.Count > 1)
                .Select(_pair => new DuplicateGroup(_pair.Key, _pair.Value))
                .ToList();
        }

        public static int CountDuplicates(Object _target) {
            if (_target == null) return 0;

            using SerializedObject _serializedObject = new(_target);
            return FindDuplicates(_serializedObject).Sum(_group => _group.Paths.Count - 1);
        }
        #endregion

        // =================================================

        #region Fix
        /// <summary>
        /// Sustituye cada referencia compartida por una copia independiente (la primera aparición conserva la instancia original).
        /// </summary>
        /// <param name="_onlyTypeSelectorFields"> Si es verdadero, solo se corrigen los campos con [TypeSelector] </param>
        /// <param name="_recordUndo"> Si es verdadero, la corrección se puede deshacer como una operación propia </param>
        /// <returns> Número de referencias corregidas </returns>
        public static int FixDuplicates(Object _target, bool _onlyTypeSelectorFields, bool _recordUndo = false) {
            if (_target == null) return 0;

            int _total = 0;

            // Varias pasadas: al clonar una referencia se clonan también sus hijas, pero hay que volver a leer el objeto
            for (int _pass = 0; _pass < MaxPasses; _pass++) {
                using SerializedObject _serializedObject = new(_target);
                List<DuplicateGroup> _groups = FindDuplicates(_serializedObject);
                if (_groups.Count == 0) break;

                List<string> _pathsToFix = new();

                foreach (DuplicateGroup _group in _groups) {
                    if (_onlyTypeSelectorFields && !_group.Paths.Any(_path => _serializedObject.FindProperty(_path)?.HasTypeSelector() ?? false)) continue;

                    _pathsToFix.AddRange(_group.Paths.Skip(1));
                }

                // Si una ruta está dentro de otra que también se va a clonar, se corregirá sola
                _pathsToFix = _pathsToFix
                    .Where(_path => !_pathsToFix.Any(_other => _other != _path && _path.StartsWith(_other + ".")))
                    .ToList();

                if (_pathsToFix.Count == 0) break;

                foreach (string _path in _pathsToFix) {
                    SerializedProperty _property = _serializedObject.FindProperty(_path);
                    if (_property == null) continue;

                    _property.managedReferenceValue = ManagedReferenceTools.Clone(_property.managedReferenceValue);
                    _total++;

                    if (SerializeReferencesSettings.LogFixes) {
                        Debug.Log($"[SerializeReferences] Referencia compartida convertida en instancia independiente: '{_target.name}' ({_target.GetType().Name}) > {_path}", _target);
                    }
                }

                if (_recordUndo) {
                    _serializedObject.ApplyModifiedProperties();
                } else {
                    // Sin Undo propio: al deshacer la acción que generó el duplicado, se deshace todo junto
                    _serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(_target);

                    if (_target is Component _component && _component.gameObject.scene.IsValid()) {
                        EditorSceneManager.MarkSceneDirty(_component.gameObject.scene);
                    }
                }
            }

            if (_total > 0 && PrefabUtility.IsPartOfPrefabInstance(_target)) {
                PrefabUtility.RecordPrefabInstancePropertyModifications(_target);
            }

            return _total;
        }
        #endregion
    }
}
