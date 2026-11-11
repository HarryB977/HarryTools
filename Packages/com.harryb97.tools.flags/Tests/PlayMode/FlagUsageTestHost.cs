using UnityEngine;

namespace HarryTools.Flags.Tests
{
    /// <summary>User-style component with FlagRef fields, for the usage index tests. Lives in a runtime assembly so it can be added to objects.</summary>
    [AddComponentMenu("")]
    public class FlagUsageTestHost : MonoBehaviour
    {
        // ==================== VARIABLES ====================

        [FlagUsage(FlagUsageKind.Write)]
        public FlagRef marked = new FlagRef();

        public FlagRef plain = new FlagRef();
    }
}
