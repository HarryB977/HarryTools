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
        [Tooltip("Posición y rotación de origen (vacío = las del prefab)"), SerializeField] private TargetGameObject spawnPoint = new(TargetSource.Self);
        [Tooltip("Padre del objeto creado (vacío = raíz de la escena)"), SerializeField] private Transform parent;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            if (prefab == null) return;

            GameObject _spawnPoint = spawnPoint.Resolve(_context);

            if (_spawnPoint == null) Object.Instantiate(prefab, parent);
            else Object.Instantiate(prefab, _spawnPoint.transform.position, _spawnPoint.transform.rotation, parent);
        }
    }
}
