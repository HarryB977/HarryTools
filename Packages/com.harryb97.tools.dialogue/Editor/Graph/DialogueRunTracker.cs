using System;
using System.Collections.Generic;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Follows the dialogues running in Play: the node each one is on and the outputs it took, per dialogue. Follows the main dialogue unless told otherwise </summary>
    internal sealed class DialogueRunTracker : IDisposable {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static readonly string[] NoOutputs = new string[0];

        /// <summary> Where one run is and what it went through </summary>
        private sealed class RunPath {
            internal readonly Dictionary<DialogueAsset, HashSet<string>> Outputs = new Dictionary<DialogueAsset, HashSet<string>>();
            // Last node entered in each dialogue, whose output to the next node of that dialogue is the one taken
            internal readonly Dictionary<DialogueAsset, DialogueNode> LastNodes = new Dictionary<DialogueAsset, DialogueNode>();
            internal DialogueAsset Asset;
            internal DialogueNode Node;
            // The output of the option just chosen, which the next node entered is reached through
            internal NodeOutput Chosen;
        }

        private readonly Dictionary<DialogueRun, RunPath> paths = new Dictionary<DialogueRun, RunPath>();
        private readonly List<NodeOutput> outputBuffer = new List<NodeOutput>();
        private DialogueRun followed;
        // Cleared while a run other than a main one was chosen; the next main run is followed again either way once it is lost
        private bool followsMain = true;
        private bool disposed;
        #endregion

        #region Public Variables
        /// <summary> The run whose node and path are shown. Kept after it ends, with its path, until another run is followed </summary>
        internal DialogueRun Followed {
            get => followed;
            set {
                if (value == followed) return;
                followsMain = value == null || value.IsMain;
                SetFollowed(value);
            }
        }

        /// <summary> The dialogue the followed run is in. Kept after the run ends </summary>
        internal DialogueAsset CurrentAsset => FollowedPath?.Asset;

        /// <summary> The node the followed run is on. Null when nothing is followed or the run ended </summary>
        internal string CurrentNodeId => followed != null && followed.State != RunState.Ended ? FollowedPath?.Node?.Id : null;

        /// <summary> Raised when the followed run moves, ends or changes, and when runs start or end </summary>
        internal event Action Changed;
        #endregion

        #region Private Properties
        private RunPath FollowedPath => followed != null && paths.TryGetValue(followed, out RunPath _path) ? _path : null;
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        /// <summary> Starts tracking. Runs already going are picked up where they are </summary>
        internal DialogueRunTracker() {
            Dialogue.Started += OnStarted;
            Dialogue.Ended += OnEnded;
            Dialogue.NodeEntered += OnNodeEntered;
            Dialogue.ChoiceMade += OnChoiceMade;

            IReadOnlyList<DialogueRun> _active = Dialogue.Active;
            for (int _i = 0; _i < _active.Count; _i++) Track(_active[_i]);
            followed = Dialogue.Main;
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            Dialogue.Started -= OnStarted;
            Dialogue.Ended -= OnEnded;
            Dialogue.NodeEntered -= OnNodeEntered;
            Dialogue.ChoiceMade -= OnChoiceMade;
            paths.Clear();
            followed = null;
            Changed = null;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> Ids of the outputs of the dialogue the followed run went through </summary>
        internal IReadOnlyCollection<string> TraversedOutputIds(DialogueAsset _asset) {
            RunPath _path = FollowedPath;
            if (_path == null || _asset == null || !_path.Outputs.TryGetValue(_asset, out HashSet<string> _outputs)) return NoOutputs;
            return _outputs;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void SetFollowed(DialogueRun _run) {
            followed = _run;
            // Ended runs are only kept for their path while followed
            List<DialogueRun> _ended = null;
            foreach (DialogueRun _tracked in paths.Keys) {
                if (_tracked != followed && _tracked.State == RunState.Ended) (_ended ?? (_ended = new List<DialogueRun>())).Add(_tracked);
            }
            if (_ended != null) {
                for (int _i = 0; _i < _ended.Count; _i++) paths.Remove(_ended[_i]);
            }
            Changed?.Invoke();
        }

        private RunPath Track(DialogueRun _run) {
            if (paths.TryGetValue(_run, out RunPath _path)) return _path;
            _path = new RunPath { Asset = _run.CurrentAsset, Node = _run.Current };
            if (_path.Asset != null && _path.Node != null) _path.LastNodes[_path.Asset] = _path.Node;
            paths.Add(_run, _path);
            return _path;
        }

        private void OnStarted(DialogueRun _run) {
            Track(_run);
            bool _lost = followed == null || followed.State == RunState.Ended;
            if (_run.IsMain && (followsMain || _lost)) {
                followsMain = true;
                SetFollowed(_run);
                return;
            }
            Changed?.Invoke();
        }

        private void OnEnded(DialogueRun _run, EndReason _reason) {
            if (_run != followed) {
                paths.Remove(_run);
                Changed?.Invoke();
                return;
            }
            // A followed run that ends hands over to the main run still going; otherwise its path stays
            DialogueRun _main = Dialogue.Main;
            if (_main != null && _main != _run) {
                followsMain = true;
                Track(_main);
                SetFollowed(_main);
                return;
            }
            Changed?.Invoke();
        }

        private void OnNodeEntered(DialogueRun _run, DialogueNode _node) {
            if (_node == null) return;
            RunPath _path = Track(_run);
            DialogueAsset _asset = _run.CurrentAsset;
            if (_asset != null && _path.LastNodes.TryGetValue(_asset, out DialogueNode _previous) && _previous != null) {
                string _taken = OutputTo(_previous, _node.Id, _path.Chosen);
                if (_taken != null) {
                    if (!_path.Outputs.TryGetValue(_asset, out HashSet<string> _outputs)) _path.Outputs[_asset] = _outputs = new HashSet<string>();
                    _outputs.Add(_taken);
                }
            }
            _path.Chosen = null;
            if (_asset != null) _path.LastNodes[_asset] = _node;
            _path.Asset = _asset;
            _path.Node = _node;
            if (_run == followed) Changed?.Invoke();
        }

        private void OnChoiceMade(DialogueRun _run, ChoiceOption _option) {
            if (_option != null) Track(_run).Chosen = _option.Next;
        }

        /// <summary> Id of the output of the node that leads to the target: the chosen option's, else the no answer output of a choice left without one, else the first that does. Null when none does </summary>
        private string OutputTo(DialogueNode _node, string _targetId, NodeOutput _chosen) {
            if (_node is ChoiceNode _choice) {
                if (_chosen != null && _chosen.Target == _targetId) return _chosen.Id;
                // No option was chosen, so the choice was left through its no answer output
                if (_chosen == null && _choice.NoAnswer.Target == _targetId) return _choice.NoAnswer.Id;
            }
            outputBuffer.Clear();
            _node.CollectOutputs(outputBuffer);
            for (int _i = 0; _i < outputBuffer.Count; _i++) {
                if (outputBuffer[_i] != null && outputBuffer[_i].Target == _targetId) return outputBuffer[_i].Id;
            }
            return null;
        }
        #endregion
    }
}
