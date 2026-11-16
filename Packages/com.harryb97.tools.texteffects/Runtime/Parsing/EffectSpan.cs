using System.Collections.Generic;

namespace HarryTools.TextEffects {
    /// <summary> Kind of range tag </summary>
    public enum SpanKind {
        Instant,
        Preset
    }

    /// <summary> A 'name=value' parameter written inside a range tag </summary>
    public readonly struct EffectParam {
        public string Name { get; }
        public string Value { get; }

        public EffectParam(string _name, string _value) {
            Name = _name;
            Value = _value;
        }
    }

    /// <summary> A range of text affected by a range tag. Positions are in the TextMeshPro text (markup removed) </summary>
    public sealed class EffectSpan {
        // ==================== VARIABLES ===================
        #region Public Variables
        public SpanKind Kind { get; }

        /// <summary> Tag name as written in the source </summary>
        public string TagName { get; }

        public int Start { get; }

        /// <summary> Exclusive end. Spans left open end at the length of the TextMeshPro text </summary>
        public int End { get; internal set; }

        public IReadOnlyList<EffectParam> Overrides => overrides;
        #endregion

        #region Private Variables
        private readonly IReadOnlyList<EffectParam> overrides;
        #endregion

        // ==================== START ====================
        internal EffectSpan(SpanKind _kind, string _tagName, int _start, IReadOnlyList<EffectParam> _overrides) {
            Kind = _kind;
            TagName = _tagName;
            Start = _start;
            End = _start;
            overrides = _overrides;
        }
    }
}
