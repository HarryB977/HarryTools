using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Flags
{
    [Serializable, TypeMenu("Flags/Clear Flag")]
    public sealed class ClearFlagAction : InstantAction
    {
        // ==================== VARIABLES ====================

        [FlagUsage(FlagUsageKind.Write)]
        [SerializeField] private FlagRef flag = new FlagRef();

        // ==================== CONSTRUCTORS ====================

        public ClearFlagAction() { }

        internal ClearFlagAction(FlagRef _flag)
        {
            flag = _flag;
        }

        // ==================== METHODS ====================

        protected override void Execute(LogicContext _context) => Flags.Clear(flag, _context.Self);
    }
}
