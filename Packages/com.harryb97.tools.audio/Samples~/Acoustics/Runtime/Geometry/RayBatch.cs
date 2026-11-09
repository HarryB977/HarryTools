using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Reusable buffers and job state of one batch of raycasts. </summary>
    internal sealed class RayBatch : IDisposable {
        // ==================== VARIABLES ===================
        private const int MinCommandsPerJob = 16;

        private NativeArray<RaycastCommand> commands;
        private NativeArray<RaycastHit> results;
        private AcousticHit[] hits = Array.Empty<AcousticHit>();
        private int[] hitCounts = Array.Empty<int>();
        private JobHandle handle;
        private int rayCount;
        private int maxHits;
        private int scheduledFrame;
        private bool collected;

        // ==================== PROPERTIES ===================
        public bool IsCreated => commands.IsCreated || results.IsCreated;
        public bool IsJobDone => handle.IsCompleted;
        public bool IsReady => Time.frameCount > scheduledFrame && handle.IsCompleted;
        public ReadOnlySpan<AcousticHit> Hits => new ReadOnlySpan<AcousticHit>(hits, 0, rayCount * maxHits);
        public ReadOnlySpan<int> HitCounts => new ReadOnlySpan<int>(hitCounts, 0, rayCount);

        // ==================== PUBLIC API ===================
        /// <summary> Starts the raycasts. The previous job of this batch must have finished. </summary>
        public void Schedule(ReadOnlySpan<AcousticRay> _rays, int _maxHits, int _layerMask) {
            handle.Complete();
            handle = default;
            rayCount = _rays.Length;
            maxHits = Mathf.Max(0, _maxHits);
            scheduledFrame = Time.frameCount;
            collected = false;

            int _hitCapacity = rayCount * maxHits;
            if (hitCounts.Length < rayCount) hitCounts = new int[Mathf.Max(rayCount, hitCounts.Length * 2)];
            if (hits.Length < _hitCapacity) hits = new AcousticHit[Mathf.Max(_hitCapacity, hits.Length * 2)];
            if (_hitCapacity == 0) return;

            Grow(ref commands, rayCount);
            Grow(ref results, _hitCapacity);

            QueryParameters _query = new QueryParameters(_layerMask, false, QueryTriggerInteraction.Ignore, false);
            for (int i = 0; i < rayCount; i++) commands[i] = ToCommand(_rays[i], _query);

            handle = RaycastCommand.ScheduleBatch(commands.GetSubArray(0, rayCount), results.GetSubArray(0, _hitCapacity), MinCommandsPerJob, maxHits);
            JobHandle.ScheduleBatchedJobs();
        }

        /// <summary> Copies the finished job into the managed arrays, once. Resolves colliders on the main thread. </summary>
        public void Collect(Dictionary<int, Collider> _colliders) {
            handle.Complete();
            if (collected) return;
            collected = true;

            if (maxHits == 0) {
                Array.Clear(hitCounts, 0, rayCount);
                return;
            }

            for (int i = 0; i < rayCount; i++) {
                int _start = i * maxHits;
                int _count = commands[i].distance > 0f ? ReadRay(_start, _colliders) : 0;
                SortByDistance(hits, _start, _count);
                hitCounts[i] = _count;
            }
        }

        public void Dispose() {
            handle.Complete();
            handle = default;
            if (commands.IsCreated) commands.Dispose();
            if (results.IsCreated) results.Dispose();
        }

        /// <summary> Insertion sort: the API does not document any order and rays cross few surfaces. </summary>
        public static void SortByDistance(AcousticHit[] _hits, int _start, int _count) {
            int _end = _start + _count;
            for (int i = _start + 1; i < _end; i++) {
                AcousticHit _current = _hits[i];
                int _slot = i - 1;
                while (_slot >= _start && _hits[_slot].Distance > _current.Distance) {
                    _hits[_slot + 1] = _hits[_slot];
                    _slot--;
                }
                _hits[_slot + 1] = _current;
            }
        }

        // ==================== HELPERS ===================
        private int ReadRay(int _start, Dictionary<int, Collider> _colliders) {
            int _count = 0;
            for (int i = 0; i < maxHits; i++) {
                RaycastHit _result = results[_start + i];
                // Not RaycastHit.collider: it is also null for a valid hit whose collider was destroyed in flight.
#pragma warning disable CS0618 // colliderInstanceID is obsolete in 6000.3 but colliderEntityId does not exist in 6000.0.
                int _id = _result.colliderInstanceID;
#pragma warning restore CS0618
                if (_id == 0) break;

                hits[_start + _count] = new AcousticHit { Distance = _result.distance, Point = _result.point, ColliderId = _id };
                _count++;

                if (_colliders.ContainsKey(_id)) continue;
                Collider _collider = _result.collider;
                if (_collider != null) _colliders.Add(_id, _collider);
            }
            return _count;
        }

        internal static RaycastCommand ToCommand(in AcousticRay _ray, QueryParameters _query) {
            Vector3 _delta = _ray.To - _ray.From;
            float _distance = _delta.magnitude;
            // The negated comparison also catches NaN. Collect reports no hits for a zero distance command.
            if (!(_distance > Mathf.Epsilon) || float.IsInfinity(_distance)) return new RaycastCommand(Vector3.zero, Vector3.forward, _query, 0f);
            return new RaycastCommand(_ray.From, _delta / _distance, _query, _distance);
        }

        private static void Grow<T>(ref NativeArray<T> _array, int _needed) where T : struct {
            if (_array.IsCreated && _array.Length >= _needed) return;
            int _capacity = _needed;
            if (_array.IsCreated) {
                _capacity = Mathf.Max(_needed, _array.Length * 2);
                _array.Dispose();
            }
            _array = new NativeArray<T>(_capacity, Allocator.Persistent);
        }
    }
}
