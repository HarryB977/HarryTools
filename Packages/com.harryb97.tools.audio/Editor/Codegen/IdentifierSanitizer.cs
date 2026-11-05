using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HarryTools.Audio.Editor {
    /// <summary> Turns asset and folder names into valid C# identifiers. </summary>
    internal static class IdentifierSanitizer {
        // ==================== VARIABLES ===================
        private static readonly HashSet<string> Keywords = new HashSet<string> {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        };

        // ==================== PUBLIC ===================
        /// <summary> Several words become PascalCase, a single word keeps its casing. Accents are removed. </summary>
        public static string ToIdentifier(string _name) {
            if (string.IsNullOrEmpty(_name)) return "_";
            string _decomposed = _name.Normalize(NormalizationForm.FormD);

            var _words = new List<StringBuilder>();
            StringBuilder _current = null;
            for (int _i = 0; _i < _decomposed.Length; _i++) {
                char _c = _decomposed[_i];
                if (CharUnicodeInfo.GetUnicodeCategory(_c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(_c) || _c == '_') {
                    if (_current == null) {
                        _current = new StringBuilder();
                        _words.Add(_current);
                    }
                    _current.Append(_c);
                } else {
                    _current = null;
                }
            }
            if (_words.Count == 0) return "_";

            var _result = new StringBuilder();
            for (int _i = 0; _i < _words.Count; _i++) {
                string _word = _words[_i].ToString();
                if (_words.Count > 1) _result.Append(char.ToUpperInvariant(_word[0])).Append(_word, 1, _word.Length - 1);
                else _result.Append(_word);
            }

            string _identifier = _result.ToString();
            if (char.IsDigit(_identifier[0])) return "_" + _identifier;
            if (Keywords.Contains(_identifier)) return "@" + _identifier;
            return _identifier;
        }
    }
}
