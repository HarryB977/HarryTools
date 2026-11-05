using HarryTools.DomainReload;

namespace HarryTools.Audio {
    /// <summary> Platform capabilities. WebGL has no usable mixer, so volumes are multiplied on the audio sources. </summary>
    internal static class AudioPlatform {
        // ==================== VARIABLES ===================
        [ClearOnReload] private static bool? overrideValue;

        // ==================== PROPERTIES ===================
        public static bool UsesMixer {
            get {
                if (overrideValue.HasValue) return overrideValue.Value;
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        // ==================== INTERNAL ===================
        internal static void OverrideUsesMixer(bool? _value) => overrideValue = _value;
    }
}
