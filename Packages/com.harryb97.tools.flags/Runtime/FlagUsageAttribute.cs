using System;

namespace HarryTools.Flags
{
    public enum FlagUsageKind
    {
        Read,
        Write,
        Other
    }

    /// <summary>
    /// Marks a FlagRef field so the usage index knows whether it reads or writes the flag.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class FlagUsageAttribute : Attribute
    {
        // ==================== CONSTRUCTORS ====================

        public FlagUsageAttribute(FlagUsageKind _kind)
        {
            Kind = _kind;
        }

        // ==================== PROPERTIES ====================

        public FlagUsageKind Kind { get; }
    }
}
