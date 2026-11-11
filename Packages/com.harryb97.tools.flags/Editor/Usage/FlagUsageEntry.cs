using System;

namespace HarryTools.Flags.Editor
{
    /// <summary>One FlagRef of the project, as stored in the <see cref="FlagUsageIndex"/>.</summary>
    [Serializable]
    internal sealed class FlagUsageEntry
    {
        // ==================== VARIABLES ====================

        public string flagGuid = string.Empty;

        public FlagUsageKind kind = FlagUsageKind.Other;

        /// <summary>Asset guid of the scene that holds the reference. Empty for assets.</summary>
        public string sceneGuid = string.Empty;

        /// <summary>Path of the asset (ScriptableObject or prefab) that holds the reference. Empty for scenes.</summary>
        public string assetPath = string.Empty;

        /// <summary>Names from the root to the object, separated by '/'. For a ScriptableObject, its name. Empty when approximate.</summary>
        public string hierarchyPath = string.Empty;

        /// <summary>Full type name of the component or ScriptableObject. Empty when approximate.</summary>
        public string componentType = string.Empty;

        /// <summary>Found by searching the scene file for the flag guid: no path, no component and no kind until the scene is read again.</summary>
        public bool approximate;

        // ==================== METHODS ====================

        internal bool SameAs(FlagUsageEntry _other) =>
            _other != null && flagGuid == _other.flagGuid && kind == _other.kind && sceneGuid == _other.sceneGuid && assetPath == _other.assetPath
            && hierarchyPath == _other.hierarchyPath && componentType == _other.componentType && approximate == _other.approximate;
    }
}
