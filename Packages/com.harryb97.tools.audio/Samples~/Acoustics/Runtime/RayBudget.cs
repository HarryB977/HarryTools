using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Rays and main thread time the acoustic modules may use in one frame, shared between them. Refills once per frame. </summary>
    internal sealed class RayBudget {
        // ==================== VARIABLES ===================
        // Frames the later module may wait in a row before the first one gives up a frame for it.
        private const int MaxWaits = 3;

        private int limit;
        private int consumed;
        private long timeLimit;
        private long timeSpent;
        private long workStarted;
        private object firstWorker;
        private int waits;
        private int frame = -1;

        // ==================== PROPERTIES ===================
        /// <summary> Rays still available this frame. </summary>
        public int Remaining {
            get {
                Refill();
                return Mathf.Max(0, limit - consumed);
            }
        }

        /// <summary> Milliseconds of main thread time still available this frame. </summary>
        public float RemainingMs {
            get {
                Refill();
                return System.Math.Max(0L, timeLimit - timeSpent) * 1000f / Stopwatch.Frequency;
            }
        }

        // ==================== INITIALIZATION ===================
        public RayBudget(int _perFrame, float _milliseconds) {
            SetLimit(_perFrame);
            SetTimeLimit(_milliseconds);
        }

        /// <summary> Changes the rays per frame. Rays already consumed this frame still count. </summary>
        public void SetLimit(int _perFrame) {
            limit = Mathf.Max(0, _perFrame);
        }

        /// <summary> Changes the main thread milliseconds per frame. Time already spent this frame still counts. </summary>
        public void SetTimeLimit(float _milliseconds) {
            timeLimit = (long)(Mathf.Max(0f, _milliseconds) * Stopwatch.Frequency / 1000f);
        }

        // ==================== RAYS ===================
        /// <summary> Takes up to <paramref name="_rays"/> rays from this frame and returns how many were granted. </summary>
        public int TryConsume(int _rays) {
            int _granted = Mathf.Clamp(_rays, 0, Remaining);
            consumed += _granted;
            return _granted;
        }

        // ==================== TIME ===================
        /// <summary> Starts timing a module and returns the timestamp at which the time left this frame runs out. </summary>
        public long BeginWork() {
            Refill();
            workStarted = Stopwatch.GetTimestamp();
            return workStarted + System.Math.Max(0L, timeLimit - timeSpent);
        }

        /// <summary> Adds the time since <see cref="BeginWork"/> to this frame. </summary>
        public void EndWork() {
            Refill();
            timeSpent += Stopwatch.GetTimestamp() - workStarted;
        }

        /// <summary>
        /// True when <paramref name="_owner"/> may do one more unit of work. Past the deadline only the first unit of the module that worked first this frame goes through,
        /// so a tiny budget slows the simulation down instead of stopping it, without paying that minimum twice.
        /// </summary>
        public bool MayWork(object _owner, long _deadline, bool _firstUnit) {
            Refill();
            if (firstWorker == null) firstWorker = _owner;
            if (Stopwatch.GetTimestamp() < _deadline) return true;
            return _firstUnit && firstWorker == _owner;
        }

        /// <summary> True when another module already used up this frame, so <paramref name="_owner"/> must wait for the next one. Counts the waits that <see cref="ShouldYield"/> reads. </summary>
        public bool WaitIfSpent(object _owner) {
            Refill();
            if (firstWorker != null && firstWorker != _owner && timeSpent >= timeLimit) {
                waits++;
                return true;
            }
            waits = 0;
            return false;
        }

        /// <summary> True when the module that runs first must skip this frame because the later one has waited too long. It keeps priority the rest of the time. </summary>
        public bool ShouldYield() {
            Refill();
            if (waits < MaxWaits) return false;
            waits = 0;
            return true;
        }

        // ==================== HELPERS ===================
        private void Refill() {
            int _now = Time.frameCount;
            if (_now == frame) return;
            frame = _now;
            consumed = 0;
            timeSpent = 0L;
            firstWorker = null;
        }
    }
}
