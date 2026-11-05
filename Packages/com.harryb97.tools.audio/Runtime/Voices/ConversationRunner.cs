using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary>
    /// Runs conversations: one async loop per conversation that says each line through VoiceChannel (the emitter of its speaker,
    /// else 2D), waits for it to end and then waits PauseAfter in unscaled seconds that only count while Audio is not paused
    /// (globally, through the bus of the line or through Master). Conversations live in reusable slots addressed by
    /// ConversationHandle (slot + generation).
    /// </summary>
    internal sealed class ConversationRunner {
        private sealed class Slot {
            public int Generation;
            public bool Alive;
            public ConversationAsset Asset;
            public int CurrentLine = -1;
            public AudioHandle Line;
        }

        // ==================== VARIABLES ===================
        // Not reset on reload on purpose (as in VoiceManager): generations keep growing across runtimes, so a handle from a previous runtime never matches.
        private static int generationSeed;

        private readonly AudioRuntime runtime;
        private readonly VoiceChannel channel;
        private readonly List<Slot> slots = new List<Slot>(4);
        private bool disposed;

        // ==================== CONSTRUCTOR ===================
        public ConversationRunner(AudioRuntime _runtime, VoiceChannel _channel) {
            runtime = _runtime;
            channel = _channel;
        }

        // ==================== PUBLIC ===================
        public ConversationHandle Start(ConversationAsset _conversation) {
            if (disposed || _conversation == null) return default;
            int _index = FreeSlot();
            Slot _slot = slots[_index];
            _slot.Generation = ++generationSeed;
            _slot.Alive = true;
            _slot.Asset = _conversation;
            _slot.CurrentLine = -1;
            _slot.Line = AudioHandle.Invalid;
            var _handle = new ConversationHandle(_index, _slot.Generation);
            // Runs synchronously up to the first wait, so the first line starts before Start returns.
            Run(_slot, _slot.Generation).Forget();
            return _handle;
        }

        public bool IsAlive(int _slot, int _generation) => TryGetSlot(_slot, _generation, out _);

        public int GetCurrentLine(int _slot, int _generation) => TryGetSlot(_slot, _generation, out Slot _found) ? _found.CurrentLine : -1;

        public void Stop(int _slot, int _generation, float _fade) {
            if (!TryGetSlot(_slot, _generation, out Slot _found)) return;
            AudioHandle _line = _found.Line;
            Finish(_found);
            channel.StopLine(_line, _fade);
        }

        public void StopAll(float _fade) {
            for (int _i = 0; _i < slots.Count; _i++) {
                Slot _slot = slots[_i];
                if (_slot.Alive) Stop(_i, _slot.Generation, _fade);
            }
        }

        public void Dispose() {
            disposed = true;
            for (int _i = 0; _i < slots.Count; _i++) Finish(slots[_i]);
        }

        // ==================== PRIVATE ===================
        private async UniTaskVoid Run(Slot _slot, int _generation) {
            try {
                IReadOnlyList<ConversationLine> _lines = _slot.Asset.Lines;
                for (int _i = 0; _i < _lines.Count; _i++) {
                    if (!IsCurrent(_slot, _generation)) return;
                    ConversationLine _line = _lines[_i];
                    _slot.CurrentLine = _i;

                    if (_line.Voice != null) {
                        AudioHandle _handle = channel.Play(_line.Voice);
                        // The handle may already be gone if a LineStarted handler stopped the conversation.
                        if (!IsCurrent(_slot, _generation)) return;
                        _slot.Line = _handle;
                        while (runtime.Voices.TryGetVoice(_handle, out _)) {
                            await UniTask.Yield(PlayerLoopTiming.Update);
                            if (!IsCurrent(_slot, _generation)) return;
                        }
                        _slot.Line = AudioHandle.Invalid;
                    }

                    AudioBus _bus = _line.Voice != null ? _line.Voice.Bus : AudioBus.Voice;
                    float _remaining = _line.PauseAfter;
                    while (_remaining > 0f) {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                        if (!IsCurrent(_slot, _generation)) return;
                        if (!IsPaused(_bus)) _remaining -= Time.unscaledDeltaTime;
                    }
                }
                if (IsCurrent(_slot, _generation)) Finish(_slot);
            } catch (Exception _exception) {
                Debug.LogException(_exception);
                if (IsCurrent(_slot, _generation)) Finish(_slot);
            }
        }

        private bool IsCurrent(Slot _slot, int _generation) => !disposed && _slot.Alive && _slot.Generation == _generation;

        private bool IsPaused(AudioBus _bus) {
            VoiceManager _voices = runtime.Voices;
            return _voices.IsPaused || _voices.IsBusPaused(_bus) || _voices.IsBusPaused(AudioBus.Master);
        }

        private bool TryGetSlot(int _slot, int _generation, out Slot _found) {
            if (!disposed && (uint)_slot < (uint)slots.Count) {
                _found = slots[_slot];
                if (_found.Alive && _found.Generation == _generation) return true;
            }
            _found = null;
            return false;
        }

        // Frees the slot: its generation no longer matches any handle, and its loop exits on its next check.
        private static void Finish(Slot _slot) {
            _slot.Alive = false;
            _slot.Generation = ++generationSeed;
            _slot.Asset = null;
            _slot.CurrentLine = -1;
            _slot.Line = AudioHandle.Invalid;
        }

        private int FreeSlot() {
            for (int _i = 0; _i < slots.Count; _i++) {
                if (!slots[_i].Alive) return _i;
            }
            slots.Add(new Slot());
            return slots.Count - 1;
        }
    }
}
