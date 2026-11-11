using System.Collections.Generic;
using HarryTools.DomainReload;
using Logger = HarryTools.LoggerTool.Logger;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs
{
    /// <summary>
    /// Runtime registry of every <see cref="SceneId"/> in the loaded scenes, inactive objects included.
    /// </summary>
    public static class SceneRefs
    {
        // ==================== VARIABLES ====================

        private const string Category = "SceneRefs";

        private static readonly Dictionary<string, SceneId> byGuid = new Dictionary<string, SceneId>(System.StringComparer.Ordinal);
        private static readonly Dictionary<string, SceneId> byId = new Dictionary<string, SceneId>(SceneIdNaming.Comparer);
        private static readonly List<GameObject> rootBuffer = new List<GameObject>();
        private static readonly List<SceneId> idBuffer = new List<SceneId>();
        private static readonly List<string> removeBuffer = new List<string>();

        // Never goes back: with domain reload off, a SceneRef owned by an asset keeps the version it last warned at between Play sessions.
        private static int loadVersion = 1;

        // ==================== PROPERTIES ====================

        /// <summary>Increases with every scene load or unload and every Play session. SceneRef uses it to warn once per load.</summary>
        internal static int LoadVersion => loadVersion;

        // ==================== PUBLIC API ====================

        /// <summary>Finds a loaded object by its visible id (case insensitive). Null when not found.</summary>
        public static GameObject Find(string _id)
        {
            if (string.IsNullOrEmpty(_id)) return null;
            if (!byId.TryGetValue(_id, out SceneId _sceneId)) return null;
            if (_sceneId != null) return _sceneId.gameObject;

            byId.Remove(_id);
            return null;
        }

        /// <summary>Finds a component on the loaded object with the given visible id.</summary>
        public static bool TryFind<T>(string _id, out T _value) where T : Component
        {
            GameObject _gameObject = Find(_id);
            _value = _gameObject != null ? _gameObject.GetComponent<T>() : null;
            return _value != null;
        }

        /// <summary>
        /// Gives a runtime object a new guid and the visible id, and registers it.
        /// Logs an error and returns false when the id is already taken or the object already has an id.
        /// </summary>
        public static bool Assign(GameObject _gameObject, string _id)
        {
            if (_gameObject == null || string.IsNullOrEmpty(_id))
            {
                Logger.LogError(Category, "Assign needs a GameObject and a non empty id.");
                return false;
            }

            if (Find(_id) != null)
            {
                Logger.LogError(Category, $"Cannot assign id '{_id}' to '{_gameObject.name}': the id is already taken.");
                return false;
            }

            if (_gameObject.TryGetComponent(out SceneId _existing))
            {
                Logger.LogError(Category, $"Cannot assign id '{_id}' to '{_gameObject.name}': it already has id '{_existing.Id}'.");
                return false;
            }

            SceneId _sceneId = _gameObject.AddComponent<SceneId>();
            _sceneId.Set(SceneIdNaming.NewGuid(), _id);
            Register(_sceneId);
            return true;
        }

        // ==================== INTERNAL ====================

        internal static bool TryGetByGuid(string _guid, out SceneId _sceneId)
        {
            _sceneId = null;
            if (string.IsNullOrEmpty(_guid) || !byGuid.TryGetValue(_guid, out SceneId _found)) return false;
            if (_found == null)
            {
                byGuid.Remove(_guid);
                return false;
            }

            _sceneId = _found;
            return true;
        }

        /// <summary>Registers every SceneId of a loaded scene, inactive objects included.</summary>
        internal static void ScanScene(Scene _scene)
        {
            loadVersion++;
            if (!_scene.IsValid() || !_scene.isLoaded) return;

            _scene.GetRootGameObjects(rootBuffer);
            for (int _i = 0; _i < rootBuffer.Count; _i++)
            {
                rootBuffer[_i].GetComponentsInChildren(true, idBuffer);
                for (int _j = 0; _j < idBuffer.Count; _j++) Register(idBuffer[_j]);
            }

            rootBuffer.Clear();
            idBuffer.Clear();
        }

        internal static void Register(SceneId _sceneId)
        {
            if (_sceneId == null) return;

            string _guid = _sceneId.Guid;
            if (string.IsNullOrEmpty(_guid)) return;

            if (byGuid.TryGetValue(_guid, out SceneId _owner) && _owner != null)
            {
                if (_owner == _sceneId) return;
                Logger.LogError(Category, $"Duplicate guid '{_guid}' on '{_sceneId.gameObject.name}' (scene '{_sceneId.gameObject.scene.name}'). '{_owner.gameObject.name}' keeps it.");
                return;
            }
            byGuid[_guid] = _sceneId;

            string _id = _sceneId.Id;
            if (string.IsNullOrEmpty(_id)) return;

            if (byId.TryGetValue(_id, out SceneId _idOwner) && _idOwner != null && _idOwner != _sceneId)
            {
                Logger.LogError(Category, $"Duplicate id '{_id}' on '{_sceneId.gameObject.name}'. '{_idOwner.gameObject.name}' keeps it.");
                return;
            }
            byId[_id] = _sceneId;
        }

        /// <summary>Registers only when no other live instance holds the guid (no error otherwise).</summary>
        internal static void RegisterIfFree(SceneId _sceneId)
        {
            if (_sceneId == null || string.IsNullOrEmpty(_sceneId.Guid)) return;
            if (byGuid.TryGetValue(_sceneId.Guid, out SceneId _owner) && _owner != null && _owner != _sceneId) return;
            Register(_sceneId);
        }

        internal static void Unregister(SceneId _sceneId)
        {
            if (ReferenceEquals(_sceneId, null)) return;

            string _guid = _sceneId.Guid;
            if (!string.IsNullOrEmpty(_guid) && byGuid.TryGetValue(_guid, out SceneId _guidOwner) && ReferenceEquals(_guidOwner, _sceneId))
                byGuid.Remove(_guid);

            string _id = _sceneId.Id;
            if (!string.IsNullOrEmpty(_id) && byId.TryGetValue(_id, out SceneId _idOwner) && ReferenceEquals(_idOwner, _sceneId))
                byId.Remove(_id);
        }

        /// <summary>Empties the registry. Used by tests for isolation.</summary>
        internal static void Clear()
        {
            byGuid.Clear();
            byId.Clear();
            loadVersion++;
        }

        // ==================== SCENE EVENTS ====================

        private static void OnSceneLoaded(Scene _scene, LoadSceneMode _mode) => ScanScene(_scene);

        private static void OnSceneUnloaded(Scene _scene)
        {
            loadVersion++;

            removeBuffer.Clear();
            foreach (KeyValuePair<string, SceneId> _pair in byGuid)
                if (_pair.Value == null || _pair.Value.gameObject.scene == _scene) removeBuffer.Add(_pair.Key);
            for (int _i = 0; _i < removeBuffer.Count; _i++)
            {
                if (byGuid.TryGetValue(removeBuffer[_i], out SceneId _sceneId) && _sceneId != null && !string.IsNullOrEmpty(_sceneId.Id)
                    && byId.TryGetValue(_sceneId.Id, out SceneId _idOwner) && ReferenceEquals(_idOwner, _sceneId))
                    byId.Remove(_sceneId.Id);
                byGuid.Remove(removeBuffer[_i]);
            }
            removeBuffer.Clear();

            foreach (KeyValuePair<string, SceneId> _pair in byId)
                if (_pair.Value == null) removeBuffer.Add(_pair.Key);
            for (int _i = 0; _i < removeBuffer.Count; _i++) byId.Remove(removeBuffer[_i]);
            removeBuffer.Clear();
        }

        // ==================== INITIALIZATION ====================

        [ExecuteOnReload]
        internal static void ResetState()
        {
            byGuid.Clear();
            byId.Clear();
            loadVersion++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ScanLoadedScenes()
        {
            for (int _i = 0; _i < SceneManager.sceneCount; _i++) ScanScene(SceneManager.GetSceneAt(_i));
        }
    }
}
