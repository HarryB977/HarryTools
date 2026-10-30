using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

namespace HarryTools.SerializeReferences.Editor {
    /// <summary>
    /// Reúne todos los componentes de las escenas abiertas (y del prefab en edición) que contienen 'SerializeReference',
    /// permitiendo editarlos, filtrarlos y corregir duplicados o tipos perdidos desde un solo lugar.
    /// </summary>
    public class SerializeReferenceExplorerWindow : EditorWindow {
        private sealed class ComponentEntry {
            public MonoBehaviour Component;
            public SerializedObject SerializedObject;
            public readonly List<string> RootPaths = new();
            public readonly HashSet<string> TypeNames = new();
            public int ReferenceCount;
            public int DuplicateCount;
            public bool HasMissingTypes;
            public string HierarchyPath;
        }

        private sealed class SceneEntry {
            public string Name;
            public readonly List<ComponentEntry> Components = new();
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        private const string AllTypesOption = "Todos los tipos";

        private readonly List<SceneEntry> scenes = new();
        private readonly HashSet<string> expandedKeys = new();
        private string[] typeOptions = { AllTypesOption };

        private SearchField searchField;
        private Vector2 scroll;
        private string search = string.Empty;
        private int typeFilterIndex;
        private bool onlyProblems;
        private bool needsRefresh = true;
        #endregion

        // ==================== START ====================
        [MenuItem("Tools/HarryTools/SerializeReference Explorer")]
        public static void Open() {
            SerializeReferenceExplorerWindow _window = GetWindow<SerializeReferenceExplorerWindow>();
            _window.titleContent = new GUIContent("SerializeReference Explorer", EditorGUIUtility.IconContent("d_UnityEditor.HierarchyWindow").image);
            _window.Show();
        }

        private void OnEnable() {
            searchField = new SearchField();

            EditorApplication.hierarchyChanged += MarkDirty;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
            Undo.undoRedoPerformed += MarkDirty;
        }

        private void OnDisable() {
            EditorApplication.hierarchyChanged -= MarkDirty;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
            Undo.undoRedoPerformed -= MarkDirty;

            DisposeEntries();
        }

        private void OnSceneOpened(Scene _scene, OpenSceneMode _mode) => MarkDirty();
        private void OnSceneClosed(Scene _scene) => MarkDirty();
        private void OnPrefabStageChanged(PrefabStage _stage) => MarkDirty();

        private void MarkDirty() {
            needsRefresh = true;
            Repaint();
        }

        // ==================== METHODS ====================
        #region Scan
        private void Refresh() {
            needsRefresh = false;
            DisposeEntries();

            PrefabStage _prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (_prefabStage != null) {
                AddScene($"Prefab: {_prefabStage.prefabContentsRoot.name}", new[] { _prefabStage.prefabContentsRoot });
            } else {
                for (int i = 0; i < SceneManager.sceneCount; i++) {
                    Scene _scene = SceneManager.GetSceneAt(i);
                    if (_scene.isLoaded) AddScene(string.IsNullOrEmpty(_scene.name) ? "Untitled" : _scene.name, _scene.GetRootGameObjects());
                }
            }

            typeOptions = new[] { AllTypesOption }
                .Concat(scenes.SelectMany(_scene => _scene.Components).SelectMany(_entry => _entry.TypeNames).Distinct().OrderBy(_name => _name))
                .ToArray();

            typeFilterIndex = Mathf.Clamp(typeFilterIndex, 0, typeOptions.Length - 1);
        }

        private void AddScene(string _name, IEnumerable<GameObject> _roots) {
            SceneEntry _sceneEntry = new() { Name = _name };

            foreach (GameObject _root in _roots) {
                foreach (MonoBehaviour _component in _root.GetComponentsInChildren<MonoBehaviour>(true)) {
                    if (_component == null || !ManagedReferenceTools.HasManagedReferences(_component)) continue;

                    ComponentEntry _entry = new() {
                        Component = _component,
                        SerializedObject = new SerializedObject(_component),
                        HierarchyPath = GetHierarchyPath(_component.transform)
                    };

                    Analyze(_entry);
                    _sceneEntry.Components.Add(_entry);
                }
            }

            if (_sceneEntry.Components.Count > 0) scenes.Add(_sceneEntry);
        }

