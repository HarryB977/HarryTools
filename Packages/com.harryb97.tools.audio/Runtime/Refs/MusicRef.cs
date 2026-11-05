using System;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Serializable reference to a MusicAsset by stable id. Generated constants keep it in sync with the project. </summary>
    [Serializable]
    public struct MusicRef : IEquatable<MusicRef> {
        // ==================== VARIABLES ===================
        [SerializeField] private string id;

        // ==================== CONSTRUCTORS ===================
        public MusicRef(string _id) {
            id = _id;
        }

        // ==================== PROPERTIES ===================
        public string Id => id;
        public bool IsValid => !string.IsNullOrEmpty(id);

        // ==================== PUBLIC ===================
        /// <summary> Looks the asset up in the active catalog. False when there is no catalog, no match, or the match has another type. </summary>
        public bool TryResolve(out MusicAsset _asset) {
            AudioCatalog _catalog = AudioCatalog.Active;
            if (_catalog == null) {
                _asset = null;
                return false;
            }
            return _catalog.TryGet(id, out _asset);
        }

        /// <summary> Null and empty ids are the same (no reference). </summary>
        public bool Equals(MusicRef _other) => string.Equals(id ?? string.Empty, _other.id ?? string.Empty, StringComparison.Ordinal);
        public override bool Equals(object _obj) => _obj is MusicRef _other && Equals(_other);
        public override int GetHashCode() => string.IsNullOrEmpty(id) ? 0 : id.GetHashCode();
        public override string ToString() => id ?? string.Empty;

        public static bool operator ==(MusicRef _a, MusicRef _b) => _a.Equals(_b);
        public static bool operator !=(MusicRef _a, MusicRef _b) => !_a.Equals(_b);
    }
}
