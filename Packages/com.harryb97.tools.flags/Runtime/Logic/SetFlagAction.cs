using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Flags
{
    [Serializable, TypeMenu("Flags/Set Flag")]
    public sealed class SetFlagAction : InstantAction
    {
        // ==================== VARIABLES ====================

        [FlagUsage(FlagUsageKind.Write)]
        [SerializeField] private FlagRef flag = new FlagRef();

        // ==================== CONSTRUCTORS ====================

        public SetFlagAction() { }

        internal SetFlagAction(FlagRef _flag)
        {
            flag = _flag;
        }

        // ==================== METHODS ====================

        protected override void Execute(LogicContext _context) => Flags.Set(flag, _context.Self);
    }
}
