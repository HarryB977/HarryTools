using UnityEngine;

namespace HarryTools.Conditions {
    /// <summary>
    /// Information about who runs the logic. Allows reusable conditions and actions
    /// that act on 'Self' or 'Target' without dragging references in the inspector.
    /// </summary>
    public readonly struct LogicContext {
        // ==================== VARIABLES ===================
        #region Public Variables
        public static LogicContext Empty => default;

        /// <summary> Object that runs the logic (usually the owner of the component) </summary>
        public GameObject Self { get; }

        /// <summary> Optional secondary object (the one entering a trigger, the one interacting, etc.) </summary>
        public GameObject Target { get; }
        #endregion

        // ==================== START ====================
        public LogicContext(GameObject _self, GameObject _target = null) {
            Self = _self;
            Target = _target;
        }

        // ==================== METHODS ====================
        public static LogicContext From(Component _self, GameObject _target = null) {
            return new LogicContext(_self != null ? _self.gameObject : null, _target);
        }

        public LogicContext WithTarget(GameObject _target) => new(Self, _target);
        public LogicContext WithSelf(GameObject _self) => new(_self, Target);
    }
}
