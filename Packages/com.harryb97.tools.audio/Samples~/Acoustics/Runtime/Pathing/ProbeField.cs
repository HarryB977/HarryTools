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

        private const int RingInUse = 0;
        private const int RingInvalidated = 1;
        private const int RingFlood = 2;
        private const int RingCount = 3;

        private static readonly Vector3[] LineAxis = BuildLineAxes();

        private readonly IAcousticGeometry geometry;
        private readonly RayBudget budget;

        private ProbeGraph graph;
        private ProbeSearch search;
        private IntRing[] rings;
        private bool[] queued;
        private byte[] nextLine;
        private bool[] reached;
        // One bit per direction: the connection is blocked and its far end has no probe to hold that state.
        private ushort[] closedToAbsent;
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
            reached = new bool[graph.Capacity];
            closedToAbsent = new ushort[graph.Capacity];
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

        /// <summary> Asks for a connection used by a path to be traced again. </summary>
        public void MarkInUse(int _probe, int _direction) {
            if (disposed || graph == null || _direction < 0 || _direction >= ProbeGraph.DirectionCount) return;
            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor < 0) return;
            Enqueue(RingInUse, CanonicalKey(_probe, _direction, _neighbor));
        }

        // ==================== READING ===================
        private void ReadBatch() {
            if (batch == NoBatch) return;
            if (!geometry.TryGetResults(batch, out ReadOnlySpan<AcousticHit> _, out ReadOnlySpan<int> _counts)) return;

            for (int i = 0; i < entryCount; i++) {
                int _ray = rootCount + i * RaysPerLine;
                ResolveEdge(entries[i], _counts[_ray] > 0, _counts[_ray + 1] > 0);
            }
            if (rootCount > 0) StartSearch(_counts);
            DropBatch();
        }

        private void ResolveEdge(in BatchEdge _entry, bool _forwardHit, bool _backwardHit) {
            int _probe = _entry.Key / ProbeGraph.DirectionCount;
            int _direction = _entry.Key % ProbeGraph.DirectionCount;

            if (!graph.IsAlive(_probe) || graph.GenerationOf(_probe) != _entry.Generation) {
                Forget(_entry.Key);
                return;
            }

            bool _blocked = _forwardHit || _backwardHit;
            // A hit from one side only means an end of the center line is inside a solid. A side line could slip past its surface and let the flood in.
            bool _endInsideSolid = _entry.Line == 0 && _forwardHit != _backwardHit;
            if (_blocked && !_endInsideSolid && _entry.Line + 1 < LineCount) {
                nextLine[_entry.Key] = (byte)(_entry.Line + 1);
                rings[_entry.Ring].Push(_entry.Key);
                return;
            }

            Forget(_entry.Key);
            if (_blocked) MarkBlocked(_probe, _direction);
            else MarkOpen(_probe, _direction);
        }

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

        private void MarkOpen(int _probe, int _direction) {
            int _neighbor = graph.Neighbor(_probe, _direction);
            if (_neighbor < 0) {
                Vector3Int _target = graph.CoordOf(_probe) + ProbeGraph.Step(_direction);
                if (!InRegion(_target)) return;
                _neighbor = AddProbe(_target);
                if (_neighbor < 0) return;
            }

            closedToAbsent[_probe] &= (ushort)~(1 << _direction);
            if (graph.GetEdge(_probe, _direction) != EdgeState.Open) edgesChanged = true;
            graph.SetEdge(_probe, _direction, EdgeState.Open);
            Reach(_probe);
            Reach(_neighbor);
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
            int _first = _probe * ProbeGraph.DirectionCount;
            for (int d = 0; d < ProbeGraph.DirectionCount; d++) nextLine[_first + d] = 0;
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
                entries[entryCount++] = new BatchEdge { Key = _key, Generation = graph.GenerationOf(_probe), Line = _line, Ring = _ring };
                WriteLine(_probe, _key % ProbeGraph.DirectionCount, _line, _rayCount);
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

        private void WriteLine(int _probe, int _direction, int _line, int _at) {
            Vector3Int _coord = graph.CoordOf(_probe);
            Vector3 _offset = LineOffset(_direction, _line);
            Vector3 _from = CoordToPosition(_coord) + _offset;
            Vector3 _to = CoordToPosition(_coord + ProbeGraph.Step(_direction)) + _offset;
            rays[_at] = new AcousticRay { From = _from, To = _to };
            rays[_at + 1] = new AcousticRay { From = _to, To = _from };
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
            if (_ring != RingFlood) return _neighbor >= 0;
            return _neighbor >= 0 ? graph.GetEdge(_probe, _direction) == EdgeState.Unknown : WantsAbsent(_probe, _direction);
        }

        // ==================== QUEUES ===================
        private void Enqueue(int _ring, int _key) {
            if (queued[_key]) return;
            queued[_key] = true;
            nextLine[_key] = 0;
            rings[_ring].Push(_key);
        }

        private void Forget(int _key) {
            queued[_key] = false;
            nextLine[_key] = 0;
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
