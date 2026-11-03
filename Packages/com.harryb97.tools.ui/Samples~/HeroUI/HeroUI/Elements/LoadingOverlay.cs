using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HarryTools.UI.HeroUI
{
    /// <summary>
    /// Pantalla de carga de ejemplo para la capa de overlays. Al implementar IProgress&lt;float&gt;,
    /// recibe el progreso de <c>UI.Overlays.ShowWhile&lt;LoadingOverlay&gt;(...)</c>.
    /// </summary>
    public class LoadingOverlay : View, IProgress<float>
    {
        [Tooltip("Image with 'Image Type = Filled' that shows the progress (optional).")]
        [SerializeField] private Image _progressBar;
        [Tooltip("Text that shows the percentage (optional).")]
        [SerializeField] private TMP_Text _progressLabel;

        public float Progress { get; private set; }

        public void Report(float value)
        {
            Progress = Mathf.Clamp01(value);

            if (_progressBar)
                _progressBar.fillAmount = Progress;
            if (_progressLabel)
                _progressLabel.text = $"{Mathf.RoundToInt(Progress * 100f)} %";
        }

        protected override void OnShown() => Report(0f);
    }
}
