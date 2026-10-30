using System.Collections.Generic;
using UnityEngine;

using HarryTools.Conditions;

namespace HarryTools.Actions {
    /// <summary> Componente que ejecuta una 'ActionList'. Se puede lanzar automáticamente, por código o desde un UnityEvent </summary>
    [AddComponentMenu("HarryTools/Action Runner")]
    public class ActionRunner : MonoBehaviour {
        public enum RunMoment {
            [Tooltip("Solo por código o eventos")] Manual,
            Awake,
            Start,
            OnEnable
        }

        public enum RestartMode {
            [Tooltip("Cancela la ejecución en curso y empieza de nuevo")] Restart,
            [Tooltip("No hace nada si ya se está ejecutando")] Ignore,
            [Tooltip("Permite varias ejecuciones a la vez")] Parallel
        }

        // ==================== VARIABLES ===================
        #region Public Variables
        public bool IsRunning => handles.Count > 0;
        public ActionList Actions => actions;
        #endregion

        #region Private Variables
        [SerializeField] private RunMoment runOn = RunMoment.Manual;
        [SerializeField] private RestartMode whenAlreadyRunning = RestartMode.Restart;
        [Tooltip("Cancela la ejecución al desactivar el componente"), SerializeField] private bool cancelOnDisable = true;
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
        /// <summary> Ejecuta las acciones con este objeto como 'Self' </summary>
        public void Run() => RunWithTarget(null);

        /// <summary> Ejecuta las acciones con este objeto como 'Self' y el indicado como 'Target' </summary>
        public void RunWithTarget(GameObject _target) => RunAndGetHandle(_target);

        /// <summary> Igual que 'RunWithTarget', pero devuelve el handle (null si se ignoró) </summary>
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
            // Copia: al cancelar, 'Finished' modifica la lista
            foreach (ActionHandle _handle in handles.ToArray()) _handle.Cancel();
        }

        private void OnHandleFinished(ActionHandle _handle) {
            handles.Remove(_handle);
        }
    }
}
