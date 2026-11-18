using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Read only text of a fixed number of lines. What does not fit is cut and ends with an ellipsis, so the text never changes the size of what holds it </summary>
    internal sealed class ClippedLabel : Label {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string LabelClass = "dialogue-node__preview";
        private const string Ellipsis = "…";
        // Room for rounding when a measured height is compared with the lines allowed
        private const float HeightTolerance = 0.5f;

        private string fullText = "";
        private float fittedWidth = -1f;
        #endregion

        #region Public Variables
        /// <summary> The most lines of text shown </summary>
        internal int MaxLines { get; }

        /// <summary> The whole text. <see cref="TextElement.text"/> holds the part shown </summary>
        internal string FullText {
            get => fullText;
            set {
                value = value ?? "";
                if (value == fullText) return;
                fullText = value;
                Fit();
            }
        }
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal ClippedLabel(int _maxLines) {
            MaxLines = Mathf.Max(1, _maxLines);
            // Plain text: a cut can never leave half a tag
            enableRichText = false;
            pickingMode = PickingMode.Ignore;
            AddToClassList(LabelClass);
            RegisterCallback<GeometryChangedEvent>(_ => {
                if (!Mathf.Approximately(contentRect.width, fittedWidth)) Fit();
            });
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        /// <summary> Shows the longest start of the text that fits the lines allowed at the current width </summary>
        private void Fit() {
            float _width = contentRect.width;
            // Not laid out yet: the first layout pass fits it
            if (float.IsNaN(_width) || _width <= 0f) {
                text = fullText;
                return;
            }
            fittedWidth = _width;

            float _maxHeight = LinesHeight() + HeightTolerance;
            if (TextHeight(fullText, _width) <= _maxHeight) {
                text = fullText;
                return;
            }

            int _low = 0;
            int _high = fullText.Length;
            while (_low < _high) {
                int _middle = (_low + _high + 1) / 2;
                if (TextHeight(Cut(_middle), _width) <= _maxHeight) _low = _middle;
                else _high = _middle - 1;
            }
            text = Cut(_low);
        }

        private string Cut(int _length) => fullText.Substring(0, _length).TrimEnd() + Ellipsis;

        private float TextHeight(string _text, float _width) {
            return MeasureTextSize(_text, _width, MeasureMode.Exactly, 0f, MeasureMode.Undefined).y;
        }

        private float LinesHeight() {
            string _lines = "A";
            for (int _i = 1; _i < MaxLines; _i++) _lines += "\nA";
            return MeasureTextSize(_lines, 0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).y;
        }
        #endregion
    }
}
