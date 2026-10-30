using System;

namespace HarryTools.SerializeReferences {
    /// <summary> Cambia la ruta y el nombre con el que aparece el tipo en el desplegable de [TypeSelector] </summary>
    /// <example> [Serializable, TypeMenu("Audio/Play Clip")] public class PlayClip : IAction { } </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TypeMenuAttribute : Attribute {
        // ==================== VARIABLES ===================
        #region Public Variables
        public string MenuName { get; }
        public int Order { get; }
        #endregion

        #region Private Variables
        private static readonly char[] Separators = { '/' };
        #endregion

        // ==================== START ====================
        public TypeMenuAttribute(string _menuName, int _order = 0) {
            MenuName = _menuName;
            Order = _order;
        }

        // ==================== METHODS ====================
        /// <summary> Devuelve la ruta separada por '/' </summary>
        public string[] GetSplitMenuName() {
            return string.IsNullOrWhiteSpace(MenuName) ? Array.Empty<string>() : MenuName.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary> Devuelve el nombre sin la ruta </summary>
        public string GetNameWithoutPath() {
            string[] _split = GetSplitMenuName();
            return _split.Length != 0 ? _split[^1] : null;
        }
    }
}
