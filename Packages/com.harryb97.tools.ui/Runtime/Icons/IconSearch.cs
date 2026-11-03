using System;
using System.Collections.Generic;

namespace HarryTools.UI {
    /// <summary> Búsqueda de iconos por nombre, alias y etiquetas, ordenada por relevancia </summary>
    public static class IconSearch {
        // ==================== METHODS ====================
        /// <summary>
        /// Orden: nombre exacto, nombre que empieza por la búsqueda, alias, etiqueta y nombre que la contiene.
        /// Los empates se ordenan alfabéticamente. Una búsqueda vacía devuelve todos los iconos.
        /// </summary>
        public static List<(IconSet set, IconGlyph glyph)> Find(IEnumerable<IconSet> _sets, string _query, string _setId = null, int _max = 500) {
            var _ranked = new List<(int rank, IconSet set, IconGlyph glyph)>();
            string _q = string.IsNullOrWhiteSpace(_query) ? string.Empty : IconSet.Normalize(_query);

            foreach (IconSet _set in _sets) {
                if (!_set) continue;
                if (!string.IsNullOrEmpty(_setId) && !string.Equals(_set.Id, _setId, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (IconGlyph _glyph in _set.Glyphs) {
                    int _rank = Rank(_glyph, _q);
                    if (_rank >= 0) _ranked.Add((_rank, _set, _glyph));
                }
            }

            _ranked.Sort((_a, _b) => _a.rank != _b.rank
                ? _a.rank.CompareTo(_b.rank)
                : string.CompareOrdinal(_a.glyph.name, _b.glyph.name));

            var _results = new List<(IconSet, IconGlyph)>(Math.Min(_ranked.Count, _max));
            for (int i = 0; i < _ranked.Count && i < _max; i++) _results.Add((_ranked[i].set, _ranked[i].glyph));
            return _results;
        }

        /// <summary> 0 exacto, 1 prefijo, 2 alias, 3 etiqueta, 4 contiene, -1 no coincide </summary>
        private static int Rank(IconGlyph _glyph, string _q) {
            if (_q.Length == 0) return 0;

            string _name = IconSet.Normalize(_glyph.name ?? string.Empty);
            if (_name == _q) return 0;
            if (_name.StartsWith(_q, StringComparison.Ordinal)) return 1;

            if (_glyph.aliases != null) {
                foreach (string _alias in _glyph.aliases) {
                    if (!string.IsNullOrEmpty(_alias) && IconSet.Normalize(_alias).StartsWith(_q, StringComparison.Ordinal)) return 2;
                }
            }

            if (_glyph.tags != null) {
                foreach (string _tag in _glyph.tags) {
                    if (!string.IsNullOrEmpty(_tag) && IconSet.Normalize(_tag).Contains(_q)) return 3;
                }
            }

            return _name.Contains(_q) ? 4 : -1;
        }
    }
}
