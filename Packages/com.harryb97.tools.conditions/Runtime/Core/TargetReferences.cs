using System;
using UnityEngine;

namespace HarryTools.Conditions {
    public enum TargetSource {
        [Tooltip("El objeto que ejecuta la lógica")] Self,
        [Tooltip("El objeto secundario del contexto")] Target,
        [Tooltip("Un objeto asignado en el inspector")] Custom
    }

    /// <summary> Base no genérica, necesaria para que el drawer funcione con todas las variantes </summary>
    [Serializable]
    public abstract class TargetReference {
        [SerializeField] protected TargetSource source = TargetSource.Custom;
        public TargetSource Source => source;
    }

    /// <summary> Referencia a un GameObject que puede venir del contexto (Self / Target) o asignarse en el inspector </summary>
    [Serializable]
    public class TargetGameObject : TargetReference {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private GameObject custom;
        #endregion

        // ==================== START ====================
        public TargetGameObject() { }

        public TargetGameObject(TargetSource _source, GameObject _custom = null) {
            source = _source;
            custom = _custom;
        }

        // ==================== METHODS ====================
        public GameObject Resolve(LogicContext _context) {
            return source switch {
                TargetSource.Self => _context.Self,
                TargetSource.Target => _context.Target,
                _ => custom
            };
        }
    }

    /// <summary> Referencia a un componente que puede venir del contexto (Self / Target) o asignarse en el inspector </summary>
    [Serializable]
    public class TargetComponent<T> : TargetReference where T : Component {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private T custom;
        #endregion

        // ==================== START ====================
        public TargetComponent() { }

        public TargetComponent(TargetSource _source, T _custom = null) {
            source = _source;
            custom = _custom;
        }

        // ==================== METHODS ====================
        public T Resolve(LogicContext _context) {
            GameObject _gameObject = source switch {
                TargetSource.Self => _context.Self,
                TargetSource.Target => _context.Target,
                _ => null
            };

            if (source == TargetSource.Custom) return custom;
            return _gameObject != null ? _gameObject.GetComponent<T>() : null;
        }
    }
}
