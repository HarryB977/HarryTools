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
            public float Radius;
            public Vector3 Source;
            public Vector3 Listener;
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
        // Colliders holding an endpoint of the voice being computed. A ray list has at most maxHits of them per direction.
        private int[] ignored = Array.Empty<int>();
        private int ignoredCount;

        private Slot[] slots = new Slot[InitialSlots];
        private int[] slotOfQuery = new int[InitialSlots];
        private BatchEntry[] entries = new BatchEntry[InitialSlots];
        private AcousticRay[] rays = new AcousticRay[InitialSlots];
        private int entryCount;
        private int entryCursor;
        private int batch = NoBatch;
        private int stamp;
        private bool disposed;
        private DebugVoice[] debugVoices = Array.Empty<DebugVoice>();

        // ==================== PROPERTIES ===================
        /// <summary> When true, each finished voice keeps what it traced for the scene view gizmos. </summary>
        internal bool CaptureDebug { get; set; }
        internal DebugVoice[] DebugVoices => debugVoices;
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
            if (ignored.Length != maxHits * 2) ignored = new int[maxHits * 2];
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

        // ==================== DEBUG ===================
        /// <summary> True when the slot was asked about in the last Evaluate. </summary>
        internal bool IsLive(int _slot) => _slot < slots.Length && slots[_slot].SeenStamp == stamp;

        private void BeginCapture(in BatchEntry _entry, ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, bool _blocked) {
            if (debugVoices.Length < slots.Length) Array.Resize(ref debugVoices, slots.Length);
            ref DebugVoice _voice = ref debugVoices[_entry.Slot];
            if (_voice.Hits == null || _voice.Hits.Length < maxHits * 2) _voice.Hits = new DebugHit[maxHits * 2];
            if (_voice.Rays == null || _voice.Rays.Length < sphere.Length) _voice.Rays = new DebugRay[sphere.Length];

            _voice.Valid = true;
            _voice.Source = _entry.Source;
            _voice.Listener = _entry.Listener;
            _voice.Radius = _entry.Radius;
            _voice.Blocked = _blocked;
            _voice.RayCount = _entry.SphereRays;
            _voice.HitCount = 0;

            for (int f = 0; f < _forward.Length; f++) {
                if (!IsIgnored(_forward[f].ColliderId)) _voice.Hits[_voice.HitCount++] = new DebugHit { Point = _forward[f].Point, Material = geometry.MaterialOf(_forward[f].ColliderId) };
            }
            for (int b = 0; b < _backward.Length; b++) {
                int _id = _backward[b].ColliderId;
                if (IsIgnored(_id) || IndexOf(_forward, _id) >= 0) continue;
                _voice.Hits[_voice.HitCount++] = new DebugHit { Point = _backward[b].Point, Material = geometry.MaterialOf(_id) };
            }
        }

        private void CaptureRay(int _slot, int _index, Vector3 _from, bool _open) {
            debugVoices[_slot].Rays[_index] = new DebugRay { From = _from, Open = _open };
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
                if (_free < debugVoices.Length) debugVoices[_free].Valid = false;
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
                if (entryCount > 0 && Stopwatch.GetTimestamp() >= _deadline) break;

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
                entries[entryCount++] = new BatchEntry { Slot = _slot, Handle = _query.Handle, RayStart = _rayCount, SphereRays = _sphereRays, Distance = _distance, Radius = _radius, Source = _query.SourcePosition, Listener = _query.ListenerPosition };
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
            for (int i = 0; i < _sphereRays; i++) rays[_at++] = new AcousticRay { From = _source + sphere[SphereIndex(i, _sphereRays)] * _radius, To = _listener };
            return _at;
        }

        // A reduced count walks the whole lattice: a prefix would only sample the top cap.
        private int SphereIndex(int _sample, int _sphereRays) => _sample * sphere.Length / _sphereRays;

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
                if (_read > 0 && Stopwatch.GetTimestamp() >= _deadline) return;
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
            ReadOnlySpan<AcousticHit> _forward = RayHits(_entry.RayStart, _hits, _counts);
            ReadOnlySpan<AcousticHit> _backward = RayHits(_entry.RayStart + 1, _hits, _counts);

            ignoredCount = 0;
            IgnoreContaining(_forward, _backward, _entry.Listener);
            IgnoreContaining(_backward, _forward, _entry.Source);
            bool _blocked = FirstSolid(_forward) >= 0 || FirstSolid(_backward) >= 0;
            if (CaptureDebug) BeginCapture(_entry, _forward, _backward, _blocked);

            Vector3 _through = profile.Transmission && _blocked ? ThroughWalls(_forward, _backward, _entry.Distance) : Vector3.one;
            Vector3 _gains = profile.Occlusion ? Occlude(_entry, _hits, _counts, _forward, _backward, _blocked, _through) : _through;
            AcousticResult _result = ToResult(ApplyAir(_gains, _entry.Distance));
            if (CaptureDebug) debugVoices[_entry.Slot].Result = _result;
            return _result;
        }

        private ReadOnlySpan<AcousticHit> RayHits(int _ray, ReadOnlySpan<AcousticHit> _hits, ReadOnlySpan<int> _counts) {
            return _hits.Slice(_ray * maxHits, _counts[_ray]);
        }

        // Only a hit with no partner from the other side can hold the point where its ray ends, so walls never pay for this query.
        private void IgnoreContaining(ReadOnlySpan<AcousticHit> _hits, ReadOnlySpan<AcousticHit> _opposite, Vector3 _endpoint) {
            for (int i = 0; i < _hits.Length; i++) {
                int _id = _hits[i].ColliderId;
                if (IndexOf(_opposite, _id) < 0 && geometry.ContainsPoint(_id, _endpoint)) ignored[ignoredCount++] = _id;
            }
        }

        private bool IsIgnored(int _colliderId) {
            for (int i = 0; i < ignoredCount; i++) {
                if (ignored[i] == _colliderId) return true;
            }
            return false;
        }

        private int FirstSolid(ReadOnlySpan<AcousticHit> _hits) {
            for (int i = 0; i < _hits.Length; i++) {
                if (!IsIgnored(_hits[i].ColliderId)) return i;
            }
            return -1;
        }

        private Vector3 Occlude(in BatchEntry _entry, ReadOnlySpan<AcousticHit> _hits, ReadOnlySpan<int> _counts, ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, bool _blocked, Vector3 _through) {
            int _firstForward = FirstSolid(_forward);
            // Rays do not hit a surface from behind: a backward hit alone means the side rays cannot see that surface either.
            bool _hidden = _firstForward < 0 && _blocked;
            Vector3 _toListener = (_entry.Listener - _entry.Source) / _entry.Distance;

            int _open = _blocked ? 0 : 1;
            int _closed = _blocked ? 1 : 0;
            Vector3 _leak = _blocked ? _through : Vector3.zero;

            for (int i = 0; i < _entry.SphereRays; i++) {
                // A start point past the first wall would count as open, or start inside the wall where it reports no hit.
                Vector3 _offset = sphere[SphereIndex(i, _entry.SphereRays)] * _entry.Radius;
                float _advance = Vector3.Dot(_offset, _toListener);
                if (_hidden || (_firstForward >= 0 && _advance >= _forward[_firstForward].Distance)) {
                    if (CaptureDebug) CaptureRay(_entry.Slot, i, _entry.Source + _offset, false);
                    _closed++;
                    _leak += _through;
                    continue;
                }

                ReadOnlySpan<AcousticHit> _side = RayHits(_entry.RayStart + CenterRays + i, _hits, _counts);
                bool _clear = FirstSolid(_side) < 0;
                if (CaptureDebug) CaptureRay(_entry.Slot, i, _entry.Source + _offset, _clear);
                if (_clear) {
                    _open++;
                    continue;
                }
                _closed++;
                if (profile.Transmission) _leak += ThroughSideRay(_side, _forward, _backward, _entry.Distance);
            }

            Vector3 _average = profile.Transmission && _closed > 0 ? _leak / _closed : Vector3.zero;
            return AcousticMath.Occlude((float)_open / (_open + _closed), _average);
        }

        private Vector3 ThroughWalls(ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, float _length) {
            Vector3 _gains = Vector3.one;

            for (int f = 0; f < _forward.Length; f++) {
                int _id = _forward[f].ColliderId;
                if (!IsIgnored(_id)) _gains = Vector3.Scale(_gains, Transmit(_id, Thickness(_id, _forward, _backward, _length)));
            }

            for (int b = 0; b < _backward.Length; b++) {
                int _id = _backward[b].ColliderId;
                if (IndexOf(_forward, _id) >= 0 || IsIgnored(_id)) continue;
                _gains = Vector3.Scale(_gains, Transmit(_id, AcousticMaterial.ReferenceThickness));
            }

            return _gains;
        }

        // Side rays go one way only: a wall the center ray measured keeps that thickness, any other surface gets the reference one.
        private Vector3 ThroughSideRay(ReadOnlySpan<AcousticHit> _side, ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, float _length) {
            Vector3 _gains = Vector3.one;
            for (int i = 0; i < _side.Length; i++) {
                int _id = _side[i].ColliderId;
                if (!IsIgnored(_id)) _gains = Vector3.Scale(_gains, Transmit(_id, Thickness(_id, _forward, _backward, _length)));
            }
            return _gains;
        }

        // A one-sided hit that does not hold an endpoint is an open mesh, or its partner fell off a full hit list: its real thickness is unknown.
        private static float Thickness(int _colliderId, ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, float _length) {
            int _entry = IndexOf(_forward, _colliderId);
            int _exit = IndexOf(_backward, _colliderId);
            if (_entry < 0 || _exit < 0) return AcousticMaterial.ReferenceThickness;
            return _length - _backward[_exit].Distance - _forward[_entry].Distance;
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
