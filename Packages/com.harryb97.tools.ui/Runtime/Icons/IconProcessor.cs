using System;
using TMPro;
using UnityEngine;

namespace HarryTools.UI
{
    /// <summary>
    /// Sustituye &lt;icon=nombre&gt; o &lt;icon=set:nombre&gt; por el glifo del set de iconos en el texto TMP.
    /// </summary>
    [ExecuteInEditMode]
    public class IconProcessor : MonoBehaviour, ITextPreprocessor
    {
        [SerializeField] private TMP_Text _text;
        [Tooltip("Biblioteca de sets de iconos. Si está vacía, se usa la del paquete")]
        [SerializeField] private IconLibrary _library;

        /// <summary> Biblioteca usada para resolver los iconos (la del paquete si no se asigna otra) </summary>
        public IconLibrary Library
        {
            get => _library ? _library : IconLibrary.Instance;
            set
            {
                _library = value;
                if (_text) _text.SetAllDirty();
            }
        }

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

                var library = Library;
                if (library && library.TryResolve(iconName, out var set, out var glyph) && set.FontAsset)
                {
                    RegisterFont(set.FontAsset);
                    var processed = $"<font=\"{set.FontAsset.name}\">{glyph.Character}</font>";
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
    
        /// <summary> TMP resuelve &lt;font="nombre"&gt; buscando primero en MaterialReferenceManager </summary>
        private static void RegisterFont(TMP_FontAsset font)
        {
            if (!MaterialReferenceManager.TryGetFontAsset(font.hashCode, out _))
                MaterialReferenceManager.AddFontAsset(font);
        }
    }
}
