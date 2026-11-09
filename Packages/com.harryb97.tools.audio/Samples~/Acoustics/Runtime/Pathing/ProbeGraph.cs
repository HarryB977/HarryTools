using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> State of the connection between two neighbouring probes. </summary>
    internal enum EdgeState : byte {
        Unknown,
        Open,
        Blocked
    }

    /// <summary>
    /// Sparse grid of probes with fixed capacity. Each probe keeps its neighbours in an array, so searches never touch the dictionary.
    /// Directions 0..4 are +x, +z, +x+z, +x-z, +y; direction d + 5 is the opposite of d.
    /// </summary>
    internal sealed class ProbeGraph {
        // ==================== VARIABLES ===================
        public const int EdgesPerProbe = 5;
        public const int DirectionCount = 10;

        private static readonly Vector3Int[] Steps = {
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 1),
            new Vector3Int(1, 0, -1),
            new Vector3Int(0, 1, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, -1),
            new Vector3Int(-1, 0, -1),
            new Vector3Int(-1, 0, 1),
            new Vector3Int(0, -1, 0)
        };

        private readonly int capacity;
        private readonly Dictionary<Vector3Int, int> lookup;
        private readonly Vector3Int[] coords;
        private readonly bool[] alive;
        private readonly int[] generations;
        private readonly int[] neighbors;
        private readonly EdgeState[] edges;
        private readonly int[] freeList;
        private int freeCount;
        private int nextFresh;
        private int count;

        // ==================== PROPERTIES ===================
        public int Capacity => capacity;
        public int Count => count;

        // ==================== LIFECYCLE ===================
        public ProbeGraph(int _capacity) {
            capacity = Mathf.Max(1, _capacity);
            lookup = new Dictionary<Vector3Int, int>(capacity);
            coords = new Vector3Int[capacity];
            alive = new bool[capacity];
            generations = new int[capacity];
            neighbors = new int[capacity * DirectionCount];
            edges = new EdgeState[capacity * EdgesPerProbe];
            freeList = new int[capacity];
            for (int i = 0; i < neighbors.Length; i++) neighbors[i] = -1;
        }

        // ==================== PUBLIC API ===================
        public int Find(Vector3Int _coord) {
            return lookup.TryGetValue(_coord, out int _probe) ? _probe : -1;
        }

        public int Add(Vector3Int _coord) {
            if (lookup.TryGetValue(_coord, out int _existing)) return _existing;
            int _probe;
            if (freeCount > 0) _probe = freeList[--freeCount];
            else if (nextFresh < capacity) _probe = nextFresh++;
            else return -1;

            coords[_probe] = _coord;
            alive[_probe] = true;
            lookup[_coord] = _probe;
            count++;
            for (int d = 0; d < DirectionCount; d++) {
                int _neighbor = Find(_coord + Steps[d]);
                if (_neighbor < 0) continue;
                neighbors[_probe * DirectionCount + d] = _neighbor;
                neighbors[_neighbor * DirectionCount + Opposite(d)] = _probe;
            }
            return _probe;
        }

        public void Remove(int _probe) {
            if (!IsAlive(_probe)) return;
            for (int d = 0; d < DirectionCount; d++) {
                int _neighbor = neighbors[_probe * DirectionCount + d];
                if (_neighbor < 0) continue;
                neighbors[_neighbor * DirectionCount + Opposite(d)] = -1;
                // The shared edge lives on the neighbour when the direction from it is 0..4.
                if (d >= EdgesPerProbe) edges[_neighbor * EdgesPerProbe + d - EdgesPerProbe] = EdgeState.Unknown;
                neighbors[_probe * DirectionCount + d] = -1;
            }
            for (int e = 0; e < EdgesPerProbe; e++) edges[_probe * EdgesPerProbe + e] = EdgeState.Unknown;
            lookup.Remove(coords[_probe]);
            alive[_probe] = false;
            generations[_probe]++;
            freeList[freeCount++] = _probe;
            count--;
        }

        public bool IsAlive(int _probe) {
            return _probe >= 0 && _probe < capacity && alive[_probe];
        }

        public int GenerationOf(int _probe) {
            return _probe >= 0 && _probe < capacity ? generations[_probe] : -1;
        }

        public Vector3Int CoordOf(int _probe) {
            return _probe >= 0 && _probe < capacity ? coords[_probe] : Vector3Int.zero;
        }

        public int Neighbor(int _probe, int _direction) {
            if (!IsAlive(_probe) || _direction < 0 || _direction >= DirectionCount) return -1;
            return neighbors[_probe * DirectionCount + _direction];
        }

        public EdgeState GetEdge(int _probe, int _direction) {
            return ResolveEdge(_probe, _direction, out int _index) ? edges[_index] : EdgeState.Unknown;
        }

        public void SetEdge(int _probe, int _direction, EdgeState _state) {
            if (ResolveEdge(_probe, _direction, out int _index)) edges[_index] = _state;
        }

        public static Vector3Int Step(int _direction) {
            return Steps[_direction];
        }

        // ==================== INTERNAL ===================
        private static int Opposite(int _direction) {
            return _direction < EdgesPerProbe ? _direction + EdgesPerProbe : _direction - EdgesPerProbe;
        }

        private bool ResolveEdge(int _probe, int _direction, out int _index) {
            _index = -1;
            int _neighbor = Neighbor(_probe, _direction);
            if (_neighbor < 0) return false;
            _index = _direction < EdgesPerProbe
                ? _probe * EdgesPerProbe + _direction
                : _neighbor * EdgesPerProbe + _direction - EdgesPerProbe;
            return true;
        }
    }
}
