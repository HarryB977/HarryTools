using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;

namespace HarryTools.Dialogue.Tests {
    /// <summary> Line channel whose lines only finish when advanced or skipped. Logs "present:{writer note}" to <see cref="RecordingAction.Log"/> </summary>
    public sealed class FakeLineChannel : ILineChannel {
        public readonly List<FakeLineHandle> Handles = new List<FakeLineHandle>();

        public FakeLineHandle Last => Handles.Count > 0 ? Handles[Handles.Count - 1] : null;

        public ILineHandle Present(LinePresentation _line) {
            RecordingAction.Log.Add("present:" + _line.Line.WriterNote);
            FakeLineHandle _handle = new FakeLineHandle(_line);
            Handles.Add(_handle);
            return _handle;
        }
    }

    public sealed class FakeLineHandle : ILineHandle {
        public readonly LinePresentation Presentation;
        public bool Advanced;
        public bool Skipped;
        public bool Paused;
        public bool Released;

        public FakeLineHandle(LinePresentation _presentation) {
            Presentation = _presentation;
        }

        public bool IsFinished => Advanced || Skipped;
        public void Advance() => Advanced = true;
        public void Skip() => Skipped = true;
        public void SetPaused(bool _paused) => Paused = _paused;
        public void Release() => Released = true;
    }

    /// <summary> Test action that calls a delegate synchronously </summary>
    public sealed class CallbackAction : IAction {
        private readonly Action callback;

        public CallbackAction(Action _callback) {
            callback = _callback;
        }

        public UniTask Run(LogicContext _context, CancellationToken _token) {
            callback?.Invoke();
            return UniTask.CompletedTask;
        }
    }

    /// <summary> Test condition with a fixed result </summary>
    public sealed class FixedCondition : ICondition {
        private readonly bool result;

        public FixedCondition(bool _result) {
            result = _result;
        }

        public bool Check(LogicContext _context) => result;
    }
}
