using System;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.HotspotUV {
    /// <summary>One rectangle of a hotspot layout and the ways it may be applied to a face.</summary>
    [Serializable]
    public struct HotspotPatch {
        [Tooltip("Normalized rect (0-1) with the origin at the top-left corner of the texture.")]
        public Rect rect;

        [Tooltip("Allow rotating the patch 90° to fit a face. Disable for walls or anything that must stay upright.")]
        public bool allowRotation;

        [Tooltip("Allow randomly flipping the patch horizontally.")]
        public bool allowMirrorX;

        [Tooltip("Allow randomly flipping the patch vertically.")]
        public bool allowMirrorY;

        [Tooltip("Repeat the patch along its full-span axis instead of stretching it. Only valid for patches spanning the whole width or height.")]
        public bool allowTiling;

        [Tooltip("Pixels to shrink the patch by on each side when mapping, to avoid bleeding from neighbours in the mipmaps.")]
        [Min(0)] public int inset;

        public static HotspotPatch Default(Rect rect) => new() {
            rect = rect,
            allowRotation = true,
            inset = 1,
        };
    }

    public enum TilingAxis { None, Horizontal, Vertical }
}
