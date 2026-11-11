using System.IO;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEngine;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Flags.Editor
{
    /// <summary>Finds, or creates on demand, the project's <see cref="FlagUsageIndex"/> asset.</summary>
    internal static class FlagUsageIndexLocator
    {
        // ==================== VARIABLES ====================

        private const string Category = "Flags";

        internal const string AssetFolder = "Assets/HarryTools/Flags";
        internal const string AssetPath = AssetFolder + "/FlagUsageIndex.asset";

        /// <summary>When set, <see cref="Find"/> and <see cref="GetOrCreate"/> return it instead of touching the project.</summary>
        [ClearOnReload]
        internal static FlagUsageIndex OverrideForTests;

        [ClearOnReload] private static FlagUsageIndex cached;

        // ==================== METHODS ====================

        /// <summary>The project's index, or null when there is none. Never creates anything.</summary>
        public static FlagUsageIndex Find()
        {
            if (OverrideForTests != null) return OverrideForTests;
            if (cached == null) cached = AssetDatabase.LoadAssetAtPath<FlagUsageIndex>(AssetPath);
            return cached;
        }

        /// <summary>
        /// The project's index, creating it at the default path when missing. A file that is there but cannot be loaded
        /// (git conflict markers or a broken file) is replaced, with a warning asking for a rebuild.
        /// </summary>
        public static FlagUsageIndex GetOrCreate()
        {
            FlagUsageIndex _existing = Find();
            if (_existing != null) return _existing;

            bool _unloadable = File.Exists(AssetPath);
            if (!AssetDatabase.IsValidFolder("Assets/HarryTools")) AssetDatabase.CreateFolder("Assets", "HarryTools");
            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/HarryTools", "Flags");
            cached = ScriptableObject.CreateInstance<FlagUsageIndex>();
            AssetDatabase.CreateAsset(cached, AssetPath);
            AssetDatabase.SaveAssetIfDirty(cached);

            if (_unloadable)
                Logger.LogWarning(Category, $"The flag usage index '{AssetPath}' could not be loaded (a git merge conflict or a broken file) and was recreated empty. Run Tools > HarryTools > Flags > Rebuild Usage Index.");
            return cached;
        }
    }
}
