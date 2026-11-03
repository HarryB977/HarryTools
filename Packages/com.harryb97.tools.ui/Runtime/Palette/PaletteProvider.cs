using System;
using UnityEngine;

namespace HarryTools.UI
{
    [ExecuteAlways]
    public class PaletteProvider : MonoBehaviour, IPaletteProvider
    {
        [SerializeField] private ColorPalette _colorPalette;
        private ColorPalette _subscribedPalette;

        public event Action onColorChange;

        public ColorPalette palette
        {
            get
            {
                return _colorPalette;
            }
            set
            {
                if (_colorPalette == value)
                    return;

                _colorPalette = value;
                if (isActiveAndEnabled)
                    SubscribeToPalette();
                onColorChange?.Invoke();
            }
        }

        private void OnEnable()
        {
            SubscribeToPalette();
            onColorChange?.Invoke();
        }

        private void OnDisable()
        {
            UnsubscribeFromPalette();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
                SubscribeToPalette();
            onColorChange?.Invoke();
        }

        private void SubscribeToPalette()
        {
            if (_subscribedPalette == _colorPalette)
                return;

            UnsubscribeFromPalette();

            _subscribedPalette = _colorPalette;
            if (_subscribedPalette)
                _subscribedPalette.onChange += OnColorPaletteDirty;
        }

        private void UnsubscribeFromPalette()
        {
            if (!_subscribedPalette)
                return;

            _subscribedPalette.onChange -= OnColorPaletteDirty;
            _subscribedPalette = null;
        }

        private void OnColorPaletteDirty()
        {
            onColorChange?.Invoke();
        }
    }
}
