using System;
using System.Text.RegularExpressions;

namespace HarryTools.Flags
{
    /// <summary>Naming rules for flags.</summary>
    public static class FlagNaming
    {
        // ==================== VARIABLES ====================

        private static readonly Regex ValidName = new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        // ==================== PROPERTIES ====================

        /// <summary>Comparer used for every flag name comparison (case insensitive).</summary>
        public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

        // ==================== METHODS ====================

        /// <summary>True when the name is non-empty and only contains letters, digits and underscores.</summary>
        public static bool IsValidName(string _name) => !string.IsNullOrEmpty(_name) && ValidName.IsMatch(_name);

        /// <summary>New stable flag identifier (32 hex characters).</summary>
        public static string NewGuid() => Guid.NewGuid().ToString("N");
    }
}
