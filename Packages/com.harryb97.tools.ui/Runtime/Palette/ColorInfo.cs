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
        /// <summary> Id del rol personalizado cuando <see cref="color"/> es <see cref="ColorType.Custom"/> </summary>
        public int customRoleId;
        /// <summary> Pasos más claros (positivo) o más oscuros (negativo) </summary>
        [Range(ColorTones.MinTone, ColorTones.MaxTone)] public int tone;

        /// <summary> Color resuelto: blanco sin paleta; magenta si el rol personalizado ya no existe </summary>
        public Color GetColor(ColorPalette palette)
        {
            if (!palette)
                return Color.white;

            Color resolved;

            if (color == ColorType.Custom)
            {
                if (!palette.TryGetCustomRole(customRoleId, out var role))
                    return Color.magenta;
                resolved = contrast ? role.contrast : role.color;
            }
            else
            {
                resolved = contrast ? palette.GetContrast(color) : palette.GetColor(color);
            }

            return ColorTones.Apply(resolved, tone);
        }
    }
}
