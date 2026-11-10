using System;
using Unity.Profiling;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary>
    /// The single module that computes everything that changes volume and filters: occlusion, transmission and air absorption, with the audibility floor applied last.
    /// Rays are scheduled in one Evaluate and read in a later one; until then each voice keeps its last finished result (neutral at first).
    /// Only one batch is in flight, so a batch never goes over the rays of one frame.
    /// With Pathing on, a blocked voice is also heard around obstacles through the <see cref="ProbeField"/>, mixed by energy with the direct sound and heard from where the path reaches the listener.
    /// </summary>
    internal sealed class PropagationModule : IAcousticModule, IDisposable {
        // ==================== TYPES ===================
        private struct Slot {
            public AudioHandle Handle;
            public AcousticResult Result;
            public float NextDue;
            public int SeenStamp;
            public int PickedStamp;
            // Pathing: corner probe the path starts from, open fraction of the last read, last valid path and the smoothed perceived position.
            public int SourceProbe;
            public float LastOpen;
            public Vector3 Anchor;
            public float Length;
            public bool Valid;
            public float Share;
            public Vector3 Perceived;
            public bool HasPerceived;
            // Probes of the last walk that succeeded, kept in lastWalks.
            public int WalkCount;
            // Band energies of the last read, so the loudness follows where the voice is heard from on every tick.
            public Vector3 DirectEnergy;
            public Vector3 PathEnergy;
            public float DirectRoll;
            public bool HasEnergy;
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
            public AudioAsset Asset;
            public PathRays Path;
            // Where its corner and candidate probes start in pathProbes.
            public int PathFirst;
            public int CornerCount;
            public int CandidateCount;
        }

        private enum PathRays : byte {
            // No path wanted: no probe field, an open line or no finished search. The voice uses direct only.
            None,
            // Wanted, but the source has no reached probe around it.
            NoProbes,
            // Wanted, but the rays did not fit this frame: the last valid path is kept.
            Kept,
            Traced
        }

        // ==================== VARIABLES ===================
        private const int InitialSlots = 16;
        private const int NoBatch = -1;
        // Source to listener and listener to source, the pair that measures thickness.
        private const int CenterRays = 2;
        private const float MinDistance = 0.001f;
        // Pathing: rays from the source to the corners of its cell, and from the listener to the last probes of the walk.
        private const int Corners = 8;
        private const int Candidates = 8;
        private const int PathProbesPerEntry = Corners + Candidates;
        private const int MaxWalkSteps = 256;
        private const float MinShare = 0.01f;
        private const float PerceivedSnapSqr = 0.05f * 0.05f;
        private static readonly float DefaultRadius = new AcousticSourceExtension().Radius;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Acoustics.Propagation");

        private readonly IAcousticGeometry geometry;
        private readonly RayBudget budget;
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

        private ProbeField field;
        private int[] pathProbes = new int[InitialSlots * PathProbesPerEntry];
        private int[] pathGenerations = new int[InitialSlots * PathProbesPerEntry];
        private readonly int[] walk = new int[MaxWalkSteps + 1];
        private int[] lastWalks = new int[InitialSlots * (MaxWalkSteps + 1)];
        private int[] lastWalkGenerations = new int[InitialSlots * (MaxWalkSteps + 1)];
        private int pathRaysScheduled;
        private DebugVoice[] debugVoices = Array.Empty<DebugVoice>();

        // ==================== PROPERTIES ===================
        /// <summary> When true, each finished voice keeps what it traced for the scene view gizmos. </summary>
        internal bool CaptureDebug { get; set; }
        internal DebugVoice[] DebugVoices => debugVoices;
        /// <summary> The probe region of Pathing, null when it is off. </summary>
        internal ProbeField Field => field;
        /// <summary> Path rays scheduled since creation, for tests and diagnostics. </summary>
        internal int PathRaysScheduled => pathRaysScheduled;
        public int Order => 0;
        // Each voice is throttled by the UpdateInterval of the profile here, so one late voice never holds the others back.
        public float UpdateInterval => 0f;

        // ==================== INITIALIZATION ===================
        public PropagationModule(AcousticProfile _profile, IAcousticGeometry _geometry, RayBudget _budget) {
            geometry = _geometry;
            budget = _budget;
            Configure(_profile);
        }

        /// <summary> Switches profile. A batch in flight is dropped; voices keep their last result and are evaluated again right away. The geometry and the budget belong to <see cref="Acoustics"/>, which configures them. </summary>
        public void Configure(AcousticProfile _profile) {
            profile = _profile;
            DropBatch();

            maxHits = Mathf.Max(1, _profile.MaxHitsPerRay);
            if (ignored.Length != maxHits * 2) ignored = new int[maxHits * 2];
            sphere = BuildSphere(Mathf.Max(0, _profile.OcclusionRays));
            for (int i = 0; i < slots.Length; i++) {
                slots[i].NextDue = 0f;
                // Probe ids of the old graph mean nothing in the new one.
                slots[i].SourceProbe = -1;
                slots[i].WalkCount = 0;
            }

            // With neither occlusion nor transmission nothing is ever blocked, so no voice could use a path.
            if (_profile.Pathing && (_profile.Occlusion || _profile.Transmission)) {
                if (field == null) field = new ProbeField(geometry, budget);
                field.Configure(_profile);
            } else if (field != null) {
                field.Dispose();
                field = null;
                for (int i = 0; i < slots.Length; i++) ForgetPath(ref slots[i]);
            }

            if ((_profile.Occlusion || _profile.Transmission) && _profile.RayBudgetPerFrame < CenterRays) {
                HarryTools.LoggerTool.Logger.LogWarning("Audio", "Acoustic profile '" + _profile.name + "' has a ray budget below " + CenterRays + ": nothing can be traced and voices stay neutral.");
            }
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            DropBatch();
            field?.Dispose();
            field = null;
        }

        // ==================== EVALUATION ===================
        public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
            if (disposed) return;

            try {
                using (Marker.Auto()) {
                    long _deadline = budget.BeginWork();
                    EnsureCapacity(_queries.Length);
                    AssignSlots(_queries);
                    if (!budget.ShouldYield()) {
                        ReadBatch(_deadline);
                        if (batch == NoBatch) ScheduleDue(_queries, _deadline);
                        if (field != null && _queries.Length > 0) field.Tick(_queries[0].ListenerPosition, this, _deadline);
                    }
                    WriteResults(_queries, _results);
                    budget.EndWork();
                }
            } catch {
                // The core unregisters a module that throws and has no callback, so the cleanup happens here.
                Acoustics.OnModuleFailed(this);
                throw;
            }
        }

        // ==================== DEBUG ===================
        /// <summary> True when the slot was asked about in the last Evaluate. </summary>
        internal bool IsLive(int _slot) => _slot < slots.Length && slots[_slot].SeenStamp == stamp;

        /// <summary> Path of a live voice for the gizmos: its anchor, the position it is heard from and how many probes its last walk has. </summary>
        internal bool TryGetPathDebug(int _slot, out Vector3 _anchor, out Vector3 _perceived, out bool _hasPerceived, out int _walkCount) {
            _anchor = default;
            _perceived = default;
            _hasPerceived = false;
            _walkCount = 0;
            if (field == null || !IsLive(_slot) || !slots[_slot].Valid) return false;

            _anchor = slots[_slot].Anchor;
            _perceived = slots[_slot].Perceived;
            _hasPerceived = slots[_slot].HasPerceived;
            _walkCount = slots[_slot].WalkCount;
            return true;
        }

        /// <summary> Position of a probe of the last walk of a voice, false when that probe was recycled since. </summary>
        internal bool TryGetWalkPoint(int _slot, int _index, out Vector3 _position) {
            _position = default;
            if (field == null || !IsLive(_slot) || _index < 0 || _index >= slots[_slot].WalkCount) return false;

            int _at = _slot * (MaxWalkSteps + 1) + _index;
            int _probe = lastWalks[_at];
            if (!field.Graph.IsAlive(_probe) || field.Graph.GenerationOf(_probe) != lastWalkGenerations[_at]) return false;
            _position = field.PositionOf(_probe);
            return true;
        }

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
                slots[_free] = new Slot { Handle = _queries[i].Handle, Result = AcousticResult.Neutral, SeenStamp = stamp, SourceProbe = -1, LastOpen = 1f };
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
            Array.Resize(ref pathProbes, _queries * PathProbesPerEntry);
            Array.Resize(ref pathGenerations, _queries * PathProbesPerEntry);
            Array.Resize(ref lastWalks, _queries * (MaxWalkSteps + 1));
            Array.Resize(ref lastWalkGenerations, _queries * (MaxWalkSteps + 1));
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
                if (!budget.MayWork(this, _deadline, entryCount == 0)) break;

                int _slot = slotOfQuery[_next];
                AcousticQuery _query = _queries[_next];
                AcousticSourceExtension _extension = _query.Asset != null ? _query.Asset.GetExtension<AcousticSourceExtension>() : null;
                float _distance = Vector3.Distance(_query.SourcePosition, _query.ListenerPosition);

                // The negated comparison also catches NaN positions.
                if ((_extension != null && _extension.Exclude) || !(_distance > MinDistance) || float.IsInfinity(_distance)) {
                    Finish(_slot, AcousticResult.Neutral, _now, _interval);
                    continue;
                }

                if (!profile.Occlusion && !profile.Transmission) {
                    Finish(_slot, ToResult(ApplyAir(Vector3.one, _distance)), _now, _interval);
                    continue;
                }

                int _sphereRays = profile.Occlusion ? sphere.Length : 0;
                int _available = budget.Remaining;
                if (CenterRays + _sphereRays > _available) {
                    if (entryCount > 0 || _available < CenterRays) break;
                    _sphereRays = _available - CenterRays;
                }

                int _cornerCount = 0;
                int _candidateCount = 0;
                PathRays _path = WantsPath(slots[_slot], _query) ? CollectPathProbes(entryCount, _query.SourcePosition, out _cornerCount, out _candidateCount) : PathRays.None;
                // Path rays go only when everything fits; otherwise the last valid path is kept.
                if (_path == PathRays.Traced && CenterRays + _sphereRays + _cornerCount + _candidateCount > _available) {
                    _path = PathRays.Kept;
                    _cornerCount = 0;
                    _candidateCount = 0;
                }

                int _pathRays = _cornerCount + _candidateCount;
                budget.TryConsume(CenterRays + _sphereRays + _pathRays);
                pathRaysScheduled += _pathRays;
                float _radius = _extension != null ? Mathf.Max(0f, _extension.Radius) : DefaultRadius;
                EnsureRays(_rayCount + CenterRays + _sphereRays + _pathRays);
                entries[entryCount] = new BatchEntry {
                    Slot = _slot, Handle = _query.Handle, RayStart = _rayCount, SphereRays = _sphereRays, Distance = _distance, Radius = _radius,
                    Source = _query.SourcePosition, Listener = _query.ListenerPosition, Asset = _query.Asset,
                    Path = _path, PathFirst = entryCount * PathProbesPerEntry, CornerCount = _cornerCount, CandidateCount = _candidateCount
                };
                _rayCount = WriteRays(_query.SourcePosition, _query.ListenerPosition, _radius, _sphereRays, _rayCount);
                _rayCount = WritePathRays(entryCount, _query.SourcePosition, _query.ListenerPosition, _rayCount);
                entryCount++;

                slots[_slot].PickedStamp = stamp;
                slots[_slot].NextDue = _now + _interval;
            }

            if (entryCount > 0) batch = geometry.Schedule(new ReadOnlySpan<AcousticRay>(rays, 0, _rayCount), maxHits);
        }

        private void Finish(int _slot, in AcousticResult _result, float _now, float _interval) {
            slots[_slot].Result = _result;
            slots[_slot].Share = 0f;
            slots[_slot].Valid = false;
            slots[_slot].HasEnergy = false;
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

        // ==================== PATH RAYS ===================
        private bool WantsPath(in Slot _slot, in AcousticQuery _query) {
            return field != null && _query.Asset != null && _slot.LastOpen < profile.PathMaxOpen && field.Search.HasResult;
        }

        // The corners of the source cell that the search reached, and the last probes of the walk from the best of them, which is where the path reaches the listener.
        // Which corner the source sees is only known when the rays come back, so the walk here starts from the cheapest one.
        private PathRays CollectPathProbes(int _entry, Vector3 _source, out int _cornerCount, out int _candidateCount) {
            int _first = _entry * PathProbesPerEntry;
            _cornerCount = 0;
            _candidateCount = 0;

            Vector3Int _base = field.CoordOf(_source);
            int _cheapest = -1;
            float _cheapestCost = float.PositiveInfinity;
            for (int i = 0; i < Corners; i++) {
                int _probe = field.Graph.Find(_base + CornerOffset(i));
                if (!HasDistance(_probe)) continue;
                pathProbes[_first + _cornerCount] = _probe;
                pathGenerations[_first + _cornerCount] = field.Graph.GenerationOf(_probe);
                _cornerCount++;

                float _cost = Vector3.Distance(_source, field.PositionOf(_probe)) + field.Search.DistanceOf(_probe);
                if (_cost >= _cheapestCost) continue;
                _cheapest = _probe;
                _cheapestCost = _cost;
            }
            if (_cornerCount == 0) return PathRays.NoProbes;

            int _steps = Walk(_cheapest, false);
            for (int i = Mathf.Max(0, _steps - Candidates); i < _steps; i++) {
                int _probe = walk[i];
                pathProbes[_first + Corners + _candidateCount] = _probe;
                pathGenerations[_first + Corners + _candidateCount] = field.Graph.GenerationOf(_probe);
                _candidateCount++;
            }
            return PathRays.Traced;
        }

        private int WritePathRays(int _entry, Vector3 _source, Vector3 _listener, int _at) {
            int _first = entries[_entry].PathFirst;
            for (int i = 0; i < entries[_entry].CornerCount; i++) rays[_at++] = new AcousticRay { From = _source, To = field.PositionOf(pathProbes[_first + i]) };
            for (int i = 0; i < entries[_entry].CandidateCount; i++) rays[_at++] = new AcousticRay { From = _listener, To = field.PositionOf(pathProbes[_first + Corners + i]) };
            return _at;
        }

        // ==================== READING ===================
        private void ReadBatch(long _deadline) {
            if (batch == NoBatch) return;
            if (!geometry.TryGetResults(batch, out ReadOnlySpan<AcousticHit> _hits, out ReadOnlySpan<int> _counts)) return;

            // What does not fit in the time of this frame is read in the next; the hits stay valid until Release.
            int _read = 0;
            while (entryCursor < entryCount) {
                if (!budget.MayWork(this, _deadline, _read == 0)) return;
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
            float _open = _blocked ? 0f : 1f;
            Vector3 _gains = profile.Occlusion ? Occlude(_entry, _hits, _counts, _forward, _backward, _blocked, _through, out _open) : _through;
            _gains = ApplyAir(_gains, _entry.Distance);
            // A voice still heard away from its source after Pathing was turned off keeps the mix until it is back.
            AcousticResult _result = field != null || slots[_entry.Slot].HasPerceived ? Mix(_entry, _hits, _counts, _gains, _open) : ToResult(_gains);
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

        private Vector3 Occlude(in BatchEntry _entry, ReadOnlySpan<AcousticHit> _hits, ReadOnlySpan<int> _counts, ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, bool _blocked, Vector3 _through, out float _openFraction) {
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
            _openFraction = (float)_open / (_open + _closed);
            return AcousticMath.Occlude(_openFraction, _average);
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
        private float Thickness(int _colliderId, ReadOnlySpan<AcousticHit> _forward, ReadOnlySpan<AcousticHit> _backward, float _length) {
            int _entry = IndexOf(_forward, _colliderId);
            int _exit = IndexOf(_backward, _colliderId);
            if (_entry < 0 || _exit < 0) return AcousticMaterial.ReferenceThickness;
            return Mathf.Min(_length - _backward[_exit].Distance - _forward[_entry].Distance, profile.MaxWallThickness);
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

        // ==================== PATHING ===================
        // Direct and path energies summed per band, heard from between the source and the anchor in proportion to the path's share. The floor applies last.
        // The energies are kept so WriteResults can compensate for where the voice is really heard from while the perceived position moves.
        private AcousticResult Mix(in BatchEntry _entry, ReadOnlySpan<AcousticHit> _hits, ReadOnlySpan<int> _counts, Vector3 _gains, float _open) {
            ref Slot _slot = ref slots[_entry.Slot];
            _slot.LastOpen = _open;
            if (_entry.Path == PathRays.Traced) ReadPath(_entry, _hits, _counts, ref _slot);
            else if (_entry.Path != PathRays.Kept || _open >= profile.PathMaxOpen) _slot.Valid = false;

            _slot.Share = 0f;
            _slot.HasEnergy = false;
            if (_entry.Asset == null) return ToResult(_gains);

            SpatialSettings _spatial = _entry.Asset.GetSpatial();
            float _directRoll = _spatial.GainAt(_entry.Distance);
            Vector3 _direct = _gains * (_directRoll * _directRoll);
            Vector3 _path = Vector3.zero;
            if (_slot.Valid) {
                float _pathRoll = _spatial.GainAt(_slot.Length);
                Vector3 _bend = PathMath.BendLoss(_slot.Length - _entry.Distance, profile.PathMidDbPerMeter, profile.PathHighDbPerMeter);
                _path = Vector3.Scale(_bend, ApplyAir(Vector3.one, _slot.Length)) * (PathMath.PathWeight(_open, profile.PathMaxOpen) * _pathRoll * _pathRoll);
            }

            float _share = PathMath.PathShare(_direct.y, _path.y);
            _slot.Share = _share > 0f ? _share : 0f;
            _slot.DirectEnergy = _direct;
            _slot.PathEnergy = _path;
            _slot.DirectRoll = _directRoll;
            _slot.HasEnergy = true;
            // Nothing heard away from the source: the same result as with no path at all.
            if (!(_share > 0f) && !_slot.HasPerceived) return ToResult(_gains);

            Vector3 _target = Vector3.Lerp(_entry.Source, _slot.Anchor, _slot.Share);
            return MixedResult(_slot, _spatial.GainAt(Vector3.Distance(_target, _entry.Listener)));
        }

        // Never louder than the open sound at the real distance, compensated for the roll-off of where the voice is heard from, with the floor relative to no acoustics.
        private AcousticResult MixedResult(in Slot _slot, float _heardRoll) {
            float _open = _slot.DirectRoll * _slot.DirectRoll;
            Vector3 _sum = Vector3.Min(_slot.DirectEnergy + _slot.PathEnergy, new Vector3(_open, _open, _open));
            Vector3 _output = PathMath.Output(_sum, Vector3.zero, _heardRoll);
            return AcousticMath.ToResult(_output, PathMath.FloorDb(profile.FloorDb, _slot.DirectRoll, _heardRoll), profile.MinLowPass, profile.MaxHighPass);
        }

        // Source probe: the visible corner with the cheapest way to the listener. Anchor: the visible candidate on its walk farthest from the listener, or the root.
        // A walk that breaks keeps the last path; a source that sees no reached corner has none.
        private void ReadPath(in BatchEntry _entry, ReadOnlySpan<AcousticHit> _hits, ReadOnlySpan<int> _counts, ref Slot _slot) {
            int _first = _entry.PathFirst;
            int _cornerRay = _entry.RayStart + CenterRays + _entry.SphereRays;
            int _candidateRay = _cornerRay + _entry.CornerCount;

            int _source = -1;
            float _bestCost = float.PositiveInfinity;
            for (int i = 0; i < _entry.CornerCount; i++) {
                int _probe = pathProbes[_first + i];
                if (_counts[_cornerRay + i] > 0 || !IsSame(_probe, pathGenerations[_first + i])) continue;
                float _cost = Vector3.Distance(_entry.Source, field.PositionOf(_probe)) + field.Search.DistanceOf(_probe);
                if (_cost >= _bestCost) continue;
                _source = _probe;
                _bestCost = _cost;
            }
            if (_source < 0) {
                _slot.Valid = false;
                return;
            }

            CheckLastWalk(_entry.Slot, ref _slot);
            int _steps = Walk(_source, true);
            if (_steps <= 0) return;
            KeepWalk(_entry.Slot, ref _slot, _steps);

            int _anchor = walk[_steps - 1];
            float _anchorDistance = field.Search.DistanceOf(_anchor);
            for (int i = 0; i < _entry.CandidateCount; i++) {
                int _probe = pathProbes[_first + Corners + i];
                if (_counts[_candidateRay + i] > 0 || !IsSame(_probe, pathGenerations[_first + Corners + i]) || !IsOnWalk(_probe, _steps)) continue;
                float _distance = field.Search.DistanceOf(_probe);
                if (_distance <= _anchorDistance) continue;
                _anchor = _probe;
                _anchorDistance = _distance;
            }

            Vector3 _sourcePosition = field.PositionOf(_source);
            Vector3 _anchorPosition = field.PositionOf(_anchor);
            _slot.SourceProbe = _source;
            _slot.Anchor = _anchorPosition;
            _slot.Length = Vector3.Distance(_entry.Source, _sourcePosition) + field.Search.DistanceOf(_source) - _anchorDistance + Vector3.Distance(_anchorPosition, _entry.Listener);
            _slot.Valid = true;
        }

        // Fills walk with the probes from _start to the root of the search. Returns how many, or -1 when a parent is stale, a connection is blocked or the walk is too long.
        // When _mark is true every connection walked is asked to be checked again, so a door that closes on a path is noticed.
        private int Walk(int _start, bool _mark) {
            int _count = 0;
            int _probe = _start;
            while (true) {
                if (!HasDistance(_probe) || _count > MaxWalkSteps) return -1;
                walk[_count++] = _probe;
                int _parent = field.Search.ParentOf(_probe);
                if (_parent == ProbeSearch.NoParent) return _count;

                int _direction = DirectionTo(_probe, _parent);
                if (_direction < 0) return -1;
                if (field.Graph.GetEdge(_probe, _direction) == EdgeState.Blocked) {
                    if (_mark) {
                        MarkAround(_probe);
                        MarkAround(_parent);
                    }
                    return -1;
                }
                if (_mark) field.MarkInUse(_probe, _direction);
                _probe = _parent;
            }
        }

        // A connection on the path closed, so the geometry changed there. The open connections next to it cross the same opening and are just as stale:
        // checking them all now takes one new search instead of one per connection the next path would try.
        private void MarkAround(int _probe) {
            Vector3Int _center = field.Graph.CoordOf(_probe);
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    for (int z = -1; z <= 1; z++) {
                        int _near = field.Graph.Find(_center + new Vector3Int(x, y, z));
                        if (_near < 0) continue;
                        for (int d = 0; d < ProbeGraph.DirectionCount; d++) field.MarkInUse(_near, d);
                    }
                }
            }
        }

        // A new search can route around a connection that closed before any walk sees it blocked, so the last path is checked too.
        private void CheckLastWalk(int _slotIndex, ref Slot _slot) {
            int _first = _slotIndex * (MaxWalkSteps + 1);
            for (int i = 0; i + 1 < _slot.WalkCount; i++) {
                int _probe = lastWalks[_first + i];
                int _next = lastWalks[_first + i + 1];
                if (!field.Graph.IsAlive(_probe) || field.Graph.GenerationOf(_probe) != lastWalkGenerations[_first + i]) continue;
                if (!field.Graph.IsAlive(_next) || field.Graph.GenerationOf(_next) != lastWalkGenerations[_first + i + 1]) continue;
                int _direction = DirectionTo(_probe, _next);
                if (_direction < 0 || field.Graph.GetEdge(_probe, _direction) != EdgeState.Blocked) continue;
                MarkAround(_probe);
                MarkAround(_next);
                _slot.WalkCount = 0;
                return;
            }
        }

        private void KeepWalk(int _slotIndex, ref Slot _slot, int _steps) {
            int _first = _slotIndex * (MaxWalkSteps + 1);
            for (int i = 0; i < _steps; i++) {
                lastWalks[_first + i] = walk[i];
                lastWalkGenerations[_first + i] = field.Graph.GenerationOf(walk[i]);
            }
            _slot.WalkCount = _steps;
        }

        private int DirectionTo(int _probe, int _neighbor) {
            Vector3Int _step = field.Graph.CoordOf(_neighbor) - field.Graph.CoordOf(_probe);
            for (int d = 0; d < ProbeGraph.DirectionCount; d++) {
                if (ProbeGraph.Step(d) == _step && field.Graph.Neighbor(_probe, d) == _neighbor) return d;
            }
            return -1;
        }

        private bool IsOnWalk(int _probe, int _steps) {
            for (int i = 0; i < _steps; i++) {
                if (walk[i] == _probe) return true;
            }
            return false;
        }

        // The search result belongs to the probe that lives in that slot now.
        private bool HasDistance(int _probe) {
            ProbeGraph _graph = field.Graph;
            return _graph.IsAlive(_probe) && field.Search.GenerationOf(_probe) == _graph.GenerationOf(_probe) && !float.IsPositiveInfinity(field.Search.DistanceOf(_probe));
        }

        private bool IsSame(int _probe, int _generation) => HasDistance(_probe) && field.Graph.GenerationOf(_probe) == _generation;

        private static Vector3Int CornerOffset(int _index) {
            return new Vector3Int(_index & 1, (_index >> 1) & 1, (_index >> 2) & 1);
        }

        // The perceived position is kept, so it still returns to the source smoothly.
        private static void ForgetPath(ref Slot _slot) {
            _slot.Valid = false;
            _slot.Share = 0f;
            _slot.PathEnergy = Vector3.zero;
            _slot.SourceProbe = -1;
            _slot.WalkCount = 0;
        }

        // ==================== RESULTS ===================
        // The core applies the perceived position as is, so it moves here at PerceivedSpeed: towards the doorway when the path takes over, back to the source along the same way.
        private void WriteResults(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
            float _step = Mathf.Max(0f, profile.PerceivedSpeed) * Time.unscaledDeltaTime;
            for (int i = 0; i < _queries.Length; i++) {
                ref Slot _slot = ref slots[slotOfQuery[i]];
                AcousticResult _result = _slot.Result;
                if (field != null || _slot.HasPerceived) {
                    Vector3 _source = _queries[i].SourcePosition;
                    if (!_slot.HasPerceived) _slot.Perceived = _source;
                    Vector3 _target = _slot.Share > 0f ? Vector3.Lerp(_source, _slot.Anchor, _slot.Share) : _source;
                    _slot.Perceived = Vector3.MoveTowards(_slot.Perceived, _target, _step);
                    _slot.HasPerceived = _slot.Share >= MinShare || (_slot.Perceived - _source).sqrMagnitude >= PerceivedSnapSqr;
                    AudioAsset _asset = _queries[i].Asset;
                    if (_slot.HasEnergy && _asset != null && (_slot.HasPerceived || _slot.Share > 0f)) {
                        Vector3 _heard = _slot.HasPerceived ? _slot.Perceived : _source;
                        _result = MixedResult(_slot, _asset.GetSpatial().GainAt(Vector3.Distance(_heard, _queries[i].ListenerPosition)));
                    }
                    _result.HasPerceivedPosition = _slot.HasPerceived;
                    _result.PerceivedPosition = _slot.HasPerceived ? _slot.Perceived : Vector3.zero;
                }
                _results[i] = _result;
            }
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
