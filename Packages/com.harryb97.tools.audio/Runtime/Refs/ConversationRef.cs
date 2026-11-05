using System;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Serializable reference to a ConversationAsset by stable id. Generated constants keep it in sync with the project. </summary>
    [Serializable]
    public struct ConversationRef : IEquatable<ConversationRef> {
        // ==================== VARIABLES ===================
        [SerializeField] private string id;

        // ==================== CONSTRUCTORS ===================
        public ConversationRef(string _id) {
            id = _id;
        }

        // ==================== PROPERTIES ===================
        public string Id => id;
        public bool IsValid => !string.IsNullOrEmpty(id);

        // ==================== PUBLIC ===================
        /// <summary> Looks the asset up in the active catalog. False when there is no catalog, no match, or the match has another type. </summary>
        public bool TryResolve(out ConversationAsset _asset) {
            AudioCatalog _catalog = AudioCatalog.Active;
            if (_catalog == null) {
                _asset = null;
                return false;
            }
            return _catalog.TryGet(id, out _asset);
        }

        public bool Equals(ConversationRef _other) => string.Equals(id, _other.id, StringComparison.Ordinal);
        public override bool Equals(object _obj) => _obj is ConversationRef _other && Equals(_other);
        public override int GetHashCode() => id == null ? 0 : id.GetHashCode();
        public override string ToString() => id ?? string.Empty;

        public static bool operator ==(ConversationRef _a, ConversationRef _b) => _a.Equals(_b);
        public static bool operator !=(ConversationRef _a, ConversationRef _b) => !_a.Equals(_b);
    }
}
