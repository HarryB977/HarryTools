using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HarryTools.UI {
    /// <summary> Un icono de un set: nombre, codepoint en la fuente, alias y etiquetas para buscarlo </summary>
    [Serializable]
    public struct IconGlyph {
        public string name;
        public uint codepoint;
        public string[] aliases;
        public string[] tags;

        /// <summary> Carácter que dibuja el icono con la fuente del set </summary>
        public string Character => char.ConvertFromUtf32((int)codepoint);
    }

    /// <summary> Un set de iconos: una fuente TMP con glifos y los datos para buscarlos por nombre, alias o etiqueta </summary>
    [CreateAssetMenu(menuName = "HarryTools/UI/Icon Set", order = 510)]
    public class IconSet : ScriptableObject {
        // ==================== VARIABLES ===================
        #region Properties
        /// <summary> Prefijo del set en las etiquetas: &lt;icon=id:nombre&gt; </summary>
        public string Id => id;
        public TMP_FontAsset FontAsset => fontAsset;
        /// <summary> Fuente original, usada por el buscador del editor para dibujar los glifos </summary>
        public Font SourceFont => sourceFont;
        public IReadOnlyList<IconGlyph> Glyphs => glyphs;
        #endregion

        #region Private Variables
        [SerializeField] private string id;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private Font sourceFont;
        [SerializeField] private List<IconGlyph> glyphs = new();

        [NonSerialized] private Dictionary<string, int> lookup;
        #endregion

        // ==================== METHODS ====================
        /// <summary> Busca por nombre o alias, sin distinguir mayúsculas y tratando '-' y '_' como iguales </summary>
        public bool TryGetGlyph(string _nameOrAlias, out IconGlyph _glyph) {
            if (!string.IsNullOrEmpty(_nameOrAlias) && Lookup().TryGetValue(Normalize(_nameOrAlias), out int _index)) {
                _glyph = glyphs[_index];
                return true;
            }

            _glyph = default;
            return false;
        }

        internal void SetData(string _id, TMP_FontAsset _font, Font _source, List<IconGlyph> _glyphs) {
            id = _id;
            fontAsset = _font;
            sourceFont = _source;
            glyphs = _glyphs ?? new List<IconGlyph>();
            lookup = null;
        }

        internal static string Normalize(string _value) => _value.Trim().ToLowerInvariant().Replace('-', '_');

        private Dictionary<string, int> Lookup() {
            if (lookup != null) return lookup;

            lookup = new Dictionary<string, int>(glyphs.Count * 2);

            for (int i = 0; i < glyphs.Count; i++) {
                if (!string.IsNullOrEmpty(glyphs[i].name)) lookup[Normalize(glyphs[i].name)] = i;
            }

            // Los alias no pisan a un nombre real
            for (int i = 0; i < glyphs.Count; i++) {
                if (glyphs[i].aliases == null) continue;
                foreach (string _alias in glyphs[i].aliases) {
                    if (!string.IsNullOrEmpty(_alias)) lookup.TryAdd(Normalize(_alias), i);
                }
            }

            return lookup;
        }

        private void OnValidate() => lookup = null;
    }
}
