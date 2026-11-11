using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Application/Load Scene")]
    public class LoadSceneAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Scene name or path (must be in Build Profiles)"), SerializeField] private string sceneName;
        [SerializeField] private LoadSceneMode mode = LoadSceneMode.Single;
        [Tooltip("Waits for the load to finish (only meaningful in 'Additive' mode)"), SerializeField] private bool wait = true;
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            if (string.IsNullOrEmpty(sceneName)) return;

            AsyncOperation _operation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (_operation == null || !wait) return;

            await _operation.ToUniTask(cancellationToken: _token);
        }
    }
}
