using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Transform/Set Transform")]
    public class SetTransformAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject target = new();
        [SerializeField] private TransformProperty property = TransformProperty.Position;
        [SerializeField] private TransformSpace space = TransformSpace.Local;
        [Space]
        [SerializeField] private Vector3 value;
        [Tooltip("Adds the value to the current one instead of replacing it"), SerializeField] private bool relative;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            GameObject _gameObject = target.Resolve(_context);
            if (_gameObject == null) return;

            Transform _transform = _gameObject.transform;
            Vector3 _value = relative ? _transform.Get(property, space) + value : value;

            _transform.Set(property, space, _value);
        }
    }
}
