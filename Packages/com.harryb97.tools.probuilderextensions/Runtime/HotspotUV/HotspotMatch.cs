namespace HarryTools.ProBuilderExtensions.HotspotUV {
    public struct HotspotMatchSettings {
        /// <summary>Master switch, combined with each patch's own Allow Rotation.</summary>
        public bool allowRotation;

        /// <summary>Master switch, combined with each patch's own Allow Mirror X/Y.</summary>
        public bool allowMirror;

        /// <summary>How much worse than the best score a candidate can be and still be picked at random.</summary>
        public float randomTolerance;

        /// <summary>Allowed relative difference between face and patch world size on each axis (0.25 = ±25%).</summary>
        public float scaleTolerance;

        /// <summary>Source of randomness. A shared generator is used when null.</summary>
        public System.Random random;
    }

    public struct HotspotMatch {
        public int index;

        /// <summary>The patch is rotated 90° relative to the face.</summary>
        public bool rotated;

        public bool mirrorX;
        public bool mirrorY;

        /// <summary>Axis of the patch (in patch space) along which it repeats instead of stretching.</summary>
        public TilingAxis tiling;

        /// <summary>No patch was within the scale tolerance; this is the closest one.</summary>
        public bool outOfTolerance;

        public bool IsValid => index >= 0;

        public static HotspotMatch Invalid => new() { index = -1 };
    }
}
