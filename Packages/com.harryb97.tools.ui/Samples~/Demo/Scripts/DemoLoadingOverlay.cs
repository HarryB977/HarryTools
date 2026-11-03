using System;
using TMPro;
using UnityEngine;

namespace HarryTools.UI.Demo {
    /// <summary> Pantalla de carga con barra de progreso. Recibe el progreso de UI.Overlays.ShowWhile </summary>
    public class DemoLoadingOverlay : View, IProgress<float> {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Bar fill: its anchorMax.x is adjusted.")]
        [SerializeField] private RectTransform barFill;
        [SerializeField] private TMP_Text label;
        #endregion

        // ==================== METHODS ====================
        public void Report(float _value) {
            float _progress = Mathf.Clamp01(_value);

            barFill.anchorMax = new Vector2(_progress, barFill.anchorMax.y);
            label.text = $"Loading... {Mathf.RoundToInt(_progress * 100f)} %";
        }

        protected override void OnShown() => Report(0f);
    }
}
