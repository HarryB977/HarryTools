using System.Collections.Generic;
using UnityEngine;

using HarryTools.Conditions;

namespace HarryTools.Actions {
    /// <summary> Component that runs an 'ActionList'. It can start automatically, from code or from a UnityEvent </summary>
    [AddComponentMenu("HarryTools/Action Runner")]
    public class ActionRunner : MonoBehaviour {
        public enum RunMoment {
            [Tooltip("Only from code or events")] Manual,
            Awake,
            Start,
            OnEnable
        }

        public enum RestartMode {
            [Tooltip("Cancels the running execution and starts again")] Restart,
            [Tooltip("Does nothing if it is already running")] Ignore,
            [Tooltip("Allows several executions at once")] Parallel
        }

        // ==================== VARIABLES ===================
        #region Public Variables
        public bool IsRunning => handles.Count > 0;
        public ActionList Actions => actions;
        #endregion

        #region Private Variables
        [SerializeField] private RunMoment runOn = RunMoment.Manual;
        [SerializeField] private RestartMode whenAlreadyRunning = RestartMode.Restart;
        [Tooltip("Cancels the execution when the component is disabled"), SerializeField] private bool cancelOnDisable = true;
        [Space]
        [SerializeField] private ActionList actions = new();

        private readonly List<ActionHandle> handles = new();
        #endregion

        // ==================== START ====================
        private void Awake() {
            if (runOn == RunMoment.Awake) Run();
        }

        private void Start() {
            if (runOn == RunMoment.Start) Run();
        }

        private void OnEnable() {
            if (runOn == RunMoment.OnEnable) Run();
        }

        private void OnDisable() {
            if (cancelOnDisable) Cancel();
        }

        // ==================== METHODS ====================
        /// <summary> Runs the actions with this object as 'Self' </summary>
        public void Run() => RunWithTarget(null);

        /// <summary> Runs the actions with this object as 'Self' and the given one as 'Target' </summary>
        public void RunWithTarget(GameObject _target) => RunAndGetHandle(_target);

        /// <summary> Same as 'RunWithTarget', but returns the handle (null if ignored) </summary>
        public ActionHandle RunAndGetHandle(GameObject _target = null) {
            if (IsRunning) {
                if (whenAlreadyRunning == RestartMode.Ignore) return null;
                if (whenAlreadyRunning == RestartMode.Restart) Cancel();
            }

            ActionHandle _handle = actions.Run(LogicContext.From(this, _target), destroyCancellationToken);
            if (!_handle.IsRunning) return _handle;

            handles.Add(_handle);
            _handle.Finished += OnHandleFinished;

            return _handle;
        }

        public void Cancel() {
            // Copy: when cancelling, 'Finished' modifies the list
            foreach (ActionHandle _handle in handles.ToArray()) _handle.Cancel();
        }

        private void OnHandleFinished(ActionHandle _handle) {
            handles.Remove(_handle);
        }
    }
}
