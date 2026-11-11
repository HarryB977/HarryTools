using System;
using System.Text;

namespace HarryTools.SceneRefs
{
    /// <summary>
    /// Naming rules for scene ids: sanitizing names, incrementing trailing numbers and finding free ids.
    /// </summary>
    public static class SceneIdNaming
    {
        // ==================== VARIABLES ====================

        private const string FallbackId = "Object";

        // ==================== PROPERTIES ====================

        /// <summary>Comparer used for every id comparison (case insensitive).</summary>
        public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

        // ==================== METHODS ====================

        /// <summary>Creates a new internal guid as 32 lowercase hex characters.</summary>
        public static string NewGuid() => System.Guid.NewGuid().ToString("N");

        /// <summary>
        /// Keeps only letters, digits, '_' and '-'. Any other run of characters becomes a single '_'.
        /// Leading and trailing '_' are trimmed; an empty result becomes "Object".
        /// </summary>
        public static string Sanitize(string _name)
        {
            if (string.IsNullOrEmpty(_name)) return FallbackId;

            StringBuilder _builder = new StringBuilder(_name.Length);
            bool _pendingSeparator = false;
            foreach (char _c in _name)
            {
                bool _valid = (_c >= 'a' && _c <= 'z') || (_c >= 'A' && _c <= 'Z') || (_c >= '0' && _c <= '9') || _c == '-';
                if (_valid)
                {
                    if (_pendingSeparator) _builder.Append('_');
                    _pendingSeparator = false;
                    _builder.Append(_c);
                }
                else
                {
                    _pendingSeparator = true;
                }
            }

            string _result = _builder.ToString().Trim('_');
            return _result.Length == 0 ? FallbackId : _result;
        }

        /// <summary>
        /// Increments the trailing number keeping its width ("Item09" becomes "Item10").
        /// Without a trailing number, appends "1".
        /// </summary>
        public static string Increment(string _id)
        {
            _id ??= string.Empty;

            int _start = _id.Length;
            while (_start > 0 && _id[_start - 1] >= '0' && _id[_start - 1] <= '9') _start--;

            if (_start == _id.Length) return _id + "1";

            // Digit by digit, so a run of any length never overflows a number type.
            char[] _digits = _id.Substring(_start).ToCharArray();
            int _i = _digits.Length - 1;
            while (_i >= 0 && _digits[_i] == '9') _digits[_i--] = '0';

            string _prefix = _id.Substring(0, _start);
            if (_i < 0) return _prefix + "1" + new string(_digits);
            _digits[_i]++;
            return _prefix + new string(_digits);
        }

        /// <summary>Returns _baseId if it is free, otherwise the first incremented id that is free.</summary>
        public static string MakeUnique(string _baseId, Func<string, bool> _isTaken)
        {
            string _candidate = _baseId;
            while (_isTaken(_candidate)) _candidate = Increment(_candidate);
            return _candidate;
        }
    }
}
