using System;
using UnityEngine;

using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.TextEffects {
    /// <summary> Resumes typing after a wait for input </summary>
    [Serializable, TypeMenu("Text Effects/Continue Text")]
    public sealed class ContinueTextAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<EffectText> target = new TargetComponent<EffectText>();
        #endregion

        // ==================== CONSTRUCTORS ===================
        public ContinueTextAction() { }

        public ContinueTextAction(TargetComponent<EffectText> _target) {
            target = _target;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            EffectText _text = target?.Resolve(_context);
            if (_text != null) _text.Continue();
        }
    }
}
