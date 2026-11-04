using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.GameLoop {
    sealed class LoopManager : MonoBehaviour {
        // ==================== METHODS ====================
        private void Update() {
            if (!IsActiveManager()) return;

            LoopSystem.BeginIteration();
            try {
                foreach (KeyValuePair<string, ILoopable> _loop in LoopSystem.Loops) {
                    if (_loop.Value.IsPaused) continue;
                    _loop.Value.Update?.Invoke();
                }
            } finally {
                LoopSystem.EndIteration();
            }
        }

        private void FixedUpdate() {
            if (!IsActiveManager()) return;

            LoopSystem.BeginIteration();
            try {
                foreach (KeyValuePair<string, ILoopable> _loop in LoopSystem.Loops) {
                    if (_loop.Value.IsPaused) continue;
                    _loop.Value.FixedUpdate?.Invoke();
                }
            } finally {
                LoopSystem.EndIteration();
            }
        }

        private void LateUpdate() {
            if (!IsActiveManager()) return;

            LoopSystem.BeginIteration();
            try {
                foreach (KeyValuePair<string, ILoopable> _loop in LoopSystem.Loops) {
                    if (_loop.Value.IsPaused) continue;
                    _loop.Value.LateUpdate?.Invoke();
                }
            } finally {
                LoopSystem.EndIteration();
            }
        }

        // A manager that is not the registered one (stray copy saved in a scene) must not tick the loops twice
        private bool IsActiveManager() {
            if (LoopSystem.IsRegistered(gameObject)) return true;

            Destroy(gameObject);
            return false;
        }

        private void OnDestroy() {
            LoopSystem.OnManagerDestroyed(gameObject);
        }
    }
}
