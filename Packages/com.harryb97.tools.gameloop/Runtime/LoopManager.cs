using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.GameLoop {
    sealed class LoopManager : MonoBehaviour {
        // ==================== METHODS ====================
        private void Update() {
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

        private void OnDestroy() {
            LoopSystem.OnManagerDestroyed(gameObject);
        }
    }
}
