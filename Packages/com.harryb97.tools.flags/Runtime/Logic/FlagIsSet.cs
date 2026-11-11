using System;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Flags
{
    [Serializable, TypeMenu("Flags/Flag Is Set")]
    public sealed class FlagIsSet : Condition
    {
        // ==================== VARIABLES ====================

        [FlagUsage(FlagUsageKind.Read)]
        [SerializeField] private FlagRef flag = new FlagRef();

        // ==================== CONSTRUCTORS ====================

        public FlagIsSet() { }

        internal FlagIsSet(FlagRef _flag)
        {
            flag = _flag;
        }

        // ==================== METHODS ====================

        protected override bool Evaluate(LogicContext _context) => Flags.IsSet(flag, _context.Self);
    }
}
