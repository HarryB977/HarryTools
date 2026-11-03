using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.UI {
    /// <summary> Sets de iconos disponibles para &lt;icon=...&gt;. El del paquete está en Resources/HarryTools.UI/IconLibrary </summary>
    [CreateAssetMenu(menuName = "HarryTools/UI/Icon Library", order = 511)]
    public class IconLibrary : ScriptableObject {
        public const string ResourcePath = "HarryTools.UI/IconLibrary";

        // ==================== VARIABLES ===================
        #region Properties
        public static IconLibrary Instance => instance ? instance : instance = Resources.Load<IconLibrary>(ResourcePath);

        public IReadOnlyList<IconSet> Sets => sets;
        /// <summary> Set usado cuando la etiqueta no lleva prefijo </summary>
        public IconSet DefaultSet => defaultSet;
        #endregion

        #region Private Variables
        [SerializeField] private IconSet defaultSet;
        [SerializeField] private List<IconSet> sets = new();

        private static IconLibrary instance;
        #endregion

        // ==================== METHODS ====================
        /// <summary> "nombre" busca en el set por defecto; "set:nombre" en el set con ese id </summary>
        public bool TryResolve(string _tagValue, out IconSet _set, out IconGlyph _glyph) {
            _set = null;
            _glyph = default;
            if (string.IsNullOrEmpty(_tagValue)) return false;

            int _colon = _tagValue.IndexOf(':');
            string _name = _tagValue;

            if (_colon >= 0) {
                _set = FindSet(_tagValue[.._colon]);
                _name = _tagValue[(_colon + 1)..];
            }
            else {
                _set = defaultSet;
            }

            return _set && _set.TryGetGlyph(_name, out _glyph);
        }

        public IconSet FindSet(string _id) {
            foreach (IconSet _set in sets) {
                if (_set && string.Equals(_set.Id, _id, StringComparison.OrdinalIgnoreCase)) return _set;
            }

            return null;
        }

        internal void SetSets(IconSet _defaultSet, List<IconSet> _sets) {
            defaultSet = _defaultSet;
            sets = _sets ?? new List<IconSet>();
        }
    }
}
