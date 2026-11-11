using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Flags
{
    [Serializable, TypeMenu("Flags/Toggle Flag")]
    public sealed class ToggleFlagAction : InstantAction
    {
        // ==================== VARIABLES ====================

        [FlagUsage(FlagUsageKind.Write)]
        [SerializeField] private FlagRef flag = new FlagRef();

        // ==================== CONSTRUCTORS ====================

        public ToggleFlagAction() { }

        internal ToggleFlagAction(FlagRef _flag)
        {
            flag = _flag;
        }

        // ==================== METHODS ====================

        protected override void Execute(LogicContext _context) => Flags.Toggle(flag, _context.Self);
    }
}
