using UnityEngine;

namespace HarryTools.Flags.Tests
{
    /// <summary>ScriptableObject with a FlagRef field, for the usage index tests.</summary>
    public class FlagUsageTestAsset : ScriptableObject
    {
        // ==================== VARIABLES ====================

        [FlagUsage(FlagUsageKind.Read)]
        public FlagRef flag = new FlagRef();
    }
}
