using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace HarryTools.SerializeReferences.Editor {
    /// <summary> Nombres y rutas con los que se muestran los tipos en el desplegable </summary>
    public static class TypeMenuUtility {
        // ==================== VARIABLES ===================
        #region Public Variables
        public const string NullDisplayName = "<Null>";
        #endregion

        #region Private Variables
        private static readonly Dictionary<Type, string> DisplayNamesCache = new();
        #endregion

        // ==================== METHODS ====================
        public static TypeMenuAttribute GetAttribute(Type _type) {
            return _type == null ? null : Attribute.GetCustomAttribute(_type, typeof(TypeMenuAttribute)) as TypeMenuAttribute;
        }

        /// <summary> Ruta del tipo en el desplegable. Si no tiene [TypeMenu], se usa su namespace </summary>
        public static string[] GetSplitTypePath(Type _type) {
            TypeMenuAttribute _typeMenu = GetAttribute(_type);
            if (_typeMenu != null) return _typeMenu.GetSplitMenuName();

            string _fullName = _type.FullName ?? _type.Name;
            int _splitIndex = _fullName.LastIndexOf('.');

            return _splitIndex >= 0
                ? new[] { _fullName[.._splitIndex], _fullName[(_splitIndex + 1)..].Replace('+', '.') }
                : new[] { _type.Name };
        }

        /// <summary> Nombre legible del tipo, sin la ruta </summary>
        public static string GetDisplayName(Type _type) {
            if (_type == null) return NullDisplayName;
            if (DisplayNamesCache.TryGetValue(_type, out string _cached)) return _cached;

            string _name = GetAttribute(_type)?.GetNameWithoutPath();
            if (string.IsNullOrWhiteSpace(_name)) _name = _type.Name;

            _name = ObjectNames.NicifyVariableName(_name);
            DisplayNamesCache[_type] = _name;

            return _name;
        }

        public static IEnumerable<Type> OrderByTypeMenu(this IEnumerable<Type> _source) {
            return _source
                .OrderBy(_type => _type == null ? -999 : GetAttribute(_type)?.Order ?? 0)
                .ThenBy(_type => _type == null ? null : GetAttribute(_type)?.MenuName ?? _type.Name);
        }
    }
}
