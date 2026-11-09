using System;
using Unity.Profiling;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary>
    /// Gives each sound the reverb of the room around it, whatever room the listener is in.
    /// Rays are shared per cell of space: each cell keeps a ring of samples that is renewed a little on every update, so geometry changes show up without detecting them.
    /// Only one batch is in flight; until a cell has enough samples each voice keeps its last reverb.
    /// </summary>
    internal sealed class RoomModule : IAcousticModule, IDisposable {
        // ==================== TYPES ===================
        private struct VoiceSlot {
            public AudioHandle Handle;
            public bool HasReverb;
            public AcousticReverb Reverb;
            public int SeenStamp;
            public Vector3 Position;
            public int Cell;
        }

        private struct Cell {
            public bool InUse;
            public Vector3Int Coord;
            // Bumped when the cell is recycled, so a batch in flight never writes into its new life.
            public int Generation;
            public float LastUsed;
            public float NextRefresh;
            public int UsedStamp;
            public int PickedStamp;
            public Vector3 Origin;
            public int Count;
            public int Head;
            public bool HasEstimate;
            public RoomEstimate Estimate;
        }

        private struct BatchEntry {
            public int Cell;
            public int Generation;
            public int RayStart;
            public int RayCount;
            public int RingStart;
        }

        // ==================== VARIABLES ===================
        private const int InitialVoices = 16;
        private const int FallbackMaxSimulated = 10;
        private const int NoBatch = -1;
        // The voice has no reverb at all.
        private const int NoCell = -1;
        // The cell table is full of cells used this tick: the voice keeps what it had.
        private const int CellUnavailable = -2;
        // Room rays only care about the first surface.
        private const int MaxHits = 1;
        private const float RecycleAfter = 1f;
        private const float MinCellSize = 0.01f;
        private const float DirectionStepRatio = 0.618f;
        private static readonly Vector3 FallbackAbsorption = new Vector3(0.1f, 0.06f, 0.04f);
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Acoustics.Room");

        private readonly IAcousticGeometry geometry;
        private readonly RayBudget budget;
        private readonly Cell[] cells;
        private AcousticProfile profile;
        private Vector3[] directions = Array.Empty<Vector3>();
        private RoomSample[] samples = Array.Empty<RoomSample>();
        private int roomRays = 1;
        private int raysPerUpdate = 1;
        private int minSamples = 1;
        private float cellSize = 1f;
        private float maxDistance;

        private VoiceSlot[] slots = new VoiceSlot[InitialVoices];
        private int[] slotOfQuery = new int[InitialVoices];
        private int[] cellOfQuery = new int[InitialVoices];
        private BatchEntry[] entries = Array.Empty<BatchEntry>();
        private AcousticRay[] rays = Array.Empty<AcousticRay>();
        private int entryCount;
        private int entryCursor;
        private int batch = NoBatch;
        private int stamp;
        private bool disposed;

        // ==================== PROPERTIES ===================
        public int Order => 10;
        // Cells are throttled by the UpdateInterval of the profile here, so a voice can read a finished batch on the first tick it is ready.
        public float UpdateInterval => 0f;

        // Read by the scene view gizmos.
        internal int SlotCount => slots.Length;
        internal float CellSize => cellSize;

        // ==================== INITIALIZATION ===================
        public RoomModule(AcousticProfile _profile, IAcousticGeometry _geometry, RayBudget _budget) {
            geometry = _geometry;
            budget = _budget;
            AudioProjectSettings _settings = AudioProjectSettings.Instance;
            int _maxSimulated = _settings != null ? Mathf.Max(1, _settings.MaxSimulated) : FallbackMaxSimulated;
            cells = new Cell[_maxSimulated * 2];
            entries = new BatchEntry[cells.Length];
            Configure(_profile);
        }

        /// <summary> Switches profile. The batch in flight and every cell are dropped; voices keep their last reverb until their new cell is ready. </summary>
        public void Configure(AcousticProfile _profile) {
            profile = _profile;
            DropBatch();

            cellSize = Mathf.Max(MinCellSize, _profile.RoomCellSize);
            maxDistance = Mathf.Max(0f, _profile.RoomMaxDistance);
            int _rays = Mathf.Max(1, _profile.RoomRays);
            raysPerUpdate = Mathf.Clamp(_profile.RoomRaysPerUpdate, 1, _rays);
            minSamples = Mathf.Max(1, _rays / 2);

            if (_rays != roomRays || samples.Length == 0) {
                roomRays = _rays;
                samples = new RoomSample[cells.Length * roomRays];
                directions = BuildDirections(roomRays);
            }

            for (int c = 0; c < cells.Length; c++) {
                int _generation = cells[c].Generation + 1;
                cells[c] = new Cell { Generation = _generation };
            }
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            DropBatch();
        }

        // ==================== DEBUG ===================
        /// <summary> Reads what a slot got in the last evaluation: where the sound is, the center of its cell, the room estimate and the reverb it sends. False for an empty slot, a dead voice or a voice without a ready cell. </summary>
        internal bool TryGetDebug(int _slot, out Vector3 _source, out Vector3 _cellCenter, out RoomEstimate _room, out AcousticReverb _reverb) {
            _source = default;
            _cellCenter = default;
            _room = default;
            _reverb = default;
            if (_slot < 0 || _slot >= slots.Length) return false;

            VoiceSlot _voice = slots[_slot];
            if (_voice.SeenStamp != stamp || !_voice.HasReverb || _voice.Cell < 0 || !_voice.Handle.IsValid) return false;
            Cell _cell = cells[_voice.Cell];
            if (!_cell.HasEstimate) return false;

            _source = _voice.Position;
            _cellCenter = (new Vector3(_cell.Coord.x, _cell.Coord.y, _cell.Coord.z) + Vector3.one * 0.5f) * cellSize;
            _room = _cell.Estimate;
            _reverb = _voice.Reverb;
            return true;
        }

        // ==================== EVALUATION ===================
        public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
            // The core can still evaluate this module in the tick the other one failed in, with the geometry already gone.
            if (disposed) return;

            try {
                using (Marker.Auto()) {
                    long _deadline = budget.BeginWork();
                    float _now = Time.unscaledTime;
                    EnsureCapacity(_queries.Length);
                    AssignSlots(_queries);
                    ClaimCells(_queries, _now);
                    // Runs after the propagation module: when that one used up the frame, the voices keep their reverb until the next.
                    if (!budget.WaitIfSpent(this)) {
                        ReadBatch(_deadline);
                        if (batch == NoBatch) ScheduleDue(_queries.Length, _now, _deadline);
                    }
                    WriteResults(_queries, _results);
                    budget.EndWork();
                }
            } catch {
                Acoustics.OnModuleFailed(this);
                throw;
            }
        }

        private void WriteResults(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
            for (int i = 0; i < _queries.Length; i++) {
                ref VoiceSlot _slot = ref slots[slotOfQuery[i]];
                int _cell = cellOfQuery[i];
                _slot.Position = _queries[i].SourcePosition;
                _slot.Cell = _cell;

                if (_cell == NoCell) {
                    _slot.HasReverb = false;
                } else if (_cell != CellUnavailable && cells[_cell].HasEstimate) {
                    _slot.HasReverb = true;
                    _slot.Reverb = RoomMath.ToReverb(in cells[_cell].Estimate, profile.ReverbWet * SendOf(_queries[i]));
                }

                if (!_slot.HasReverb) continue;
                _results[i].HasReverb = true;
                _results[i].Reverb = _slot.Reverb;
            }
        }

        private static float SendOf(in AcousticQuery _query) {
            AcousticSourceExtension _extension = _query.Asset != null ? _query.Asset.GetExtension<AcousticSourceExtension>() : null;
            if (_extension == null) return 1f;
            return _extension.Exclude ? 0f : Mathf.Max(0f, _extension.ReverbSend);
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
                slots[_free] = new VoiceSlot { Handle = _queries[i].Handle, SeenStamp = stamp };
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
            Array.Resize(ref cellOfQuery, _queries);
        }

        // ==================== CELLS ===================
        private void ClaimCells(ReadOnlySpan<AcousticQuery> _queries, float _now) {
            for (int i = 0; i < _queries.Length; i++) {
                Vector3 _position = _queries[i].SourcePosition;
                bool _finite = !float.IsNaN(_position.x + _position.y + _position.z) && !float.IsInfinity(_position.x + _position.y + _position.z);
                cellOfQuery[i] = _finite && SendOf(_queries[i]) > 0f ? Claim(_position, _now) : NoCell;
            }
        }

        private int Claim(Vector3 _position, float _now) {
            Vector3Int _coord = new Vector3Int(Mathf.FloorToInt(_position.x / cellSize), Mathf.FloorToInt(_position.y / cellSize), Mathf.FloorToInt(_position.z / cellSize));
            int _free = -1;
            int _oldest = -1;
            float _oldestTime = float.MaxValue;

            for (int c = 0; c < cells.Length; c++) {
                ref Cell _cell = ref cells[c];
                if (!_cell.InUse) {
                    if (_free < 0) _free = c;
                    continue;
                }
                if (_cell.Coord == _coord) {
                    if (_now - _cell.LastUsed >= RecycleAfter && _cell.UsedStamp != stamp) Recycle(ref _cell);
                    Touch(ref _cell, _position, _now);
                    return c;
                }
                if (_cell.UsedStamp != stamp && _cell.LastUsed < _oldestTime) {
                    _oldest = c;
                    _oldestTime = _cell.LastUsed;
                }
            }

            int _chosen = _free >= 0 ? _free : _oldest;
            if (_chosen < 0) return CellUnavailable;

            ref Cell _target = ref cells[_chosen];
            Recycle(ref _target);
            _target.InUse = true;
            _target.Coord = _coord;
            Touch(ref _target, _position, _now);
            return _chosen;
        }

        private void Touch(ref Cell _cell, Vector3 _position, float _now) {
            // The first voice of the tick sets where the rays start, so voices sharing a cell do not move it around.
            if (_cell.UsedStamp != stamp) _cell.Origin = _position;
            _cell.UsedStamp = stamp;
            _cell.LastUsed = _now;
        }

        private static void Recycle(ref Cell _cell) {
            _cell.Generation++;
            _cell.Count = 0;
            _cell.Head = 0;
            _cell.NextRefresh = 0f;
            _cell.HasEstimate = false;
        }

        // ==================== SCHEDULING ===================
        private void ScheduleDue(int _queryCount, float _now, long _deadline) {
            float _interval = Mathf.Max(0f, profile.UpdateInterval);
            int _rayCount = 0;
            entryCount = 0;
            entryCursor = 0;

            while (entryCount < entries.Length) {
                int _index = MostOverdueCell(_queryCount, _now);
                if (_index < 0 || !budget.MayWork(this, _deadline, entryCount == 0)) break;

                ref Cell _cell = ref cells[_index];
                bool _incomplete = _cell.Count < roomRays;
                int _granted = budget.TryConsume(_incomplete ? roomRays - _cell.Count : raysPerUpdate);
                if (_granted == 0) break;

                EnsureRays(_rayCount + _granted);
                entries[entryCount++] = new BatchEntry { Cell = _index, Generation = _cell.Generation, RayStart = _rayCount, RayCount = _granted, RingStart = _cell.Head };
                for (int k = 0; k < _granted; k++) {
                    Vector3 _direction = directions[(_cell.Head + k) % roomRays];
                    rays[_rayCount++] = new AcousticRay { From = _cell.Origin, To = _cell.Origin + _direction * maxDistance };
                }

                _cell.PickedStamp = stamp;
                // A cell that is still filling tries again as soon as the batch comes back.
                _cell.NextRefresh = _now + _interval;
            }

            if (entryCount > 0) batch = geometry.Schedule(new ReadOnlySpan<AcousticRay>(rays, 0, _rayCount), MaxHits);
        }

        // Cells still filling go first: their voices have nothing to play with yet.
        private int MostOverdueCell(int _queryCount, float _now) {
            int _best = -1;
            float _bestDue = float.MaxValue;
            for (int i = 0; i < _queryCount; i++) {
                int _index = cellOfQuery[i];
                if (_index < 0) continue;

                ref Cell _cell = ref cells[_index];
                if (_cell.PickedStamp == stamp) continue;
                if (_cell.Count >= roomRays && _cell.NextRefresh > _now) continue;

                float _due = _cell.Count < roomRays ? float.MinValue : _cell.NextRefresh;
                if (_due >= _bestDue) continue;
                _best = _index;
                _bestDue = _due;
            }
            return _best;
        }

        private void EnsureRays(int _needed) {
            if (rays.Length < _needed) Array.Resize(ref rays, Mathf.Max(_needed, rays.Length * 2));
        }

        // ==================== READING ===================
        private void ReadBatch(long _deadline) {
            if (batch == NoBatch) return;
            if (!geometry.TryGetResults(batch, out ReadOnlySpan<AcousticHit> _hits, out ReadOnlySpan<int> _counts)) return;

            int _read = 0;
            while (entryCursor < entryCount) {
                if (!budget.MayWork(this, _deadline, _read == 0)) return;
                BatchEntry _entry = entries[entryCursor++];
                _read++;
                if (cells[_entry.Cell].Generation == _entry.Generation) StoreSamples(_entry, _hits, _counts);
            }
            DropBatch();
        }

        private void DropBatch() {
            if (batch != NoBatch) geometry.Release(batch);
            batch = NoBatch;
            entryCount = 0;
            entryCursor = 0;
        }

        private void StoreSamples(in BatchEntry _entry, ReadOnlySpan<AcousticHit> _hits, ReadOnlySpan<int> _counts) {
            ref Cell _cell = ref cells[_entry.Cell];
            int _base = _entry.Cell * roomRays;

            for (int k = 0; k < _entry.RayCount; k++) {
                int _ray = _entry.RayStart + k;
                int _sample = _base + (_entry.RingStart + k) % roomRays;
                if (_counts[_ray] > 0) {
                    AcousticHit _hit = _hits[_ray * MaxHits];
                    samples[_sample] = new RoomSample { Hit = true, Distance = _hit.Distance, Absorption = AbsorptionOf(_hit.ColliderId) };
                } else {
                    samples[_sample] = new RoomSample { Hit = false, Distance = maxDistance };
                }
            }

            _cell.Head = (_entry.RingStart + _entry.RayCount) % roomRays;
            _cell.Count = Mathf.Min(roomRays, _cell.Count + _entry.RayCount);
            if (_cell.Count < minSamples) return;

            _cell.Estimate = RoomMath.Estimate(new ReadOnlySpan<RoomSample>(samples, _base, _cell.Count), maxDistance);
            _cell.HasEstimate = true;
        }

        private Vector3 AbsorptionOf(int _colliderId) {
            AcousticMaterial _material = geometry.MaterialOf(_colliderId);
            return _material != null ? _material.Absorption : FallbackAbsorption;
        }

        // ==================== DIRECTIONS ===================
        // Fibonacci lattice walked with a step coprime to the count: consecutive ring slots land far apart, so half a ring already covers the whole sphere.
        private static Vector3[] BuildDirections(int _count) {
            Vector3[] _directions = new Vector3[_count];
            float _goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

            int _step = Mathf.Max(1, Mathf.RoundToInt(_count * DirectionStepRatio));
            while (GreatestCommonDivisor(_step, _count) != 1) _step++;

            for (int i = 0; i < _count; i++) {
                int _lattice = (int)((long)i * _step % _count);
                float _y = 1f - (_lattice + 0.5f) * 2f / _count;
                float _ring = Mathf.Sqrt(1f - _y * _y);
                float _angle = _lattice * _goldenAngle;
                _directions[i] = new Vector3(Mathf.Cos(_angle) * _ring, _y, Mathf.Sin(_angle) * _ring);
            }
            return _directions;
        }

        private static int GreatestCommonDivisor(int _a, int _b) {
            while (_b != 0) {
                int _rest = _a % _b;
                _a = _b;
                _b = _rest;
            }
            return _a;
        }
    }
}
