using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using UnityEngine;

namespace HarryTools.TextEffects.Tests {
    /// <summary> Test action that records "label:start" and "label:end" around a wait of N frames, optionally throwing </summary>
    [Serializable]
    public sealed class RecordingWaitAction : IAction {
        // ==================== VARIABLES ===================
        public static readonly List<string> Log = new List<string>();

        [SerializeField] private string label;
        [SerializeField] private int frames;
        [SerializeField] private bool throwAtStart;

        // ==================== CONSTRUCTORS ===================
        public RecordingWaitAction(string _label, int _frames = 0, bool _throwAtStart = false) {
            label = _label;
            frames = _frames;
            throwAtStart = _throwAtStart;
        }

        // ==================== METHODS ===================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            Log.Add(label + ":start");
            if (throwAtStart) throw new InvalidOperationException("Test failure in " + label);
            for (int _i = 0; _i < frames; _i++) await UniTask.Yield(PlayerLoopTiming.Update, _token);
            Log.Add(label + ":end");
        }
    }
}
