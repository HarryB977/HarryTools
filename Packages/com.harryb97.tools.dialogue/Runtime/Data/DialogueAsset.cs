using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> A dialogue: a graph of nodes connected by id. Every asset has exactly one <see cref="StartNode"/> </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "HarryTools/Dialogue/Dialogue")]
    public sealed class DialogueAsset : ScriptableObject {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField, HideInInspector] private string assetId = "";
        [SerializeReference] private List<DialogueNode> nodes = new List<DialogueNode>();

        [Tooltip("Use this dialogue's own clock instead of the project default"), SerializeField] private bool overrideTime;
        [SerializeField] private DialogueTime time = DialogueTime.Unscaled;
        [Tooltip("Use this dialogue's own ambient policy instead of the project default"), SerializeField] private bool overrideAmbientPolicy;
        [SerializeField] private AmbientPolicy ambientPolicy = AmbientPolicy.Continue;
        [Tooltip("The player can skip this dialogue"), SerializeField] private bool allowSkip = true;

        // Assigned on first read when the serialized id is empty, never saved
        private string runtimeAssetId;
        private readonly List<NodeOutput> outputBuffer = new List<NodeOutput>();
        #endregion

        #region Public Variables
        /// <summary> Identifies the asset in saved data. The AssetDatabase GUID once imported; a throwaway id for instances created in code </summary>
        public string AssetId {
            get {
                if (!string.IsNullOrEmpty(assetId)) return assetId;
                if (string.IsNullOrEmpty(runtimeAssetId)) runtimeAssetId = DialogueIds.New();
                return runtimeAssetId;
            }
        }

        public IReadOnlyList<DialogueNode> Nodes => nodes;

        public StartNode Start {
            get {
                for (int _i = 0; _i < nodes.Count; _i++) {
                    if (nodes[_i] is StartNode _start) return _start;
                }
                return null;
            }
        }

        public bool OverrideTime {
            get => overrideTime;
            internal set => overrideTime = value;
        }

        public DialogueTime Time {
            get => time;
            internal set => time = value;
        }
        public bool OverrideAmbientPolicy => overrideAmbientPolicy;
        public AmbientPolicy AmbientPolicy => ambientPolicy;
        public bool AllowSkip => allowSkip;
        #endregion

        // ==================== UNITY ====================
        #region Unity
        private void OnEnable() {
            // A freshly created asset has no nodes yet
            if (nodes.Count == 0) AddNode<StartNode>();
        }

        private void OnValidate() {
            EnsureIds();
        }
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> The node with the given id, or null </summary>
        public DialogueNode Find(string _id) {
            if (string.IsNullOrEmpty(_id)) return null;
            for (int _i = 0; _i < nodes.Count; _i++) {
                if (nodes[_i] != null && nodes[_i].Id == _id) return nodes[_i];
            }
            return null;
        }
        #endregion

        // ==================== INTERNAL ====================
        #region Internal
        /// <summary> The AssetDatabase GUID written by the editor on import </summary>
        internal string SerializedAssetId => assetId;

        internal void SetAssetId(string _id) {
            assetId = _id;
        }

        internal void SetAssetIdForTests(string _id) {
            assetId = _id;
        }

        internal T AddNode<T>() where T : DialogueNode, new() {
            T _node = new T();
            _node.Id = DialogueIds.New();
            nodes.Add(_node);
            return _node;
        }

        /// <summary> Removes the node and clears every output that pointed to it. The start node cannot be removed </summary>
        internal void RemoveNode(string _id) {
            DialogueNode _node = Find(_id);
            if (_node == null || _node is StartNode) return;

            nodes.Remove(_node);
            for (int _i = 0; _i < nodes.Count; _i++) {
                if (nodes[_i] == null) continue;
                outputBuffer.Clear();
                nodes[_i].CollectOutputs(outputBuffer);
                for (int _o = 0; _o < outputBuffer.Count; _o++) {
                    if (outputBuffer[_o] != null && outputBuffer[_o].Target == _id) outputBuffer[_o].Target = "";
                }
            }
        }

        /// <summary> Gives a new id to every empty or repeated id in nodes, options and outputs. The first one found keeps its id </summary>
        internal void EnsureIds() {
            HashSet<string> _used = new HashSet<string>();

            for (int _i = 0; _i < nodes.Count; _i++) {
                if (nodes[_i] == null) continue;
                if (string.IsNullOrEmpty(nodes[_i].Id) || !_used.Add(nodes[_i].Id)) {
                    nodes[_i].Id = NewUnused(_used);
                }
            }

            for (int _i = 0; _i < nodes.Count; _i++) {
                if (!(nodes[_i] is ChoiceNode _choice)) continue;
                for (int _o = 0; _o < _choice.Options.Count; _o++) {
                    ChoiceOption _option = _choice.Options[_o];
                    if (_option == null) continue;
                    if (string.IsNullOrEmpty(_option.Id) || !_used.Add(_option.Id)) {
                        _option.Id = NewUnused(_used);
                    }
                }
            }

            for (int _i = 0; _i < nodes.Count; _i++) {
                if (nodes[_i] == null) continue;
                outputBuffer.Clear();
                nodes[_i].CollectOutputs(outputBuffer);
                for (int _o = 0; _o < outputBuffer.Count; _o++) {
                    NodeOutput _output = outputBuffer[_o];
                    if (_output == null) continue;
                    if (string.IsNullOrEmpty(_output.Id) || !_used.Add(_output.Id)) {
                        _output.Id = NewUnused(_used);
                    }
                }
            }
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static string NewUnused(HashSet<string> _used) {
            string _id;
            do {
                _id = DialogueIds.New();
            } while (!_used.Add(_id));
            return _id;
        }
        #endregion
    }
}
