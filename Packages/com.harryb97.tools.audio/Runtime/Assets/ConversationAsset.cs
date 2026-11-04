using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> One line of a conversation. </summary>
    [Serializable]
    public struct ConversationLine {
        // ==================== VARIABLES ===================
        public VoiceAsset Voice;
        [Min(0), Tooltip("Seconds of silence after the line ends.")]
        public float PauseAfter;
    }

    /// <summary> An ordered sequence of voice lines. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Conversation", fileName = "Conversation")]
    public sealed class ConversationAsset : AudioCatalogItem {
        // ==================== VARIABLES ===================
        [SerializeField] private List<ConversationLine> lines = new List<ConversationLine>();

        // ==================== PROPERTIES ===================
        public IReadOnlyList<ConversationLine> Lines => lines;

        // ==================== INTERNAL ===================
        internal void SetLines(IList<ConversationLine> _lines) {
            lines.Clear();
            for (int _i = 0; _i < _lines.Count; _i++) lines.Add(_lines[_i]);
        }
    }
}
