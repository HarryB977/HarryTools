using System;
using TMPro;
using UnityEngine;

namespace HarryTools.UI
{
    [ExecuteInEditMode]
    public class IconProcessor : MonoBehaviour, ITextPreprocessor
    {
        [SerializeField] private TMP_Text _text;

        public TMP_Text textComponent
        {
            get => _text;
            set
            {
                if (_text == value)
                    return;

                if (_text)
                {
                    _text.textPreprocessor = null;
                    _text.SetAllDirty();
                }

                _text = value;

                if (_text)
                {
                    _text.textPreprocessor = this;
                    _text.SetAllDirty();
                }
            }
        }

        private void Reset()
        {
            _text = GetComponentInChildren<TMP_Text>();
        }

        private void OnValidate()
        {
            if (enabled && _text)
            {
                _text.textPreprocessor = this;
                _text.SetAllDirty();
            }
        }

        private void OnEnable()
        {
            if (_text)
            {
                _text.textPreprocessor = this;
                _text.SetAllDirty();
            }
        }

        private void OnDisable()
        {
            if (_text)
            {
                _text.textPreprocessor = null;
                _text.SetAllDirty();
            }
        }

        public string PreprocessText(string text)
        {
            if (string.IsNullOrEmpty(text) || !_text || !_text.richText)
                return text;

            const string prefix = "<icon=";

            int searchFrom = 0;

            while (searchFrom < text.Length)
            {
                int start = text.IndexOf(prefix, searchFrom, StringComparison.Ordinal);
                if (start < 0)
                    break;

                int cursor = start + prefix.Length;
                if (cursor >= text.Length)
                    break;

                bool quoted = text[cursor] == '"';
                if (quoted)
                    cursor++;

                int nameStart = cursor;
                while (cursor < text.Length && text[cursor] != '>' && text[cursor] != '/')
                {
                    if (quoted && text[cursor] == '"')
                        break;
                    cursor++;
                }

                if (cursor >= text.Length)
                    break;

                string iconName = text.Substring(nameStart, cursor - nameStart);

                if (quoted && cursor < text.Length && text[cursor] == '"')
                    cursor++;

                if (cursor < text.Length && text[cursor] == '/')
                    cursor++;

                if (cursor >= text.Length || text[cursor] != '>')
                {
                    searchFrom = cursor;
                    continue;
                }

                int tagEnd = cursor + 1;

                if (MaterialIcons.ICONS_MAP.TryGetValue(iconName, out string icon))
                {
                    var processed = $"<font=\"MaterialIcons\">{icon}</font>";
                    text = string.Concat(text[..start], processed, text[tagEnd..]);
                    searchFrom = start + processed.Length;
                }
                else
                {
                    searchFrom = tagEnd;
                }
            }

            return text;
        }
    }
}
