using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Acoustic geometry backed by 3D physics, with raycasts run as jobs. </summary>
    internal sealed class PhysicsGeometry3D : IAcousticGeometry {
        // ==================== VARIABLES ===================
        private const int MinSweepCount = 64;
        private const float ContainsToleranceSqr = 1e-8f;

        private readonly Dictionary<int, RayBatch> active = new Dictionary<int, RayBatch>();
        private readonly Stack<RayBatch> free = new Stack<RayBatch>();
        private readonly List<RayBatch> abandoned = new List<RayBatch>();
        private readonly Dictionary<int, Collider> colliders = new Dictionary<int, Collider>();
        private readonly List<int> destroyedIds = new List<int>();

        private MaterialResolver resolver;
        private int layerMask = Physics.DefaultRaycastLayers;
        private int nextBatchId = 1;
        private int nextSweepCount = MinSweepCount;
        private bool disposed;

        // ==================== PROPERTIES ===================
        internal int AllocatedBatches => active.Count + free.Count + abandoned.Count;
        internal int CachedColliderCount => colliders.Count;

        // ==================== PUBLIC API ===================
        public void Configure(AcousticProfile _profile) {
            resolver = _profile != null ? new MaterialResolver(_profile) : null;
            layerMask = _profile != null ? (int)_profile.Geometry : Physics.DefaultRaycastLayers;
        }

        public int Schedule(ReadOnlySpan<AcousticRay> _rays, int _maxHits) {
            if (disposed) throw new ObjectDisposedException(nameof(PhysicsGeometry3D));

            ReclaimAbandoned();
            RayBatch _batch = free.Count > 0 ? free.Pop() : new RayBatch();
            _batch.Schedule(_rays, _maxHits, layerMask);

            int _id = nextBatchId++;
            active.Add(_id, _batch);
            return _id;
        }

        public bool TryGetResults(int _batch, out ReadOnlySpan<AcousticHit> _hits, out ReadOnlySpan<int> _hitCounts) {
            _hits = default;
            _hitCounts = default;
            if (!active.TryGetValue(_batch, out RayBatch _rayBatch) || !_rayBatch.IsReady) return false;

            _rayBatch.Collect(colliders);
            SweepIfDue();
            _hits = _rayBatch.Hits;
            _hitCounts = _rayBatch.HitCounts;
            return true;
        }

        /// <summary> Returns the batch to the pool. A batch still in flight is reclaimed later, without waiting for it. </summary>
        public void Release(int _batch) {
            if (!active.Remove(_batch, out RayBatch _rayBatch)) return;
            if (_rayBatch.IsJobDone) free.Push(_rayBatch);
            else abandoned.Add(_rayBatch);
        }

        public AcousticMaterial MaterialOf(int _colliderId) {
            if (resolver == null) return null;
            if (colliders.TryGetValue(_colliderId, out Collider _collider)) {
                if (_collider != null) return resolver.Resolve(_collider);
                Forget(_colliderId);
            }
            return resolver.Resolve(null);
        }

        public bool ContainsPoint(int _colliderId, Vector3 _point) {
            if (!colliders.TryGetValue(_colliderId, out Collider _collider) || _collider == null) return false;
            if (!_collider.enabled || !_collider.gameObject.activeInHierarchy) return false;

            switch (_collider) {
                case CharacterController _character:
                    return InsideCharacterCapsule(_character, _point);
                case BoxCollider _:
                case SphereCollider _:
                case CapsuleCollider _:
                case MeshCollider _mesh when _mesh.convex:
                    // ClosestPoint returns the point itself when it is inside.
                    return (_collider.ClosestPoint(_point) - _point).sqrMagnitude < ContainsToleranceSqr;
                default:
                    // ClosestPoint logs an error for any other collider type.
                    return false;
            }
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;

            foreach (KeyValuePair<int, RayBatch> _entry in active) _entry.Value.Dispose();
            active.Clear();
            for (int i = 0; i < abandoned.Count; i++) abandoned[i].Dispose();
            abandoned.Clear();
            while (free.Count > 0) free.Pop().Dispose();

            colliders.Clear();
            resolver = null;
        }

        // ==================== HELPERS ===================
        // The capsule of a CharacterController stays upright whatever the rotation of its transform.
        private static bool InsideCharacterCapsule(CharacterController _character, Vector3 _point) {
            Vector3 _scale = _character.transform.lossyScale;
            float _radius = _character.radius * Mathf.Max(Mathf.Abs(_scale.x), Mathf.Abs(_scale.z));
            float _height = Mathf.Max(_character.height * Mathf.Abs(_scale.y), _radius * 2f);
            float _halfSegment = _height * 0.5f - _radius;

            Vector3 _offset = _point - _character.transform.TransformPoint(_character.center);
            _offset.y -= Mathf.Clamp(_offset.y, -_halfSegment, _halfSegment);
            return _offset.sqrMagnitude <= _radius * _radius;
        }

        private void ReclaimAbandoned() {
            for (int i = abandoned.Count - 1; i >= 0; i--) {
                if (!abandoned[i].IsJobDone) continue;
                free.Push(abandoned[i]);
                abandoned.RemoveAt(i);
            }
        }

        private void Forget(int _colliderId) {
            colliders.Remove(_colliderId);
            resolver?.Forget(_colliderId);
        }

        // A collider destroyed after its last hit is never asked about again, so MaterialOf alone would keep it forever.
        private void SweepIfDue() {
            if (colliders.Count < nextSweepCount) return;

            destroyedIds.Clear();
            foreach (KeyValuePair<int, Collider> _entry in colliders) {
                if (_entry.Value == null) destroyedIds.Add(_entry.Key);
            }
            for (int i = 0; i < destroyedIds.Count; i++) Forget(destroyedIds[i]);
            nextSweepCount = Mathf.Max(MinSweepCount, colliders.Count * 2);
        }
    }
}
