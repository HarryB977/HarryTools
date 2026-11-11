using System.IO;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>Finds, or creates on demand, the project's <see cref="FlagDatabase"/> asset.</summary>
    internal static class FlagDatabaseLocator
    {
        // ==================== VARIABLES ====================

        private const string AssetFolder = "Assets/HarryTools/Flags/Resources";
        private const string AssetPath = AssetFolder + "/" + FlagDatabase.ResourcesName + ".asset";

        /// <summary>When set, <see cref="Find"/> and <see cref="GetOrCreate"/> return it instead of touching the project.</summary>
        [ClearOnReload]
        internal static FlagDatabase OverrideForTests;

        // ==================== METHODS ====================

        /// <summary>The project's database, or null when there is none. Never creates anything.</summary>
        public static FlagDatabase Find()
        {
            if (OverrideForTests != null) return OverrideForTests;

            FlagDatabase _atDefault = AssetDatabase.LoadAssetAtPath<FlagDatabase>(AssetPath);
            if (_atDefault != null) return _atDefault;

            foreach (string _guid in AssetDatabase.FindAssets("t:FlagDatabase"))
            {
                FlagDatabase _found = AssetDatabase.LoadAssetAtPath<FlagDatabase>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_found != null) return _found;
            }
            return null;
        }

        /// <summary>The project's database, creating it at the default path when missing.</summary>
        public static FlagDatabase GetOrCreate()
        {
            FlagDatabase _existing = Find();
            if (_existing != null) return _existing;

            Directory.CreateDirectory(AssetFolder);
            FlagDatabase _created = ScriptableObject.CreateInstance<FlagDatabase>();
            AssetDatabase.CreateAsset(_created, AssetPath);
            AssetDatabase.SaveAssets();
            return _created;
        }
    }
}
