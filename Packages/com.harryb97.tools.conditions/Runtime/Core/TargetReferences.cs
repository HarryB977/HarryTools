using System;
using UnityEngine;

using HarryTools.SceneRefs;

namespace HarryTools.Conditions {
    public enum TargetSource {
        [Tooltip("The object that runs the logic")] Self,
        [Tooltip("The secondary object of the context")] Target,
        [Tooltip("A scene object referenced by id, or any object assigned in the inspector")] Custom
    }

    /// <summary> Non-generic base, needed so the drawer works with every variant </summary>
    [Serializable]
    public abstract class TargetReference {
        [SerializeField] protected TargetSource source = TargetSource.Custom;
        public TargetSource Source => source;
    }

    /// <summary> Reference to a GameObject that can come from the context (Self / Target) or from a SceneRef </summary>
    [Serializable]
    public class TargetGameObject : TargetReference {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private SceneRef<GameObject> custom = new();
        #endregion

        // ==================== START ====================
        public TargetGameObject() { }

        public TargetGameObject(TargetSource _source, GameObject _custom = null) {
            source = _source;
            custom = new SceneRef<GameObject>(_custom);
        }

        // ==================== METHODS ====================
        public GameObject Resolve(LogicContext _context) {
            return source switch {
                TargetSource.Self => _context.Self,
                TargetSource.Target => _context.Target,
                _ => custom?.Value
            };
        }
    }

    /// <summary> Reference to a component that can come from the context (Self / Target) or from a SceneRef </summary>
    [Serializable]
    public class TargetComponent<T> : TargetReference where T : Component {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private SceneRef<T> custom = new();
        #endregion

        // ==================== START ====================
        public TargetComponent() { }

        public TargetComponent(TargetSource _source, T _custom = null) {
            source = _source;
            custom = new SceneRef<T>(_custom);
        }

        // ==================== METHODS ====================
        public T Resolve(LogicContext _context) {
            if (source == TargetSource.Custom) return custom?.Value;

            GameObject _gameObject = source == TargetSource.Self ? _context.Self : _context.Target;
            return _gameObject != null ? _gameObject.GetComponent<T>() : null;
        }
    }
}