        /// <summary> Busca los campos raíz que contienen referencias y recopila tipos, duplicados y tipos perdidos </summary>
        private static void Analyze(ComponentEntry _entry) {
            _entry.RootPaths.Clear();
            _entry.TypeNames.Clear();
            _entry.ReferenceCount = 0;

            SerializedObject _serializedObject = _entry.SerializedObject;
            _serializedObject.Update();

            SerializedProperty _iterator = _serializedObject.GetIterator();

            if (_iterator.NextVisible(true)) {
                do {
                    if (ContainsManagedReference(_iterator)) _entry.RootPaths.Add(_iterator.propertyPath);
                } while (_iterator.NextVisible(false));
            }

            foreach (SerializedProperty _property in ManagedReferenceTools.EnumerateManagedReferences(_serializedObject)) {
                Type _type = TypeUtility.GetManagedReferenceType(_property.managedReferenceFullTypename);
                if (_type == null) continue;

                _entry.ReferenceCount++;
                _entry.TypeNames.Add(TypeMenuUtility.GetDisplayName(_type));
            }

            _entry.DuplicateCount = DuplicateReferenceFixer.FindDuplicates(_serializedObject).Sum(_group => _group.Paths.Count - 1);
            _entry.HasMissingTypes = SerializationUtility.HasManagedReferencesWithMissingTypes(_entry.Component);
        }

        private static bool ContainsManagedReference(SerializedProperty _property) {
            if (_property.propertyType == SerializedPropertyType.ManagedReference) return true;
            if (_property.propertyType != SerializedPropertyType.Generic) return false;

            SerializedProperty _iterator = _property.Copy();
            SerializedProperty _end = _property.GetEndProperty();
            bool _enterChildren = true;

            while (_iterator.Next(_enterChildren) && !SerializedProperty.EqualContents(_iterator, _end)) {
                if (_iterator.propertyType == SerializedPropertyType.ManagedReference) return true;
                _enterChildren = _iterator.propertyType == SerializedPropertyType.Generic;
            }

            return false;
        }

        private void DisposeEntries() {
            foreach (ComponentEntry _entry in scenes.SelectMany(_scene => _scene.Components)) _entry.SerializedObject?.Dispose();
            scenes.Clear();
        }

        private static string GetHierarchyPath(Transform _transform) {
            string _path = _transform.name;

            for (Transform _parent = _transform.parent; _parent != null; _parent = _parent.parent) {
                _path = $"{_parent.name}/{_path}";
            }

            return _path;
        }
        #endregion

        // =================================================

