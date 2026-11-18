using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Search of node types for the graph canvas. The chosen type is created where the search was opened </summary>
    internal sealed class DialogueNodeSearch : ScriptableObject, ISearchWindowProvider {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static readonly (string Label, Type Type)[] NodeTypes = {
            ("Line", typeof(LineNode)),
            ("Choice", typeof(ChoiceNode)),
            ("Branch", typeof(BranchNode)),
            ("Actions", typeof(ActionsNode)),
            ("Jump", typeof(JumpNode)),
            ("End", typeof(EndNode))
        };

        private DialogueGraphView view;
        private string fromOutputId;
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The canvas that receives the node, and the output to connect it to (null for none) </summary>
        internal void Setup(DialogueGraphView _view, string _fromOutputId) {
            view = _view;
            fromOutputId = _fromOutputId;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext _context) {
            List<SearchTreeEntry> _tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent("Create Node"), 0) };
            for (int _i = 0; _i < NodeTypes.Length; _i++) {
                _tree.Add(new SearchTreeEntry(new GUIContent(NodeTypes[_i].Label)) { level = 1, userData = NodeTypes[_i].Type });
            }
            return _tree;
        }

        public bool OnSelectEntry(SearchTreeEntry _entry, SearchWindowContext _context) {
            if (view == null || !(_entry.userData is Type _type)) return false;
            view.CreateNode(_type, view.ScreenToCanvas(_context.screenMousePosition), fromOutputId);
            return true;
        }
        #endregion
    }
}
