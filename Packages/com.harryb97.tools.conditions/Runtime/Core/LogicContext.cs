using UnityEngine;

namespace HarryTools.Conditions {
    /// <summary>
    /// Información sobre quién ejecuta la lógica. Permite crear condiciones y acciones reutilizables
    /// que actúan sobre 'Self' o 'Target' sin necesidad de arrastrar referencias en el inspector.
    /// </summary>
    public readonly struct LogicContext {
        // ==================== VARIABLES ===================
        #region Public Variables
        public static LogicContext Empty => default;

        /// <summary> Objeto que ejecuta la lógica (normalmente, el dueño del componente) </summary>
        public GameObject Self { get; }

        /// <summary> Objeto secundario opcional (el que entra en un trigger, el que interactúa, etc.) </summary>
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
