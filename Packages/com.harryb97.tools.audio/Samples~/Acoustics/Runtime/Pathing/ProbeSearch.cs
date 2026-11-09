using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary>
    /// Time-sliced Dijkstra over a <see cref="ProbeGraph"/>. Writes into one set of arrays while reads come from the last finished set.
    /// </summary>
    internal sealed class ProbeSearch {
        // ==================== VARIABLES ===================
        public const int NoParent = -1;

        private static readonly float[] DirectionLength = BuildLengths();

        private readonly int capacity;
        private readonly float[][] distances = new float[2][];
        private readonly int[][] parents = new int[2][];
        private readonly int[][] generations = new int[2][];
        private readonly bool[] settled;
        private readonly int[] heapProbes;
        private readonly float[] heapKeys;
        private int heapCount;
        private int writeSet;
        private int readSet = 1;
        private bool running;
        private bool hasResult;
        private ProbeGraph graph;

        // ==================== PROPERTIES ===================
        public bool HasResult => hasResult;

        // ==================== LIFECYCLE ===================
        public ProbeSearch(int _capacity) {
            capacity = Mathf.Max(1, _capacity);
            for (int s = 0; s < 2; s++) {
                distances[s] = new float[capacity];
                parents[s] = new int[capacity];
                generations[s] = new int[capacity];
                for (int i = 0; i < capacity; i++) {
                    distances[s][i] = float.PositiveInfinity;
                    parents[s][i] = NoParent;
                    generations[s][i] = -1;
                }
            }
            settled = new bool[capacity];
            int _heapSize = capacity * (ProbeGraph.DirectionCount + 1); // one push per relaxed edge plus root insertions
            heapProbes = new int[_heapSize];
            heapKeys = new float[_heapSize];
        }

        // ==================== PUBLIC API ===================
        public void Begin(ProbeGraph _graph) {
            graph = _graph;
            float[] _distance = distances[writeSet];
            int[] _parent = parents[writeSet];
            int[] _generation = generations[writeSet];
            for (int i = 0; i < capacity; i++) {
                _distance[i] = float.PositiveInfinity;
                _parent[i] = NoParent;
                _generation[i] = -1;
                settled[i] = false;
            }
            heapCount = 0;
            running = _graph != null;
        }

        public void AddRoot(int _probe, float _cost) {
            if (!running || _probe < 0 || _probe >= capacity || !graph.IsAlive(_probe)) return;
            if (_cost >= distances[writeSet][_probe]) return;
            distances[writeSet][_probe] = _cost;
            parents[writeSet][_probe] = NoParent;
            generations[writeSet][_probe] = graph.GenerationOf(_probe);
            Push(_probe, _cost);
        }

        /// <summary> Settles up to _maxNodes probes. Returns true when finished; its results then become the visible ones. </summary>
        public bool Step(float _spacing, int _maxNodes) {
            if (!running) return false;
            float[] _distance = distances[writeSet];
            int[] _parent = parents[writeSet];
            int[] _generation = generations[writeSet];

            int _processed = 0;
            while (_processed < _maxNodes && heapCount > 0) {
                int _probe = heapProbes[0];
                float _key = heapKeys[0];
                PopTop();
                if (settled[_probe] || _key > _distance[_probe]) continue;
                if (!graph.IsAlive(_probe) || graph.GenerationOf(_probe) != _generation[_probe]) continue;

                settled[_probe] = true;
                _processed++;
                for (int d = 0; d < ProbeGraph.DirectionCount; d++) {
                    if (graph.GetEdge(_probe, d) != EdgeState.Open) continue;
                    int _neighbor = graph.Neighbor(_probe, d);
                    if (_neighbor < 0 || settled[_neighbor]) continue;
                    float _candidate = _key + DirectionLength[d] * _spacing;
                    if (_candidate >= _distance[_neighbor]) continue;
                    _distance[_neighbor] = _candidate;
                    _parent[_neighbor] = _probe;
                    _generation[_neighbor] = graph.GenerationOf(_neighbor);
                    Push(_neighbor, _candidate);
                }
            }

            if (heapCount > 0) return false;
            int _swap = readSet;
            readSet = writeSet;
            writeSet = _swap;
            hasResult = true;
            running = false;
            return true;
        }

        public float DistanceOf(int _probe) {
            if (!hasResult || _probe < 0 || _probe >= capacity) return float.PositiveInfinity;
            return distances[readSet][_probe];
        }

        public int ParentOf(int _probe) {
            if (!hasResult || _probe < 0 || _probe >= capacity) return NoParent;
            return parents[readSet][_probe];
        }

        public int GenerationOf(int _probe) {
            if (!hasResult || _probe < 0 || _probe >= capacity) return -1;
            return generations[readSet][_probe];
        }

        // ==================== INTERNAL ===================
        private static float[] BuildLengths() {
            float[] _lengths = new float[ProbeGraph.DirectionCount];
            for (int d = 0; d < _lengths.Length; d++) {
                Vector3Int _step = ProbeGraph.Step(d);
                _lengths[d] = Mathf.Sqrt(_step.x * _step.x + _step.y * _step.y + _step.z * _step.z);
            }
            return _lengths;
        }

        #region Heap
        private void Push(int _probe, float _key) {
            if (heapCount >= heapProbes.Length) return;
            int _index = heapCount++;
            while (_index > 0) {
                int _parentIndex = (_index - 1) >> 1;
                if (heapKeys[_parentIndex] <= _key) break;
                heapProbes[_index] = heapProbes[_parentIndex];
                heapKeys[_index] = heapKeys[_parentIndex];
                _index = _parentIndex;
            }
            heapProbes[_index] = _probe;
            heapKeys[_index] = _key;
        }

        private void PopTop() {
            heapCount--;
            if (heapCount == 0) return;
            int _probe = heapProbes[heapCount];
            float _key = heapKeys[heapCount];
            int _index = 0;
            while (true) {
                int _child = _index * 2 + 1;
                if (_child >= heapCount) break;
                if (_child + 1 < heapCount && heapKeys[_child + 1] < heapKeys[_child]) _child++;
                if (heapKeys[_child] >= _key) break;
                heapProbes[_index] = heapProbes[_child];
                heapKeys[_index] = heapKeys[_child];
                _index = _child;
            }
            heapProbes[_index] = _probe;
            heapKeys[_index] = _key;
        }
        #endregion
    }
}
