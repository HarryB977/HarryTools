using System.Collections.Generic;
using UnityEngine;
    
namespace HarryTools.GameLoop {
    sealed class LoopManager : MonoBehaviour {
        private void Update() {
            foreach (KeyValuePair<string, ILoopable> _loop in LoopSystem.Loops) {
                if (_loop.Value.IsPaused) continue;
                _loop.Value.Update?.Invoke();
            }
        }

        private void FixedUpdate() {
            foreach (KeyValuePair<string, ILoopable> _loop in LoopSystem.Loops) {
                if (_loop.Value.IsPaused) continue;
                _loop.Value.FixedUpdate?.Invoke();
            }
        }

        private void LateUpdate() {
            foreach (KeyValuePair<string, ILoopable> _loop in LoopSystem.Loops) {
                if (_loop.Value.IsPaused) continue;
                _loop.Value.LateUpdate?.Invoke();
            }
        }
    }
}