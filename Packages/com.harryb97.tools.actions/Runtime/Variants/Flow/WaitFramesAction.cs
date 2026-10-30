using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Flow/Wait Frames", -99)]
    public class WaitFramesAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField, Min(1)] private int frames = 1;
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            return UniTask.DelayFrame(frames, PlayerLoopTiming.Update, _token);
        }
    }
}