        #region GUI
        private void OnGUI() {
            if (needsRefresh) Refresh();

            DrawToolbar();

            if (scenes.Count == 0) {
                EditorGUILayout.HelpBox("No hay componentes con [SerializeReference] en las escenas abiertas.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (SceneEntry _scene in scenes) {
                List<ComponentEntry> _visible = _scene.Components.Where(MatchesFilters).ToList();
                if (_visible.Count == 0) continue;

                string _sceneKey = $"scene:{_scene.Name}";
                bool _sceneExpanded = DrawFoldoutHeader(_sceneKey, $"{_scene.Name}  ({_visible.Count})", true, EditorStyles.foldoutHeader);

                if (!_sceneExpanded) continue;

                foreach (ComponentEntry _entry in _visible) DrawComponent(_entry);

                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                if (GUILayout.Button("Refrescar", EditorStyles.toolbarButton, GUILayout.Width(70))) Refresh();

                typeFilterIndex = EditorGUILayout.Popup(typeFilterIndex, typeOptions, EditorStyles.toolbarPopup, GUILayout.Width(170));
                onlyProblems = GUILayout.Toggle(onlyProblems, new GUIContent("Solo problemas", "Muestra solo componentes con referencias duplicadas o tipos perdidos"), EditorStyles.toolbarButton, GUILayout.Width(100));

                GUILayout.FlexibleSpace();
                search = searchField.OnToolbarGUI(search, GUILayout.MinWidth(120), GUILayout.MaxWidth(300));
            }
        }

        private void DrawComponent(ComponentEntry _entry) {
            if (_entry.Component == null) {
                MarkDirty();
                return;
            }

            string _key = $"component:{_entry.Component.GetInstanceID()}";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                bool _expanded;

                using (new EditorGUILayout.HorizontalScope()) {
                    string _title = $"{_entry.HierarchyPath}  ›  {_entry.Component.GetType().Name}  ({_entry.ReferenceCount})";
                    _expanded = DrawFoldoutHeader(_key, _title, false, EditorStyles.foldout);

                    if (_entry.DuplicateCount > 0) {
                        GUIContent _duplicateLabel = new($"Duplicados: {_entry.DuplicateCount}", "Referencias que comparten instancia. Editar una modifica las demás.");
                        if (GUILayout.Button(_duplicateLabel, EditorStyles.miniButton, GUILayout.Width(100))) FixDuplicates(_entry);
                    }

                    if (_entry.HasMissingTypes) {
                        if (GUILayout.Button(new GUIContent("Tipos perdidos", "Elimina las referencias a clases que ya no existen"), EditorStyles.miniButton, GUILayout.Width(100))) ClearMissingTypes(_entry);
                    }

                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_scenepicking_pickable_hover", "|Seleccionar"), EditorStyles.miniButton, GUILayout.Width(26))) {
                        Selection.activeObject = _entry.Component.gameObject;
                        EditorGUIUtility.PingObject(_entry.Component.gameObject);
                    }
                }

                if (!_expanded) return;

                SerializedObject _serializedObject = _entry.SerializedObject;
                _serializedObject.Update();

                EditorGUI.BeginChangeCheck();

                using (new EditorGUI.IndentLevelScope()) {
                    foreach (string _path in _entry.RootPaths) {
                        SerializedProperty _property = _serializedObject.FindProperty(_path);
                        if (_property != null) EditorGUILayout.PropertyField(_property, true);
                    }
                }

                if (EditorGUI.EndChangeCheck()) {
                    _serializedObject.ApplyModifiedProperties();
                    Analyze(_entry);
                }
            }
        }

        private bool DrawFoldoutHeader(string _key, string _title, bool _defaultExpanded, GUIStyle _style) {
            bool _expanded = expandedKeys.Contains(_key) != _defaultExpanded;
            bool _newExpanded = EditorGUILayout.Foldout(_expanded, _title, true, _style);

            if (_newExpanded != _expanded) {
                if (expandedKeys.Contains(_key)) expandedKeys.Remove(_key);
                else expandedKeys.Add(_key);
            }

            return _newExpanded;
        }

        private bool MatchesFilters(ComponentEntry _entry) {
            if (_entry.Component == null) return false;
            if (onlyProblems && _entry.DuplicateCount == 0 && !_entry.HasMissingTypes) return false;
            if (typeFilterIndex > 0 && !_entry.TypeNames.Contains(typeOptions[typeFilterIndex])) return false;

            if (string.IsNullOrWhiteSpace(search)) return true;

            return _entry.HierarchyPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   _entry.Component.GetType().Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   _entry.TypeNames.Any(_name => _name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        #endregion

        // =================================================

        #region Actions
        private static void FixDuplicates(ComponentEntry _entry) {
            int _fixed = DuplicateReferenceFixer.FixDuplicates(_entry.Component, false, true);
            Undo.SetCurrentGroupName("Fix Duplicated References");

            Debug.Log($"[SerializeReferences] {_fixed} referencias duplicadas corregidas en '{_entry.HierarchyPath}' ({_entry.Component.GetType().Name})", _entry.Component);
            Analyze(_entry);
        }

        private static void ClearMissingTypes(ComponentEntry _entry) {
            if (!EditorUtility.DisplayDialog("Tipos perdidos",
                    $"Se eliminarán las referencias a tipos que ya no existen en '{_entry.HierarchyPath}' ({_entry.Component.GetType().Name}).\n\n¿Continuar?",
                    "Eliminar", "Cancelar")) return;

            Undo.RegisterCompleteObjectUndo(_entry.Component, "Clear Missing Types");
            SerializationUtility.ClearAllManagedReferencesWithMissingTypes(_entry.Component);
            EditorUtility.SetDirty(_entry.Component);

            Analyze(_entry);
        }
        #endregion
    }
}
