using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HarryTools.Audio {
    /// <summary>
    /// Reference to one running conversation. Like AudioHandle it stores a slot and a generation, so a handle to a conversation
    /// that already ended (or was stopped) is stale: every operation on it does nothing.
    /// </summary>
    public readonly struct ConversationHandle : IEquatable<ConversationHandle> {
        // ==================== VARIABLES ===================
        private readonly int slot;
        private readonly int generation;

        // ==================== CONSTRUCTORS ===================
        internal ConversationHandle(int _slot, int _generation) {
            slot = _slot;
            generation = _generation;
        }

        // ==================== PROPERTIES ===================
        /// <summary> True until the last line and its PauseAfter end, or until Stop. </summary>
        public bool IsPlaying => TryGetRunner(out ConversationRunner _runner) && _runner.IsAlive(slot, generation);

        /// <summary> Index of the line being said (or waited for), or -1 when the conversation is not playing. </summary>
        public int CurrentLine => TryGetRunner(out ConversationRunner _runner) ? _runner.GetCurrentLine(slot, generation) : -1;

        // ==================== PUBLIC ===================
        /// <summary> Stops the current line with the fade (negative: its asset FadeOut; LineEnded with interrupted = true) and plays no further lines. </summary>
        public void Stop(float _fade = 0.1f) {
            if (TryGetRunner(out ConversationRunner _runner)) _runner.Stop(slot, generation, _fade);
        }

        /// <summary> Completes when the conversation ends or is stopped (or at once if it is not playing). Throws OperationCanceledException when cancelled. </summary>
        public UniTask WaitForEnd(CancellationToken _token = default) {
            if (!IsPlaying) return _token.IsCancellationRequested ? UniTask.FromCanceled(_token) : UniTask.CompletedTask;
            ConversationHandle _self = this;
            return UniTask.WaitUntil(() => !_self.IsPlaying, PlayerLoopTiming.Update, _token);
        }

        public bool Equals(ConversationHandle _other) => slot == _other.slot && generation == _other.generation;
        public override bool Equals(object _obj) => _obj is ConversationHandle _other && Equals(_other);
        public override int GetHashCode() => (slot * 397) ^ generation;
        public override string ToString() => generation == 0 ? "ConversationHandle(Invalid)" : $"ConversationHandle({slot}:{generation})";

        // ==================== PRIVATE ===================
        private bool TryGetRunner(out ConversationRunner _runner) {
            AudioRuntime _runtime = Audio.Current;
            _runner = generation != 0 && _runtime != null && !_runtime.IsDisposed ? _runtime.VoiceChannel.Conversations : null;
            return _runner != null;
        }
    }
}
