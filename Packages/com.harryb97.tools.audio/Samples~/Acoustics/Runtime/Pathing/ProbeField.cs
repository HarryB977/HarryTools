using System;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary>
    /// Region of sound probes that floods the free space around the listener with batched rays, and keeps a shortest path tree from the listener over it.
    /// One batch is in flight at a time. A connection is open when both directions of one of its three lines report no hit.
    /// </summary>
    internal sealed class ProbeField : IDisposable {
        // ==================== TYPES ===================
        private struct BatchEdge {
            public int Key;
            public int Generation;
            public int Line;
            public int Stage;
            public int Ring;
        }

        private struct BatchRoot {
            public int Probe;
            public int Generation;
        }

        private sealed class IntRing {
            private readonly int[] items;
            private int head;
            private int count;

            public IntRing(int _capacity) {
                items = new int[_capacity];
            }

            public void Push(int _value) {
                if (count == items.Length) return;
                items[(head + count) % items.Length] = _value;
                count++;
            }

            public bool TryPop(out int _value) {
                _value = 0;
                if (count == 0) return false;
                _value = items[head];
                head = (head + 1) % items.Length;
                count--;
                return true;
            }
        }

        // ==================== VARIABLES ===================
        private const int NoBatch = -1;
        private const int Corners = 8;
        private const int RaysPerLine = 2;
        private const int LineCount = 3;
        private const int MinRaysToSchedule = 10;
        private const int SearchChunk = 64;
        private const int CursorDivisor = 60;
        private const int MinCursorSteps = 16;
        private const float MinSpacing = 0.05f;
        private const float SearchMoveSqr = 0.1f * 0.1f;
        private const float SearchInterval = 0.5f;
        // Seconds to check every watched collider, and the least time between two checks of a connection in use.
        private const float WatchPeriod = 0.25f;
        private const float InUsePeriod = 0.25f;
        // Colliders kept per connection that made one of its rays fail. A new one past this replaces the oldest.
        private const int MaxBlockers = 3;

        // What the two rays of a batch edge trace: the line itself, or the way from a solid end's entry point to the line.
        private const int StageLine = 0;
        private const int StageNearEntry = 1;
        private const int StageFarEntry = 2;
        private const float SamePointSqr = 1e-6f;

        private const int RingInUse = 0;
        private const int RingInvalidated = 1;
        private const int RingFlood = 2;
        private const int RingCount = 3;

        private static readonly Vector3[] LineAxis = BuildLineAxes();

        private readonly IAcousticGeometry geometry;
        private readonly RayBudget budget;
        private readonly ColliderWatch watch;

        private ProbeGraph graph;
        private ProbeSearch search;
        private IntRing[] rings;
        private bool[] queued;
        private byte[] nextLine;
        private byte[] nextStage;
        private bool[] reached;
        // One bit per direction: the connection is blocked and its far end has no probe to hold that state.
        private ushort[] closedToAbsent;
        // Collider a probe was found inside of; 0 for a probe in free space. Instance ids are never 0.
        private int[] solidCollider;
        // The same for the far end of a connection being traced whose probe does not exist yet.
        private int[] pendingSolid;
        // Free point a probe was entered from, on its own side of whatever it is embedded in.
        private Vector3[] entryPoint;
        private bool[] hasEntry;
        // Up to MaxBlockers colliders per connection, oldest first, hit by any ray of it that failed.
        private int[] blockers;
        private byte[] blockerCount;
        // Time a connection was last resolved, for MarkInUse.
        private float[] lastTraced;
        private AcousticRay[] rays = Array.Empty<AcousticRay>();
        private BatchEdge[] entries = Array.Empty<BatchEdge>();
        private readonly BatchRoot[] roots = new BatchRoot[Corners];

        private float spacing;
        private float radiusSqr;
        private float verticalExtent;
        private int pathRays;
        private int cursor;
        private int cursorSteps;

        private int batch = NoBatch;
        private int entryCount;
        private int rootCount;
        private Vector3 rootListener;

        private Vector3 listener;
        private Vector3 lastSearchListener;
        private float lastSearchTime;
        private bool searched;
        private bool edgesChanged;
        private bool searchRunning;
        private bool disposed;

        // ==================== PROPERTIES ===================
        public ProbeGraph Graph => graph;
        public ProbeSearch Search => search;
        public float Spacing => spacing;

        // ==================== INITIALIZATION ===================
        public ProbeField(IAcousticGeometry _geometry, RayBudget _budget) {
            geometry = _geometry;
            budget = _budget;
            watch = new ColliderWatch(_geometry);
        }

        /// <summary> Applies the profile. Drops the batch in flight and empties the graph and the search. </summary>
        public void Configure(AcousticProfile _profile) {
            DropBatch();

            spacing = Mathf.Max(MinSpacing, _profile.ProbeSpacing);
            radiusSqr = _profile.ProbeRadius * _profile.ProbeRadius;
            verticalExtent = _profile.ProbeVerticalExtent;
            pathRays = Mathf.Max(0, _profile.PathRaysPerFrame);

            graph = new ProbeGraph(_profile.MaxProbes);
            search = new ProbeSearch(graph.Capacity);

            int _keys = graph.Capacity * ProbeGraph.DirectionCount;
            rings = new IntRing[RingCount];
            for (int r = 0; r < RingCount; r++) rings[r] = new IntRing(_keys);
            queued = new bool[_keys];
            nextLine = new byte[_keys];
            nextStage = new byte[_keys];
            reached = new bool[graph.Capacity];
            closedToAbsent = new ushort[graph.Capacity];
            solidCollider = new int[graph.Capacity];
            pendingSolid = new int[_keys];
            entryPoint = new Vector3[graph.Capacity];
            hasEntry = new bool[graph.Capacity];
            blockers = new int[_keys * MaxBlockers];
            blockerCount = new byte[_keys];
            lastTraced = new float[_keys];
            for (int i = 0; i < _keys; i++) lastTraced[i] = float.NegativeInfinity;
            watch.Clear();
            rays = new AcousticRay[Mathf.Max(1, pathRays)];
            entries = new BatchEdge[pathRays / RaysPerLine + 1];

            cursor = 0;
            cursorSteps = Mathf.Max(MinCursorSteps, graph.Capacity / CursorDivisor);
            searched = false;
            edgesChanged = false;
            searchRunning = false;
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            DropBatch();
        }

        // ==================== PUBLIC API ===================
        /// <summary> Reads the finished batch, advances the search and schedules more rays, all within the budget. </summary>
        public void Tick(Vector3 _listener, object _owner, long _deadline) {
            if (disposed || graph == null || !IsFinite(_listener)) return;

            listener = _listener;
            ReadBatch();
            WatchColliders();
            AdvanceSearch(_owner, _deadline);
            SweepRegion();
            SeedCorners();
            if (batch == NoBatch) ScheduleBatch();
        }

        public Vector3 PositionOf(int _probe) => CoordToPosition(graph.CoordOf(_probe));

        /// <summary> The cell whose center is at or below the position on each axis. </summary>
        public Vector3Int CoordOf(Vector3 _position) {
            return new Vector3Int(
                Mathf.FloorToInt(_position.x / spacing - 0.5f),
                Mathf.FloorToInt(_position.y / spacing - 0.5f),
                Mathf.FloorToInt(_position.z / spacing - 0.5f));
        }

        /// <summary> Asks for a connection used by a path to be traced again, unless it was checked less than 0.25 s ago. Its state stays until the new result. </summary>
        public void MarkInUse(int _probe, int _direction) {
            if (disposed || graph == null || _direction < 0 || _direction >= ProbeGraph.DirectionCount) return;
            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor < 0) return;
            int _key = CanonicalKey(_probe, _direction, _neighbor);
            if (Time.unscaledTime - lastTraced[_key] < InUsePeriod) return;
            Enqueue(RingInUse, _key);
        }

        // ==================== READING ===================
        private void ReadBatch() {
            if (batch == NoBatch) return;
            if (!geometry.TryGetResults(batch, out ReadOnlySpan<AcousticHit> _hits, out ReadOnlySpan<int> _counts)) return;

            for (int i = 0; i < entryCount; i++) {
                int _ray = rootCount + i * RaysPerLine;
                int _forward = _counts[_ray] > 0 ? _hits[_ray].ColliderId : 0;
                int _backward = _counts[_ray + 1] > 0 ? _hits[_ray + 1].ColliderId : 0;
                ResolveEdge(entries[i], _counts[_ray] > 0, _counts[_ray + 1] > 0, _forward, _backward);
            }
            if (rootCount > 0) StartSearch(_counts);
            DropBatch();
        }

        private void ResolveEdge(in BatchEdge _entry, bool _forwardHit, bool _backwardHit, int _forwardCollider, int _backwardCollider) {
            int _probe = _entry.Key / ProbeGraph.DirectionCount;
            int _direction = _entry.Key % ProbeGraph.DirectionCount;

            if (!graph.IsAlive(_probe) || graph.GenerationOf(_probe) != _entry.Generation) {
                Forget(_entry.Key);
                return;
            }

            if (_forwardHit) AddBlocker(_entry.Key, _forwardCollider);
            if (_backwardHit) AddBlocker(_entry.Key, _backwardCollider);

            if (_entry.Stage != StageLine) {
                ResolveEntryCheck(_entry, _probe, _direction, !_forwardHit && !_backwardHit);
                return;
            }

            if (_entry.Line == 0 && _forwardHit != _backwardHit) NoteEndInsideSolid(_entry.Key, _probe, _direction, _forwardHit, _forwardHit ? _forwardCollider : _backwardCollider);

            bool _open = !_forwardHit && !_backwardHit && IsLineTrusted(_entry.Key, _probe, _direction, _entry.Line);
            if (_open) {
                int _stage = NextEntryCheck(_entry.Key, _probe, _direction, _entry.Line, StageLine);
                if (_stage != StageLine) Requeue(_entry, _entry.Line, _stage);
                else FinishEdge(_entry, _probe, _direction, true);
            } else {
                TryNextLine(_entry, _probe, _direction);
            }
        }

        private void ResolveEntryCheck(in BatchEdge _entry, int _probe, int _direction, bool _clear) {
            if (!_clear) {
                TryNextLine(_entry, _probe, _direction);
                return;
            }
            int _stage = NextEntryCheck(_entry.Key, _probe, _direction, _entry.Line, _entry.Stage);
            if (_stage != StageLine) Requeue(_entry, _entry.Line, _stage);
            else FinishEdge(_entry, _probe, _direction, true);
        }

        private void TryNextLine(in BatchEdge _entry, int _probe, int _direction) {
            if (_entry.Line + 1 < LineCount) Requeue(_entry, _entry.Line + 1, StageLine);
            else FinishEdge(_entry, _probe, _direction, false);
        }

        private void Requeue(in BatchEdge _entry, int _line, int _stage) {
            nextLine[_entry.Key] = (byte)_line;
            nextStage[_entry.Key] = (byte)_stage;
            rings[_entry.Ring].Push(_entry.Key);
        }

        private void FinishEdge(in BatchEdge _entry, int _probe, int _direction, bool _open) {
            int _farCollider = pendingSolid[_entry.Key];
            Forget(_entry.Key);
            lastTraced[_entry.Key] = Time.unscaledTime;
            if (_open) MarkOpen(_probe, _direction, _farCollider, _entry.Line);
            else MarkBlocked(_probe, _direction);
        }

        // A side line can stop short of a wall the connection crosses, pass on the far side of a wall from where a solid end was entered, or run wholly inside another solid where its rays see nothing.
        // So each end has to see the line from its own free point: the entry point of a solid end, the position of a free one.
        private int NextEntryCheck(int _key, int _probe, int _direction, int _line, int _after) {
            if (_line == 0) return StageLine;

            LineEnds(_probe, _direction, _line, out Vector3 _from, out Vector3 _to);
            if (_after < StageNearEntry && NeedsEntryCheck(_probe, _from)) return StageNearEntry;
            if (_after >= StageFarEntry) return StageLine;

            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor >= 0) return NeedsEntryCheck(_neighbor, _to) ? StageFarEntry : StageLine;
            // A far probe that does not exist yet has no entry point when it is known to be solid: its center line hit it from one side, or its cell center is inside the near probe's solid.
            // In every other case it will be created free, so it has to see the line from its cell center.
            if (pendingSolid[_key] != 0) return StageLine;
            return IsInside(solidCollider[_probe], CoordToPosition(graph.CoordOf(_probe) + ProbeGraph.Step(_direction))) ? StageLine : StageFarEntry;
        }

        private bool NeedsEntryCheck(int _probe, Vector3 _point) {
            if (solidCollider[_probe] != 0 && !hasEntry[_probe]) return false;
            return (FreePointOf(_probe) - _point).sqrMagnitude > SamePointSqr;
        }

        private Vector3 FreePointOf(int _probe) => solidCollider[_probe] != 0 && hasEntry[_probe] ? entryPoint[_probe] : PositionOf(_probe);

        // Rays do not hit a surface from behind, so the end whose outgoing ray saw nothing is the one inside the collider the other ray hit.
        private void NoteEndInsideSolid(int _key, int _probe, int _direction, bool _farEnd, int _collider) {
            if (!_farEnd) {
                SetSolid(_probe, _collider);
                return;
            }
            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor >= 0) SetSolid(_neighbor, _collider);
            else pendingSolid[_key] = _collider;
        }

        // Two ends inside the same solid see nothing between them, so with a solid end the center line proves nothing and a side line must end outside that solid.
        private bool IsLineTrusted(int _key, int _probe, int _direction, int _line) {
            int _neighbor = graph.Neighbor(_probe, _direction);
            int _nearCollider = solidCollider[_probe];
            int _farCollider = _neighbor >= 0 ? solidCollider[_neighbor] : pendingSolid[_key];
            if (_nearCollider == 0 && _farCollider == 0) return true;
            if (_line == 0) return false;

            LineEnds(_probe, _direction, _line, out Vector3 _from, out Vector3 _to);
            return !IsInside(_nearCollider, _from) && !IsInside(_nearCollider, _to) && !IsInside(_farCollider, _from) && !IsInside(_farCollider, _to);
        }

        private bool IsInside(int _collider, Vector3 _point) => _collider != 0 && geometry.ContainsPoint(_collider, _point);

        private void MarkBlocked(int _probe, int _direction) {
            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor >= 0) {
                if (graph.GetEdge(_probe, _direction) != EdgeState.Blocked) edgesChanged = true;
                graph.SetEdge(_probe, _direction, EdgeState.Blocked);
                return;
            }
            ushort _bit = (ushort)(1 << _direction);
            if ((closedToAbsent[_probe] & _bit) != 0) return;
            closedToAbsent[_probe] |= _bit;
            edgesChanged = true;
        }

        private void MarkOpen(int _probe, int _direction, int _farCollider, int _line) {
            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor < 0) {
                Vector3Int _target = graph.CoordOf(_probe) + ProbeGraph.Step(_direction);
                if (!InRegion(_target)) return;
                _neighbor = AddProbe(_target);
                if (_neighbor < 0) return;
            }

            if (_farCollider != 0) SetSolid(_neighbor, _farCollider);
            if (_line > 0) {
                // Kept on free probes too: one can turn out to be embedded later, when a center line first hits it from one side.
                LineEnds(_probe, _direction, _line, out Vector3 _from, out Vector3 _to);
                StoreEntry(_probe, _from);
                StoreEntry(_neighbor, _to);
            }
            InheritSolid(_probe, _neighbor);
            InheritSolid(_neighbor, _probe);
            closedToAbsent[_probe] &= (ushort)~(1 << _direction);
            if (graph.GetEdge(_probe, _direction) != EdgeState.Open) edgesChanged = true;
            graph.SetEdge(_probe, _direction, EdgeState.Open);
            Reach(_probe);
            Reach(_neighbor);
        }

        private void StoreEntry(int _probe, Vector3 _point) {
            if (hasEntry[_probe]) return;
            hasEntry[_probe] = true;
            entryPoint[_probe] = _point;
        }

        // Without this the next probe along a surface would count as free and its center lines would be trusted, letting the flood into the solid.
        private void InheritSolid(int _from, int _to) {
            if (solidCollider[_from] != 0 && solidCollider[_to] == 0 && geometry.ContainsPoint(solidCollider[_from], PositionOf(_to))) SetSolid(_to, solidCollider[_from]);
        }

        // The collider is watched so a probe embedded in a wall is freed when the wall moves or goes away.
        private void SetSolid(int _probe, int _collider) {
            solidCollider[_probe] = _collider;
            watch.Add(_collider);
        }

        // ==================== SEARCH ===================
        private void StartSearch(ReadOnlySpan<int> _counts) {
            search.Begin(graph);
            edgesChanged = false;
            searchRunning = true;
            for (int i = 0; i < rootCount; i++) {
                int _probe = roots[i].Probe;
                if (_counts[i] > 0 || !graph.IsAlive(_probe) || graph.GenerationOf(_probe) != roots[i].Generation) continue;
                search.AddRoot(_probe, Vector3.Distance(rootListener, PositionOf(_probe)));
                Reach(_probe);
            }
        }

        private void AdvanceSearch(object _owner, long _deadline) {
            bool _firstChunk = true;
            while (searchRunning && budget.MayWork(_owner, _deadline, _firstChunk)) {
                _firstChunk = false;
                if (search.Step(spacing, SearchChunk)) searchRunning = false;
            }
        }

        private bool SearchDue() {
            if (searchRunning) return false;
            if (!searched || edgesChanged) return true;
            if ((listener - lastSearchListener).sqrMagnitude >= SearchMoveSqr) return true;
            return Time.unscaledTime - lastSearchTime >= SearchInterval;
        }

        // ==================== REGION ===================
        private void SweepRegion() {
            for (int i = 0; i < cursorSteps; i++) {
                int _probe = cursor;
                cursor = (cursor + 1) % graph.Capacity;
                if (!graph.IsAlive(_probe)) continue;
                if (!InRegion(graph.CoordOf(_probe))) RemoveProbe(_probe);
                else if (reached[_probe]) Expand(_probe);
            }
        }

        private void SeedCorners() {
            Vector3Int _base = CoordOf(listener);
            for (int i = 0; i < Corners; i++) {
                Vector3Int _corner = _base + CornerOffset(i);
                if (graph.Find(_corner) < 0 && InRegion(_corner)) AddProbe(_corner);
            }
        }

        private int AddProbe(Vector3Int _coord) {
            int _probe = graph.Add(_coord);
            if (_probe < 0) return -1;
            for (int d = 0; d < ProbeGraph.DirectionCount; d++) {
                int _neighbor = graph.Neighbor(_probe, d);
                if (_neighbor < 0) continue;
                ushort _bit = (ushort)(1 << Opposite(d));
                if ((closedToAbsent[_neighbor] & _bit) == 0) continue;
                closedToAbsent[_neighbor] &= (ushort)~_bit;
                graph.SetEdge(_probe, d, EdgeState.Blocked);
            }
            return _probe;
        }

        private void RemoveProbe(int _probe) {
            graph.Remove(_probe);
            reached[_probe] = false;
            closedToAbsent[_probe] = 0;
            solidCollider[_probe] = 0;
            hasEntry[_probe] = false;
            int _first = _probe * ProbeGraph.DirectionCount;
            for (int d = 0; d < ProbeGraph.DirectionCount; d++) {
                nextLine[_first + d] = 0;
                nextStage[_first + d] = 0;
                pendingSolid[_first + d] = 0;
                blockerCount[_first + d] = 0;
            }
        }

        private void Reach(int _probe) {
            if (reached[_probe]) return;
            reached[_probe] = true;
            Expand(_probe);
        }

        private void Expand(int _probe) {
            for (int d = 0; d < ProbeGraph.DirectionCount; d++) {
                int _neighbor = graph.Neighbor(_probe, d);
                if (_neighbor >= 0) {
                    if (graph.GetEdge(_probe, d) == EdgeState.Unknown) Enqueue(RingFlood, CanonicalKey(_probe, d, _neighbor));
                } else if (WantsAbsent(_probe, d)) {
                    Enqueue(RingFlood, KeyOf(_probe, d));
                }
            }
        }

        // A full graph has nowhere to put the far probe, so tracing the connection would be wasted until the sweep frees room.
        private bool WantsAbsent(int _probe, int _direction) {
            if ((closedToAbsent[_probe] & (1 << _direction)) != 0 || graph.Count >= graph.Capacity) return false;
            return InRegion(graph.CoordOf(_probe) + ProbeGraph.Step(_direction));
        }

        private bool InRegion(Vector3Int _coord) {
            Vector3 _offset = CoordToPosition(_coord) - listener;
            return _offset.x * _offset.x + _offset.z * _offset.z <= radiusSqr && Mathf.Abs(_offset.y) <= verticalExtent;
        }

        // ==================== SCHEDULING ===================
        private void ScheduleBatch() {
            int _available = Mathf.Min(pathRays, budget.Remaining / 2);
            if (_available < MinRaysToSchedule) return;

            int _rayCount = SearchDue() ? WriteRootRays() : 0;
            rootCount = _rayCount;
            entryCount = 0;

            while (_rayCount + RaysPerLine <= _available && TryNextEdge(out int _key, out int _ring)) {
                int _probe = _key / ProbeGraph.DirectionCount;
                int _line = nextLine[_key];
                int _stage = nextStage[_key];
                entries[entryCount++] = new BatchEdge { Key = _key, Generation = graph.GenerationOf(_probe), Line = _line, Stage = _stage, Ring = _ring };
                WriteEdgeRays(_probe, _key % ProbeGraph.DirectionCount, _line, _stage, _rayCount);
                _rayCount += RaysPerLine;
            }

            if (_rayCount == 0) return;

            budget.TryConsume(_rayCount);
            batch = geometry.Schedule(new ReadOnlySpan<AcousticRay>(rays, 0, _rayCount), 1);
            if (rootCount > 0) {
                searched = true;
                lastSearchTime = Time.unscaledTime;
                lastSearchListener = listener;
                rootListener = listener;
            }
        }

        private int WriteRootRays() {
            Vector3Int _base = CoordOf(listener);
            int _count = 0;
            for (int i = 0; i < Corners; i++) {
                int _probe = graph.Find(_base + CornerOffset(i));
                if (_probe < 0) continue;
                roots[_count] = new BatchRoot { Probe = _probe, Generation = graph.GenerationOf(_probe) };
                rays[_count] = new AcousticRay { From = listener, To = PositionOf(_probe) };
                _count++;
            }
            return _count;
        }

        private void WriteEdgeRays(int _probe, int _direction, int _line, int _stage, int _at) {
            LineEnds(_probe, _direction, _line, out Vector3 _from, out Vector3 _to);
            if (_stage == StageNearEntry) _to = FreePointOf(_probe);
            else if (_stage == StageFarEntry) _from = FarFreePoint(_probe, _direction);
            rays[_at] = new AcousticRay { From = _from, To = _to };
            rays[_at + 1] = new AcousticRay { From = _to, To = _from };
        }

        private Vector3 FarFreePoint(int _probe, int _direction) {
            int _neighbor = graph.Neighbor(_probe, _direction);
            return _neighbor >= 0 ? FreePointOf(_neighbor) : CoordToPosition(graph.CoordOf(_probe) + ProbeGraph.Step(_direction));
        }

        private void LineEnds(int _probe, int _direction, int _line, out Vector3 _from, out Vector3 _to) {
            Vector3Int _coord = graph.CoordOf(_probe);
            Vector3 _offset = LineOffset(_direction, _line);
            _from = CoordToPosition(_coord) + _offset;
            _to = CoordToPosition(_coord + ProbeGraph.Step(_direction)) + _offset;
        }

        private Vector3 LineOffset(int _direction, int _line) {
            if (_line == 0) return Vector3.zero;
            float _distance = spacing / LineCount;
            return LineAxis[_direction] * (_line == 1 ? _distance : -_distance);
        }

        private bool TryNextEdge(out int _key, out int _ring) {
            for (_ring = 0; _ring < RingCount; _ring++) {
                while (rings[_ring].TryPop(out _key)) {
                    if (IsWorthTracing(_key, _ring)) return true;
                    Forget(_key);
                }
            }
            _key = 0;
            _ring = 0;
            return false;
        }

        private bool IsWorthTracing(int _key, int _ring) {
            int _probe = _key / ProbeGraph.DirectionCount;
            int _direction = _key % ProbeGraph.DirectionCount;
            if (!graph.IsAlive(_probe)) return false;
            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor >= 0) return _ring != RingFlood || graph.GetEdge(_probe, _direction) == EdgeState.Unknown;
            switch (_ring) {
                case RingFlood: return WantsAbsent(_probe, _direction);
                // Only the flood of a reached probe opens connections towards cells without probes.
                case RingInvalidated: return reached[_probe] && WantsAbsent(_probe, _direction);
                default: return false;
            }
        }

        // ==================== CHANGES ===================
        private void AddBlocker(int _key, int _collider) {
            if (_collider == 0) return;
            int _first = _key * MaxBlockers;
            int _count = blockerCount[_key];
            for (int i = 0; i < _count; i++) {
                if (blockers[_first + i] == _collider) return;
            }
            if (_count == MaxBlockers) {
                for (int i = 1; i < MaxBlockers; i++) blockers[_first + i - 1] = blockers[_first + i];
                _count--;
            }
            blockers[_first + _count] = _collider;
            blockerCount[_key] = (byte)(_count + 1);
            watch.Add(_collider);
        }

        private void WatchColliders() {
            int _changed = watch.Tick(Time.unscaledDeltaTime, WatchPeriod);
            if (_changed == 0) return;
            ReadOnlySpan<int> _ids = watch.Changed;

            for (int _key = 0; _key < blockerCount.Length; _key++) {
                int _first = _key * MaxBlockers;
                for (int i = 0; i < blockerCount[_key]; i++) {
                    if (_ids.IndexOf(blockers[_first + i]) < 0) continue;
                    Invalidate(_key);
                    break;
                }
            }

            // A freed probe finds out what it is embedded in again when its connections are traced.
            for (int _probe = 0; _probe < graph.Capacity; _probe++) {
                if (solidCollider[_probe] == 0 || _ids.IndexOf(solidCollider[_probe]) < 0) continue;
                solidCollider[_probe] = 0;
                hasEntry[_probe] = false;
                if (!graph.IsAlive(_probe)) continue;
                for (int d = 0; d < ProbeGraph.DirectionCount; d++) {
                    int _neighbor = graph.Neighbor(_probe, d);
                    if (_neighbor >= 0) Invalidate(CanonicalKey(_probe, d, _neighbor));
                    else if ((closedToAbsent[_probe] & (1 << d)) != 0) Invalidate(KeyOf(_probe, d));
                }
            }
        }

        // Back to Unknown, with what was learnt from the old geometry forgotten, and queued to be traced again.
        private void Invalidate(int _key) {
            int _probe = _key / ProbeGraph.DirectionCount;
            int _direction = _key % ProbeGraph.DirectionCount;
            blockerCount[_key] = 0;
            pendingSolid[_key] = 0;
            nextLine[_key] = 0;
            nextStage[_key] = 0;
            if (!graph.IsAlive(_probe)) return;

            closedToAbsent[_probe] &= (ushort)~(1 << _direction);
            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor >= 0) {
                if (graph.GetEdge(_probe, _direction) != EdgeState.Unknown) {
                    graph.SetEdge(_probe, _direction, EdgeState.Unknown);
                    edgesChanged = true;
                }
                // Blockers found while the far probe was absent sit under the key of the side that traced them.
                _key = CanonicalKey(_probe, _direction, _neighbor);
                blockerCount[_key] = 0;
                pendingSolid[_key] = 0;
                nextLine[_key] = 0;
                nextStage[_key] = 0;
            }
            if (queued[_key]) return;
            queued[_key] = true;
            rings[RingInvalidated].Push(_key);
        }

        // ==================== QUEUES ===================
        private void Enqueue(int _ring, int _key) {
            if (queued[_key]) return;
            queued[_key] = true;
            nextLine[_key] = 0;
            nextStage[_key] = 0;
            pendingSolid[_key] = 0;
            blockerCount[_key] = 0;
            rings[_ring].Push(_key);
        }

        private void Forget(int _key) {
            queued[_key] = false;
            nextLine[_key] = 0;
            nextStage[_key] = 0;
            pendingSolid[_key] = 0;
        }

        private static int KeyOf(int _probe, int _direction) => _probe * ProbeGraph.DirectionCount + _direction;

        // With both ends alive a connection is always queued from the side that stores it, so it is never traced twice.
        private static int CanonicalKey(int _probe, int _direction, int _neighbor) {
            return _direction < ProbeGraph.EdgesPerProbe ? KeyOf(_probe, _direction) : KeyOf(_neighbor, _direction - ProbeGraph.EdgesPerProbe);
        }

        // ==================== HELPERS ===================
        private void DropBatch() {
            if (batch != NoBatch) geometry.Release(batch);
            batch = NoBatch;
            entryCount = 0;
            rootCount = 0;
        }

        private Vector3 CoordToPosition(Vector3Int _coord) {
            return new Vector3((_coord.x + 0.5f) * spacing, (_coord.y + 0.5f) * spacing, (_coord.z + 0.5f) * spacing);
        }

        private static Vector3Int CornerOffset(int _index) {
            return new Vector3Int(_index & 1, (_index >> 1) & 1, (_index >> 2) & 1);
        }

        private static int Opposite(int _direction) {
            return _direction < ProbeGraph.EdgesPerProbe ? _direction + ProbeGraph.EdgesPerProbe : _direction - ProbeGraph.EdgesPerProbe;
        }

        private static bool IsFinite(Vector3 _value) {
            return !float.IsNaN(_value.x) && !float.IsNaN(_value.y) && !float.IsNaN(_value.z)
                && !float.IsInfinity(_value.x) && !float.IsInfinity(_value.y) && !float.IsInfinity(_value.z);
        }

        // Horizontal connections shift along their horizontal perpendicular, vertical ones along x.
        private static Vector3[] BuildLineAxes() {
            Vector3[] _axes = new Vector3[ProbeGraph.DirectionCount];
            for (int d = 0; d < _axes.Length; d++) {
                Vector3Int _step = ProbeGraph.Step(d);
                _axes[d] = _step.y != 0 ? Vector3.right : new Vector3(_step.z, 0f, -_step.x).normalized;
            }
            return _axes;
        }
    }
}
