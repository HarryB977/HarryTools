using System;
using System.Collections.Generic;

namespace HarryTools.TextEffects {
    /// <summary> Tag names that presets and voices cannot use: ours and the ones TextMeshPro understands </summary>
    public static class ReservedTags {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static readonly string[] ownNames = { "pause", "speed", "waitinput", "event", "voice", "instant" };

        private static readonly string[] names = {
            "pause", "speed", "waitinput", "event", "voice", "instant",
            "a", "align", "allcaps", "alpha", "b", "br", "color", "cspace", "font", "font-weight", "gradient", "i",
            "indent", "line-height", "line-indent", "link", "lowercase", "margin", "mark", "mspace", "nobr", "noparse",
            "page", "pos", "rotate", "s", "size", "smallcaps", "space", "sprite", "strikethrough", "style", "sub", "sup",
            "u", "uppercase", "voffset", "width"
        };

        private static readonly HashSet<string> set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        #endregion

        // ==================== METHODS ====================
        public static IReadOnlyCollection<string> Names => names;

        public static bool IsReserved(string _name) {
            return _name != null && set.Contains(_name);
        }

        /// <summary> True for the tags the parser itself consumes (pause, speed, waitinput, event, voice, instant) </summary>
        internal static bool IsOwn(string _name) {
            for (int i = 0; i < ownNames.Length; i++) {
                if (string.Equals(ownNames[i], _name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
