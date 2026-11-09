using System;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HarryTools.Audio.Acoustics {
    /// <summary>
    /// The single module that computes everything that changes volume and filters: occlusion, transmission and air absorption, with the audibility floor applied last.
    /// Rays are scheduled in one Evaluate and read in a later one; until then each voice keeps its last finished result (neutral at first).
    /// Only one batch is in flight, so a batch never goes over the rays of one frame.
    /// </summary>
    internal sealed class PropagationModule : IAcousticModule, IDisposable {
        // ==================== TYPES ===================
        private struct Slot {
            public AudioHandle Handle;
            public AcousticResult Result;
            public float NextDue;
            public int SeenStamp;
            public int PickedStamp;
        }

        private struct BatchEntry {
            public int Slot;
            public AudioHandle Handle;
            public int RayStart;
            public int SphereRays;
            public float Distance;
        }

        // ==================== VARIABLES ===================
        private const int InitialSlots = 16;
        private const int NoBatch = -1;
        // Source to listener and listener to source, the pair that measures thickness.
        private const int CenterRays = 2;
        private const float MinDistance = 0.001f;
        private static readonly float DefaultRadius = new AcousticSourceExtension().Radius;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Acoustics.Propagation");

        private readonly IAcousticGeometry geometry;
        private AcousticProfile profile;
        private Vector3[] sphere = Array.Empty<Vector3>();
        private int maxHits = 1;

        private Slot[] slots = new Slot[InitialSlots];
        private int[] slotOfQuery = new int[InitialSlots];
        private BatchEntry[] entries = new BatchEntry[InitialSlots];
        private AcousticRay[] rays = new AcousticRay[InitialSlots];
        private int entryCount;
        private int entryCursor;
        private int batch = NoBatch;
        private int stamp;
        private bool disposed;

        // ==================== PROPERTIES ===================
        public int Order => 0;
        // Each voice is throttled by the UpdateInterval of the profile here, so one late voice never holds the others back.
        public float UpdateInterval => 0f;

        // ==================== INITIALIZATION ===================
        public PropagationModule(AcousticProfile _profile, IAcousticGeometry _geometry) {
            geometry = _geometry;
            Configure(_profile);
        }

        /// <summary> Switches profile. A batch in flight is dropped; voices keep their last result and are evaluated again right away. </summary>
        public void Configure(AcousticProfile _profile) {
            profile = _profile;
            geometry.Configure(_profile);
            DropBatch();

            maxHits = Mathf.Max(1, _profile.MaxHitsPerRay);
            sphere = BuildSphere(Mathf.Max(0, _profile.OcclusionRays));
            for (int i = 0; i < slots.Length; i++) slots[i].NextDue = 0f;

            if ((_profile.Occlusion || _profile.Transmission) && _profile.RayBudgetPerFrame < CenterRays) {
                HarryTools.LoggerTool.Logger.LogWarning("Audio", "Acoustic profile '" + _profile.name + "' has a ray budget below " + CenterRays + ": nothing can be traced and voices stay neutral.");
            }
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            geometry.Dispose();
        }

        // ==================== EVALUATION ===================
        public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
            if (disposed) return;

            using (Marker.Auto()) {
                long _deadline = Stopwatch.GetTimestamp() + (long)(profile.MainThreadBudgetMs * Stopwatch.Frequency / 1000f);
                EnsureCapacity(_queries.Length);
                AssignSlots(_queries);
                ReadBatch(_deadline);
                if (batch == NoBatch) ScheduleDue(_queries, _deadline);
                for (int i = 0; i < _queries.Length; i++) _results[i] = slots[slotOfQuery[i]].Result;
            }
        }

        // ==================== SLOTS ===================
        private void AssignSlots(ReadOnlySpan<AcousticQuery> _queries) {
            stamp++;
            for (int i = 0; i < _queries.Length; i++) {
                slotOfQuery[i] = -1;
                for (int s = 0; s < slots.Length; s++) {
                    if (slots[s].Handle != _queries[i].Handle || slots[s].SeenStamp == stamp) continue;
                    slots[s].SeenStamp = stamp;
                    slotOfQuery[i] = s;
                    break;
                }
            }

            for (int i = 0; i < _queries.Length; i++) {
                if (slotOfQuery[i] >= 0) continue;
                int _free = LeastRecentlySeen();
                slots[_free] = new Slot { Handle = _queries[i].Handle, Result = AcousticResult.Neutral, SeenStamp = stamp };
                slotOfQuery[i] = _free;
            }
        }

        private int LeastRecentlySeen() {
            int _best = 0;
            int _bestStamp = int.MaxValue;
            for (int s = 0; s < slots.Length; s++) {
                if (slots[s].SeenStamp == stamp || slots[s].SeenStamp >= _bestStamp) continue;
                _best = s;
                _bestStamp = slots[s].SeenStamp;
            }
            return _best;
        }

        private void EnsureCapacity(int _queries) {
            if (slots.Length >= _queries) return;
            Array.Resize(ref slots, _queries);
            Array.Resize(ref slotOfQuery, _queries);
            Array.Resize(ref entries, _queries);
        }

        // ==================== SCHEDULING ===================
        private void ScheduleDue(ReadOnlySpan<AcousticQuery> _queries, long _deadline) {
            float _now = Time.unscaledTime;
            float _interval = Mathf.Max(0f, profile.UpdateInterval);
            int _rayCount = 0;
            entryCount = 0;
            entryCursor = 0;

            while (true) {
                int _next = MostOverdue(_queries.Length, _now);
                if (_next < 0) break;
                // The first voice always goes through, so a tiny budget slows the simulation down instead of stopping it.
                if (entryCount > 0 && Stopwatch.GetTimestamp() > _deadline) break;

                int _slot = slotOfQuery[_next];
                AcousticQuery _query = _queries[_next];
                AcousticSourceExtension _extension = _query.Asset != null ? _query.Asset.GetExtension<AcousticSourceExtension>() : null;
                float _distance = Vector3.Distance(_query.SourcePosition, _query.ListenerPosition);

                // Also catches NaN positions.
                if ((_extension != null && _extension.Exclude) || !(_distance > MinDistance)) {
                    Finish(_slot, AcousticResult.Neutral, _now, _interval);
                    continue;
                }

                if (!profile.Occlusion && !profile.Transmission) {
                    Finish(_slot, ToResult(ApplyAir(Vector3.one, _distance)), _now, _interval);
                    continue;
                }

                int _sphereRays = profile.Occlusion ? sphere.Length : 0;
                int _available = profile.RayBudgetPerFrame - _rayCount;
                if (CenterRays + _sphereRays > _available) {
                    if (entryCount > 0 || _available < CenterRays) break;
                    _sphereRays = _available - CenterRays;
                }

                float _radius = _extension != null ? Mathf.Max(0f, _extension.Radius) : DefaultRadius;
                EnsureRays(_rayCount + CenterRays + _sphereRays);
                entries[entryCount++] = new BatchEntry { Slot = _slot, Handle = _query.Handle, RayStart = _rayCount, SphereRays = _sphereRays, Distance = _distance };
                _rayCount = WriteRays(_query.SourcePosition, _query.ListenerPosition, _radius, _sphereRays, _rayCount);

                slots[_slot].PickedStamp = stamp;
                slots[_slot].NextDue = _now + _interval;
            }

            if (entryCount > 0) batch = geometry.Schedule(new ReadOnlySpan<AcousticRay>(rays, 0, _rayCount), maxHits);
        }

        private void Finish(int _slot, in AcousticResult _result, float _now, float _interval) {
            slots[_slot].Result = _result;
            slots[_slot].PickedStamp = stamp;
            slots[_slot].NextDue = _now + _interval;
        }

        private int MostOverdue(int _queryCount, float _now) {
            int _best = -1;
            float _bestDue = float.MaxValue;
            for (int i = 0; i < _queryCount; i++) {
                ref Slot _slot = ref slots[slotOfQuery[i]];
                if (_slot.PickedStamp == stamp || _slot.NextDue > _now || _slot.NextDue >= _bestDue) continue;
                _best = i;
                _bestDue = _slot.NextDue;
            }
            return _best;
        }

        private int WriteRays(Vector3 _source, Vector3 _listener, float _radius, int _sphereRays, int _at) {
            rays[_at++] = new AcousticRay { From = _source, To = _listener };
            rays[_at++] = new AcousticRay { From = _listener, To = _source };
            for (int i = 0; i < _sphereRays; i++) rays[_at++] = new AcousticRay { From = _source + sphere[i] * _radius, To = _listener };
            return _at;
        }

        private void EnsureRays(int _needed) {
            if (rays.Length < _needed) Array.Resize(ref rays, Mathf.Max(_needed, rays.Length * 2));
        }

        // Fibonacci lattice: evenly spread points without clumps at the poles.
        private static Vector3[] BuildSphere(int _count) {
            Vector3[] _points = new Vector3[_count];
            float _goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < _count; i++) {
                float _y = 1f - (i + 0.5f) * 2f / _count;
                float _ring = Mathf.Sqrt(1f - _y * _y);
                float _angle = i * _goldenAngle;
                _points[i] = new Vector3(Mathf.Cos(_angle) * _ring, _y, Mathf.Sin(_angle) * _ring);
            }
            return _points;
        }

        // ==================== READING ===================
        private void ReadBatch(long _deadline) {
            if (batch == NoBatch) return;
            if (!geometry.TryGetResults(batch, out ReadOnlySpan<AcousticHit> _hits, out ReadOnlySpan<int> _counts)) return;

            // What does not fit in the time of this frame is read in the next; the hits stay valid until Release.
            int _read = 0;
            while (entryCursor < entryCount) {
                if (_read > 0 && Stopwatch.GetTimestamp() > _deadline) return;
                BatchEntry _entry = entries[entryCursor++];
                _read++;
                if (slots[_entry.Slot].Handle == _entry.Handle) slots[_entry.Slot].Result = Compute(_entry, _hits, _counts);
            }
            DropBatch();
        }

        private void DropBatch() {
            if (batch != NoBatch) geometry.Release(batch);
            batch = NoBatch;
            entryCount = 0;
            entryCursor = 0;
        }

        // ==================== ACOUSTICS ===================
        private AcousticResult Compute(in BatchEntry _entry, ReadOnlySpan<AcousticHit> _hits, ReadOnlySpan<int> _counts) {
            ReadOnlySpan<AcousticHit> _forward = _hits.Slice(_entry.RayStart * maxHits, _counts[_entry.RayStart]);
            ReadOnlySpan<AcousticHit> _backward = _hits.Slice((_entry.RayStart + 1) * maxHits, _counts[_entry.RayStart + 1]);
            bool _blocked = _forward.Length > 0 || _backward.Length > 0;

            Vector3 _through = profile.Transmission && _blocked ? ThroughWalls(_forward, _backward, _entry.Distance) : Vector3.one;
            Vector3 _gains = profile.Occlusion ? Occlude(_entry, _counts, _forward, _backward, _through) : _through;
            return ToResult(ApplyAir(_gains, _entry.Distance));
        }

        private Vector3 Occlude(in BatchEntry _entry, ReadOnlySpan<int> _counts, ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, Vector3 _through) {
            bool _blocked = _forward.Length > 0 || _backward.Length > 0;
            // Rays do not hit the collider they start in: a source buried in a collider only shows up in the backward ray.
            bool _enclosed = _forward.Length == 0 && _backward.Length > 0;

            int _open = _blocked ? 0 : 1;
            if (!_enclosed) {
                for (int i = 0; i < _entry.SphereRays; i++) {
                    if (_counts[_entry.RayStart + CenterRays + i] == 0) _open++;
                }
            }

            // Thickness is only known along the center ray. When it is clear, the blocked side rays carry nothing.
            Vector3 _leak = profile.Transmission && _blocked ? _through : Vector3.zero;
            return AcousticMath.Occlude((float)_open / (1 + _entry.SphereRays), _leak);
        }

        private Vector3 ThroughWalls(ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, float _length) {
            Vector3 _gains = Vector3.one;

            for (int f = 0; f < _forward.Length; f++) {
                int _match = IndexOf(_backward, _forward[f].ColliderId);
                float _thickness = _match >= 0
                    ? _length - _backward[_match].Distance - _forward[f].Distance
                    : OneSidedThickness(_length - _forward[f].Distance, _backward.Length);
                _gains = Vector3.Scale(_gains, Transmit(_forward[f].ColliderId, _thickness));
            }

            for (int b = 0; b < _backward.Length; b++) {
                if (IndexOf(_forward, _backward[b].ColliderId) >= 0) continue;
                float _thickness = OneSidedThickness(_length - _backward[b].Distance, _forward.Length);
                _gains = Vector3.Scale(_gains, Transmit(_backward[b].ColliderId, _thickness));
            }

            return _gains;
        }

        // A full opposite list may have cut off the partner hit, so the distance to the endpoint would be meaningless.
        private float OneSidedThickness(float _toEndpoint, int _oppositeHits) {
            return _oppositeHits >= maxHits ? AcousticMaterial.ReferenceThickness : _toEndpoint;
        }

        private Vector3 Transmit(int _colliderId, float _thickness) {
            AcousticMaterial _material = geometry.MaterialOf(_colliderId);
            // No default material in the profile: the wall lets nothing through.
            Vector3 _transmission = _material != null ? _material.Transmission : Vector3.zero;
            return AcousticMath.Transmit(_transmission, Mathf.Max(0f, _thickness));
        }

        private static int IndexOf(ReadOnlySpan<AcousticHit> _hits, int _colliderId) {
            for (int i = 0; i < _hits.Length; i++) {
                if (_hits[i].ColliderId == _colliderId) return i;
            }
            return -1;
        }

        private Vector3 ApplyAir(Vector3 _gains, float _distance) {
            if (!profile.AirAbsorption) return _gains;
            return Vector3.Scale(_gains, AcousticMath.Air(_distance, profile.AirMidDbPerMeter, profile.AirHighDbPerMeter));
        }

        private AcousticResult ToResult(Vector3 _gains) {
            return AcousticMath.ToResult(_gains, profile.FloorDb, profile.MinLowPass, profile.MaxHighPass);
        }
    }
}
