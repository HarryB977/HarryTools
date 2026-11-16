using System;
using UnityEngine;

using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.TextEffects {
    /// <summary> Reveals the text up to the next wait for input, or to the end </summary>
    [Serializable, TypeMenu("Text Effects/Skip Text")]
    public sealed class SkipTextAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<EffectText> target = new TargetComponent<EffectText>();
        #endregion

        // ==================== CONSTRUCTORS ===================
        public SkipTextAction() { }

        public SkipTextAction(TargetComponent<EffectText> _target) {
            target = _target;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            EffectText _text = target?.Resolve(_context);
            if (_text != null) _text.Skip();
        }
    }
}
