using System;
using UnityEngine;

namespace HarryTools.UI
{
    [Serializable]
    public struct ColorInfo
    {
        public bool enabled;
        public ColorType color;
        public bool contrast;

        public Color GetColor(ColorPalette palette)
        {
            if (!palette)
                return Color.white;
            return contrast ? palette.GetContrast(color) : palette.GetColor(color);
        }
    }
}
