using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Runner {
    public class Runner : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Public Variables
        public static Runner Get {
            get {
                if (runner == null) {
                    GameObject _runner = new("Runner");
                    runner = _runner.AddComponent<Runner>();
                    DontDestroyOnLoad(runner);
                }

                return runner;
            }
        }
        #endregion
        
        #region Private Variables
        private static Runner runner;

        private List<IRunnable> runnables = new();
        private List<ILateRunnable> lateRunnables = new();
        private List<IFixedRunnable> fixedRunnables = new();

        private bool runnablesState = true;
        private bool lateRunnablesState = true;
        private bool fixedRunnablesState = true;
        #endregion
        
        // ==================== START ====================
        private void Update() {
            if (!runnablesState) return;
            
            for (int i = 0; i < runnables.Count; i++) {
                runnables[i].Ticks();
            }
        }

        private void LateUpdate() {
            if (!lateRunnablesState) return;

            for (int i = 0; i < lateRunnables.Count; i++) {
                lateRunnables[i].LateTicks();
            }
        }

        private void FixedUpdate() {
            if (!fixedRunnablesState) return;

            for (int i = 0; i < fixedRunnables.Count; i++) {
                fixedRunnables[i].FixedTicks();
            }
        }
        
        // ==================== METHODS ====================
        public void AddTicks(IRunnable _runnable) {
            if (!CheckTicks(_runnable)) runnables.Add(_runnable);
        }

        public void AddLateTicks(ILateRunnable _lateRunnable) {
            if (!CheckLateTicks(_lateRunnable)) lateRunnables.Add(_lateRunnable);
        }

        public void AddFixedTicks(IFixedRunnable _fixedRunnable) {
            if (!CheckFixedTicks(_fixedRunnable)) fixedRunnables.Add(_fixedRunnable);
        }

        // =================================================

        public bool CheckTicks(IRunnable _runnable) => runnables.Contains(_runnable);
        public bool CheckLateTicks(ILateRunnable _lateRunnable) => lateRunnables.Contains(_lateRunnable);
        public bool CheckFixedTicks(IFixedRunnable _fixedRunnable) => fixedRunnables.Contains(_fixedRunnable);
        
        // =================================================

        public void RemoveTicks(IRunnable _runnable) {
            if (CheckTicks(_runnable)) runnables.Remove(_runnable);
        }

        public void RemoveLateTicks(ILateRunnable _lateRunnable) {
            if (CheckLateTicks(_lateRunnable)) lateRunnables.Remove(_lateRunnable);
        }

        public void RemoveFixedTicks(IFixedRunnable _fixedRunnable) {
            if (CheckFixedTicks(_fixedRunnable)) fixedRunnables.Remove(_fixedRunnable);
        }
        
        // =================================================

        public void StopTicks() => runnablesState = false;
        public void StopLateTicks() => lateRunnablesState = false;
        public void StopFixedTicks() => fixedRunnablesState = false;

        public void ResumeTicks() => runnablesState = true;
        public void ResumeLateTicks() => lateRunnablesState = true;
        public void ResumeFixedTicks() => fixedRunnablesState = true;
    }
}