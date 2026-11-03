using System;

namespace HarryTools.UI
{
    public interface IPaletteProvider
    {
        public ColorPalette palette { get; }

        public event Action onColorChange;
    }
}
