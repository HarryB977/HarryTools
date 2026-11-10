using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary>
    /// Watches colliders for a change in bounds, a disable or a destroy, spreading the checks so each one is seen once per period.
    /// A collider found changed is dropped from the watch; whoever still cares adds it again.
    /// </summary>
    internal sealed class ColliderWatch {
        // ==================== VARIABLES ===================
        private const int InitialCapacity = 64;
        private const float MoveToleranceSqr = 1e-6f;

        private readonly IAcousticGeometry geometry;
        private readonly Dictionary<int, int> slots = new Dictionary<int, int>(InitialCapacity);
        private int[] ids = new int[InitialCapacity];
        private Bounds[] bounds = new Bounds[InitialCapacity];
        private bool[] present = new bool[InitialCapacity];
        private int[] changed = new int[InitialCapacity];
        private int count;
        private int changedCount;

        // Position of the current pass: colliders before the cursor were already checked in it.
        private int cursor;
        private float elapsed;

        // ==================== PROPERTIES ===================
        /// <summary> Ids found changed by the last <see cref="Tick"/>. </summary>
        public ReadOnlySpan<int> Changed => new ReadOnlySpan<int>(changed, 0, changedCount);

        // ==================== INITIALIZATION ===================
        public ColliderWatch(IAcousticGeometry _geometry) {
            geometry = _geometry;
        }

        // ==================== PUBLIC API ===================
        public void Clear() {
            slots.Clear();
            count = 0;
            changedCount = 0;
            cursor = 0;
            elapsed = 0f;
        }

        /// <summary> Starts watching a collider from its current state. Does nothing if it is already watched. </summary>
        public void Add(int _colliderId) {
            if (_colliderId == 0 || slots.ContainsKey(_colliderId)) return;
            if (count == ids.Length) Grow();

            ids[count] = _colliderId;
            present[count] = geometry.TryGetColliderBounds(_colliderId, out bounds[count]);
            slots.Add(_colliderId, count);
            count++;
        }

        /// <summary> Checks the share of colliders due after <paramref name="_deltaTime"/>, so all are covered every <paramref name="_period"/> seconds. Returns how many changed. </summary>
        public int Tick(float _deltaTime, float _period) {
            changedCount = 0;
            if (count == 0) {
                cursor = 0;
                elapsed = 0f;
                return 0;
            }

            elapsed += Mathf.Max(0f, _deltaTime);
            int _due = _period <= 0f || elapsed >= _period ? count : Mathf.CeilToInt(count * elapsed / _period);

            while (cursor < Mathf.Min(_due, count)) {
                if (HasChanged(cursor)) {
                    if (changedCount == changed.Length) Array.Resize(ref changed, changed.Length * 2);
                    changed[changedCount++] = ids[cursor];
                    // The last collider moves into this slot and is checked next, still within this pass.
                    RemoveAt(cursor);
                } else {
                    cursor++;
                }
            }

            if (cursor >= count) {
                cursor = 0;
                elapsed = _period > 0f ? Mathf.Clamp(elapsed - _period, 0f, _period) : 0f;
            }
            return changedCount;
        }

        // ==================== HELPERS ===================
        private bool HasChanged(int _slot) {
            bool _present = geometry.TryGetColliderBounds(ids[_slot], out Bounds _bounds);
            if (_present != present[_slot]) return true;
            if (!_present) return false;
            Bounds _old = bounds[_slot];
            return (_bounds.center - _old.center).sqrMagnitude > MoveToleranceSqr || (_bounds.extents - _old.extents).sqrMagnitude > MoveToleranceSqr;
        }

        private void RemoveAt(int _slot) {
            slots.Remove(ids[_slot]);
            int _last = count - 1;
            if (_slot != _last) {
                ids[_slot] = ids[_last];
                bounds[_slot] = bounds[_last];
                present[_slot] = present[_last];
                slots[ids[_slot]] = _slot;
            }
            count = _last;
        }

        private void Grow() {
            int _size = ids.Length * 2;
            Array.Resize(ref ids, _size);
            Array.Resize(ref bounds, _size);
            Array.Resize(ref present, _size);
        }
    }
}
