using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Object = UnityEngine.Object;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("GameObject/Instantiate")]
    public class InstantiateAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private GameObject prefab;
        [Tooltip("Origin position and rotation (empty = the prefab's)"), SerializeField] private TargetGameObject spawnPoint = new(TargetSource.Self);
        [Tooltip("Parent of the created object (empty = scene root)"), SerializeField] private TargetComponent<Transform> parent = new();
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            if (prefab == null) return;

            GameObject _spawnPoint = spawnPoint.Resolve(_context);
            Transform _parent = parent?.Resolve(_context);

            if (_spawnPoint == null) Object.Instantiate(prefab, _parent);
            else Object.Instantiate(prefab, _spawnPoint.transform.position, _spawnPoint.transform.rotation, _parent);
        }
    }
}
