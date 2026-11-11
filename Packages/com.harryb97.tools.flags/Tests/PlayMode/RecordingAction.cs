using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using UnityEngine;

namespace HarryTools.Flags.Tests
{
    /// <summary>Test action that records its tag when it starts and again when it finishes, optionally waiting frames.</summary>
    [Serializable]
    public sealed class RecordingAction : IAction
    {
        // ==================== VARIABLES ====================

        public static readonly List<string> Log = new List<string>();
        public static GameObject LastSelf;

        [SerializeField] private string tag;
        [SerializeField] private int frames;
        [SerializeField] private bool disableSelf;

        // ==================== CONSTRUCTORS ====================

        public RecordingAction(string _tag, int _frames = 0, bool _disableSelf = false)
        {
            tag = _tag;
            frames = _frames;
            disableSelf = _disableSelf;
        }

        // ==================== METHODS ====================

        public async UniTask Run(LogicContext _context, CancellationToken _token)
        {
            LastSelf = _context.Self;
            Log.Add(tag);
            if (disableSelf && _context.Self != null) _context.Self.SetActive(false);
            for (int _i = 0; _i < frames; _i++) await UniTask.Yield(PlayerLoopTiming.Update, _token);
            Log.Add(tag + ":done");
        }
    }
}
