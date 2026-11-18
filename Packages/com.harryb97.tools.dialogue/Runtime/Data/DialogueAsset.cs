using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> A dialogue: a graph of nodes connected by id. Every asset has exactly one <see cref="StartNode"/> </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "HarryTools/Dialogue/Dialogue")]
    public sealed class DialogueAsset : ScriptableObject {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField, HideInInspector] private string assetId = "";
        // GUID of the string table collection that holds this dialogue's text, written by the editor
        [SerializeField, HideInInspector] private string tableCollectionGuid = "";
        [SerializeReference] private List<DialogueNode> nodes = new List<DialogueNode>();

        [SerializeField, HideInInspector] private List<DialogueGroup> groups = new List<DialogueGroup>();
        [SerializeField, HideInInspector] private List<DialogueNote> notes = new List<DialogueNote>();

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

        /// <summary> Boxes the editor draws around nodes. They have no effect when the dialogue runs </summary>
        public IReadOnlyList<DialogueGroup> Groups => groups;

        /// <summary> Free text notes of the editor. They have no effect when the dialogue runs </summary>
        public IReadOnlyList<DialogueNote> Notes => notes;

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

        public bool OverrideAmbientPolicy {
            get => overrideAmbientPolicy;
            internal set => overrideAmbientPolicy = value;
        }

        /// <summary> What this dialogue does when it runs as ambient and a main dialogue starts </summary>
        public AmbientPolicy AmbientPolicy {
            get => ambientPolicy;
            internal set => ambientPolicy = value;
        }

        public bool AllowSkip {
            get => allowSkip;
            internal set => allowSkip = value;
        }
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

        internal List<DialogueGroup> GroupList => groups;

        internal List<DialogueNote> NoteList => notes;

        internal void SetAssetId(string _id) {
            assetId = _id;
        }

        /// <summary> GUID of the string table collection linked to this dialogue. Empty until the first text is written </summary>
        internal string TableCollectionGuid {
            get => tableCollectionGuid;
            set => tableCollectionGuid = value;
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
            for (int _g = 0; _g < groups.Count; _g++) {
                if (groups[_g] != null) groups[_g].NodeIdList.RemoveAll(_nodeId => _nodeId == _id);
            }
            for (int _i = 0; _i < nodes.Count; _i++) {
                if (nodes[_i] == null) continue;
                outputBuffer.Clear();
                nodes[_i].CollectOutputs(outputBuffer);
                for (int _o = 0; _o < outputBuffer.Count; _o++) {
                    if (outputBuffer[_o] != null && outputBuffer[_o].Target == _id) outputBuffer[_o].Target = "";
                }
            }
        }

        /// <summary> Gives a new id to every empty or repeated id in nodes, options, outputs, groups and notes. The first one found keeps its id </summary>
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

            for (int _i = 0; _i < groups.Count; _i++) {
                if (groups[_i] == null) continue;
                if (string.IsNullOrEmpty(groups[_i].Id) || !_used.Add(groups[_i].Id)) groups[_i].Id = NewUnused(_used);
            }

            for (int _i = 0; _i < notes.Count; _i++) {
                if (notes[_i] == null) continue;
                if (string.IsNullOrEmpty(notes[_i].Id) || !_used.Add(notes[_i].Id)) notes[_i].Id = NewUnused(_used);
            }
        }

        /// <summary> Gives every node, option, output, group and note a new id and points the outputs at the new node ids. Used when the asset is duplicated </summary>
        internal void RegenerateIds() {
            Dictionary<string, string> _nodeIds = new Dictionary<string, string>();
            HashSet<string> _used = new HashSet<string>();

            for (int _i = 0; _i < nodes.Count; _i++) {
                if (nodes[_i] == null) continue;
                string _new = NewUnused(_used);
                if (!string.IsNullOrEmpty(nodes[_i].Id)) _nodeIds[nodes[_i].Id] = _new;
                nodes[_i].Id = _new;
            }

            for (int _i = 0; _i < nodes.Count; _i++) {
                if (!(nodes[_i] is ChoiceNode _choice)) continue;
                for (int _o = 0; _o < _choice.Options.Count; _o++) {
                    if (_choice.Options[_o] != null) _choice.Options[_o].Id = NewUnused(_used);
                }
            }

            for (int _i = 0; _i < nodes.Count; _i++) {
                if (nodes[_i] == null) continue;
                outputBuffer.Clear();
                nodes[_i].CollectOutputs(outputBuffer);
                for (int _o = 0; _o < outputBuffer.Count; _o++) {
                    NodeOutput _output = outputBuffer[_o];
                    if (_output == null) continue;
                    _output.Id = NewUnused(_used);
                    // A target outside this asset no longer exists, so it ends the dialogue as before
                    if (_output.IsConnected) _output.Target = _nodeIds.TryGetValue(_output.Target, out string _target) ? _target : "";
                }
            }

            for (int _i = 0; _i < groups.Count; _i++) {
                if (groups[_i] == null) continue;
                groups[_i].Id = NewUnused(_used);
                List<string> _members = groups[_i].NodeIdList;
                for (int _m = _members.Count - 1; _m >= 0; _m--) {
                    if (_nodeIds.TryGetValue(_members[_m], out string _member)) _members[_m] = _member;
                    else _members.RemoveAt(_m);
                }
            }

            for (int _i = 0; _i < notes.Count; _i++) {
                if (notes[_i] != null) notes[_i].Id = NewUnused(_used);
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
