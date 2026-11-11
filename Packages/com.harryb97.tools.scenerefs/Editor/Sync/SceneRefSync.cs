using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Keeps the guid, component and label of every SceneRef in line with the object it points to.
    /// Runs on each scene right before it is saved, so removed components, Ctrl+D copies and cross scene references are fixed without detecting each case.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneRefSync
    {
        // ==================== VARIABLES ====================

        private const string GuidProperty = "guid";
        private const string IdProperty = "id";

        private static readonly List<GameObject> rootBuffer = new List<GameObject>();
        private static readonly List<MonoBehaviour> behaviourBuffer = new List<MonoBehaviour>();
        private static readonly List<Component> componentBuffer = new List<Component>();
        private static readonly List<SceneId> idBuffer = new List<SceneId>();

        // ==================== SETUP ====================

        static SceneRefSync()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// With a live direct reference: makes sure the target has a SceneId and writes guid, component type, component index and label.
        /// A direct reference to another scene than the owner's is cleared afterwards and the guid is kept.
        /// References to prefab objects (assets or Prefab Mode) and between objects of one prefab instance stay direct only.
        /// </summary>
        public static void SyncProperty(SerializedProperty _ref) => SyncProperty(_ref, (HashSet<string>)null);

        /// <summary>
        /// Like <see cref="SyncProperty(SerializedProperty)"/> but for a given target instead of the stored direct reference.
        /// Needed when the owner is an asset: Unity silently refuses a scene object in its direct field, so reading it back gives null.
        /// The direct field is set only when owner and target share a scene.
        /// </summary>
        public static void SyncProperty(SerializedProperty _ref, Object _target) => SyncProperty(_ref, _target, null);

        /// <summary>Syncs every SceneRef of every script in the scene.</summary>
        public static void SyncScene(Scene _scene)
        {
            if (!_scene.IsValid() || !_scene.isLoaded) return;

            HashSet<string> _taken = null;
            foreach (MonoBehaviour _behaviour in Behaviours(_scene))
            {
                SerializedObject _so = new SerializedObject(_behaviour);
                foreach (SerializedProperty _ref in SceneRefPropertyWalker.FindAll(_so))
                {
                    // Taken ids are only gathered once a reference needs a new SceneId.
                    if (_taken == null && NeedsId(_ref)) _taken = TakenIds();
                    SyncProperty(_ref, _taken);
                }
            }
        }

        /// <summary>
        /// The SceneId of the object, adding one with a new guid and a free id based on its name when missing.
        /// Null for prefab assets, Prefab Mode and other preview scenes.
        /// </summary>
        public static SceneId EnsureId(GameObject _go) => EnsureId(_go, null);

        // ==================== INTERNAL ====================

        /// <summary>Syncs against the stored direct reference.</summary>
        internal static void SyncProperty(SerializedProperty _ref, HashSet<string> _taken)
        {
            if (_ref == null) return;
            SyncProperty(_ref, _ref.FindPropertyRelative(SceneRefPropertyWalker.DirectField).objectReferenceValue, _taken);
        }

        internal static void SyncProperty(SerializedProperty _ref, Object _target, HashSet<string> _taken)
        {
            if (_ref == null || _target == null) return;

            SerializedProperty _direct = _ref.FindPropertyRelative(SceneRefPropertyWalker.DirectField);
            GameObject _go = GameObjectOf(_target);
            if (_go == null || !IsSceneObject(_go)) return;

            bool _changed = false;
            if (InSamePrefabInstance(_ref.serializedObject.targetObject, _go))
            {
                // Internal reference of a prefab instance: direct only, like inside the prefab asset, so instances get no SceneId or overrides.
                if (_direct.objectReferenceValue != _target)
                {
                    _direct.objectReferenceValue = _target;
                    _changed = true;
                }

                _changed |= SetString(_ref, SceneRefPropertyWalker.GuidField, string.Empty);
                _changed |= SetString(_ref, SceneRefPropertyWalker.ComponentTypeField, string.Empty);
                _changed |= SetInt(_ref, SceneRefPropertyWalker.ComponentIndexField, 0);
                _changed |= SetString(_ref, SceneRefPropertyWalker.LabelField, string.Empty);
                if (_changed) _ref.serializedObject.ApplyModifiedProperties();
                return;
            }

            SceneId _id = EnsureId(_go, _taken);
            if (_id != null && !string.IsNullOrEmpty(_id.Guid))
            {
                _changed |= SetString(_ref, SceneRefPropertyWalker.GuidField, _id.Guid);
                _changed |= SetString(_ref, SceneRefPropertyWalker.LabelField, $"{_id.Id} in {_go.scene.name}");

                Component _component = _target as Component;
                _changed |= SetString(_ref, SceneRefPropertyWalker.ComponentTypeField, _component != null ? TypeName(_component.GetType()) : string.Empty);
                _changed |= SetInt(_ref, SceneRefPropertyWalker.ComponentIndexField, _component != null ? IndexAmongSameType(_component) : 0);
            }

            // Built from the target, never read back: an asset owner would have dropped it already.
            Object _keep = InSameScene(_ref.serializedObject.targetObject, _go) ? _target : null;
            if (_direct.objectReferenceValue != _keep)
            {
                _direct.objectReferenceValue = _keep;
                _changed = true;
            }

            if (_changed) _ref.serializedObject.ApplyModifiedProperties();
        }

        internal static SceneId EnsureId(GameObject _go, HashSet<string> _taken)
        {
            if (_go == null || !IsSceneObject(_go)) return null;

            SceneId _id = _go.GetComponent<SceneId>();
            if (_id != null && !string.IsNullOrEmpty(_id.Guid)) return _id;

            _taken ??= TakenIds();
            string _newId = SceneIdNaming.MakeUnique(SceneIdNaming.Sanitize(_go.name), _taken.Contains);
            if (_id == null) _id = Undo.AddComponent<SceneId>(_go);
            WriteId(_id, SceneIdNaming.NewGuid(), _newId);
            _taken.Add(_newId);
            return _id;
        }

        /// <summary>
        /// Every id an automatic id must avoid: the index (closed scenes and stale entries of open ones) plus the live SceneIds of the loaded scenes.
        /// </summary>
        internal static HashSet<string> TakenIds()
        {
            HashSet<string> _taken = new HashSet<string>(SceneIdNaming.Comparer);
            SceneIdIndex _index = SceneIdIndex.Existing;
            if (_index != null)
                foreach (SceneIdIndexEntry _entry in _index.Entries)
                    if (!string.IsNullOrEmpty(_entry.Id)) _taken.Add(_entry.Id);

            foreach (SceneId _id in LoadedSceneIds())
                if (!string.IsNullOrEmpty(_id.Id)) _taken.Add(_id.Id);
            return _taken;
        }

        /// <summary>Every SceneId of the loaded, non preview scenes.</summary>
        internal static List<SceneId> LoadedSceneIds()
        {
            List<SceneId> _result = new List<SceneId>();
            for (int _i = 0; _i < SceneManager.sceneCount; _i++)
            {
                Scene _scene = SceneManager.GetSceneAt(_i);
                if (!_scene.isLoaded || EditorSceneManager.IsPreviewScene(_scene)) continue;

                _scene.GetRootGameObjects(rootBuffer);
                for (int _r = 0; _r < rootBuffer.Count; _r++)
                {
                    rootBuffer[_r].GetComponentsInChildren(true, idBuffer);
                    _result.AddRange(idBuffer);
                }
            }

            rootBuffer.Clear();
            idBuffer.Clear();
            return _result;
        }

        /// <summary>Every script of the scene that can hold a SceneRef, inactive objects included.</summary>
        internal static List<MonoBehaviour> Behaviours(Scene _scene)
        {
            List<MonoBehaviour> _result = new List<MonoBehaviour>();
            _scene.GetRootGameObjects(rootBuffer);
            for (int _r = 0; _r < rootBuffer.Count; _r++) AddBehaviours(rootBuffer[_r], _result);
            rootBuffer.Clear();
            return _result;
        }

        /// <summary>
        /// Every script under the object that can hold a SceneRef, inactive ones included. Scripts whose type can never hold one,
        /// SceneId and missing scripts are skipped, so no SerializedObject is built for them.
        /// </summary>
        internal static void AddBehaviours(GameObject _root, List<MonoBehaviour> _result)
        {
            _root.GetComponentsInChildren(true, behaviourBuffer);
            for (int _i = 0; _i < behaviourBuffer.Count; _i++)
            {
                MonoBehaviour _behaviour = behaviourBuffer[_i];
                if (_behaviour != null && !(_behaviour is SceneId) && SceneRefTypeCache.CanHold(_behaviour.GetType())) _result.Add(_behaviour);
            }
            behaviourBuffer.Clear();
        }

        /// <summary>Writes guid and id through SerializedObject, with Undo and prefab override support.</summary>
        internal static void WriteId(SceneId _id, string _guid, string _visibleId)
        {
            SerializedObject _so = new SerializedObject(_id);
            _so.FindProperty(GuidProperty).stringValue = _guid;
            _so.FindProperty(IdProperty).stringValue = _visibleId;
            _so.ApplyModifiedProperties();
        }

        /// <summary>"Full.Name, AssemblyName", the format the runtime resolves.</summary>
        internal static string TypeName(Type _type) => $"{_type.FullName}, {_type.Assembly.GetName().Name}";

        /// <summary>True for objects of a regular scene; false for assets, Prefab Mode and other preview scenes.</summary>
        internal static bool IsSceneObject(GameObject _go) =>
            !EditorUtility.IsPersistent(_go)
            && _go.scene.IsValid()
            && !EditorSceneManager.IsPreviewScene(_go.scene)
            && PrefabStageUtility.GetPrefabStage(_go) == null;

        /// <summary>Owner and target inside the same outermost prefab instance of a scene.</summary>
        internal static bool InSamePrefabInstance(Object _owner, GameObject _target)
        {
            GameObject _ownerGo = GameObjectOf(_owner);
            if (_ownerGo == null || _target == null) return false;

            GameObject _root = PrefabUtility.GetOutermostPrefabInstanceRoot(_ownerGo);
            return _root != null && _root == PrefabUtility.GetOutermostPrefabInstanceRoot(_target);
        }

        // ==================== PRIVATE ====================

        private static void OnSceneSaving(Scene _scene, string _path)
        {
            // Scenes cannot be saved in Play; a save while Play is about to start ("save on Play" scripts) is still synced.
            if (EditorApplication.isPlaying) return;
            SyncScene(_scene);
        }

        private static bool NeedsId(SerializedProperty _ref)
        {
            GameObject _go = GameObjectOf(_ref.FindPropertyRelative(SceneRefPropertyWalker.DirectField).objectReferenceValue);
            return _go != null && IsSceneObject(_go) && !_go.TryGetComponent(out SceneId _)
                && !InSamePrefabInstance(_ref.serializedObject.targetObject, _go);
        }

        private static GameObject GameObjectOf(Object _target)
        {
            if (_target is GameObject _go) return _go;
            if (_target is Component _component) return _component.gameObject;
            return null;
        }

        private static bool InSameScene(Object _owner, GameObject _target)
        {
            Scene _ownerScene = default;
            if (_owner is Component _component) _ownerScene = _component.gameObject.scene;
            else if (_owner is GameObject _go) _ownerScene = _go.scene;

            return _ownerScene.IsValid() && !EditorUtility.IsPersistent(_owner) && _ownerScene == _target.scene;
        }

        /// <summary>Position among the components of the exact same concrete type on its GameObject.</summary>
        private static int IndexAmongSameType(Component _component)
        {
            Type _type = _component.GetType();
            _component.GetComponents(componentBuffer);
            int _index = 0;
            for (int _i = 0; _i < componentBuffer.Count; _i++)
            {
                if (componentBuffer[_i] == _component) break;
                if (componentBuffer[_i] != null && componentBuffer[_i].GetType() == _type) _index++;
            }

            componentBuffer.Clear();
            return _index;
        }

        private static bool SetString(SerializedProperty _ref, string _name, string _value)
        {
            SerializedProperty _property = _ref.FindPropertyRelative(_name);
            if (_property.stringValue == _value) return false;
            _property.stringValue = _value;
            return true;
        }

        private static bool SetInt(SerializedProperty _ref, string _name, int _value)
        {
            SerializedProperty _property = _ref.FindPropertyRelative(_name);
            if (_property.intValue == _value) return false;
            _property.intValue = _value;
            return true;
        }
    }
}
