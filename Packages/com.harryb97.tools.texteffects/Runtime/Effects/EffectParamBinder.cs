using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Sets serialized fields of an object from name and value strings </summary>
    internal static class EffectParamBinder {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> fieldCache = new();
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        public static bool TryApply(object _target, string _name, string _value, out string _error) {
            _error = null;
            if (_target == null) {
                _error = "No target to apply the parameter to";
                return false;
            }

            Dictionary<string, FieldInfo> fields = GetFields(_target.GetType());
            if (!fields.TryGetValue(_name ?? string.Empty, out FieldInfo field)) {
                _error = $"Unknown parameter '{_name}' for {_target.GetType().Name}";
                return false;
            }

            if (!TryParse(field.FieldType, _value, out object parsed)) {
                _error = $"Invalid value '{_value}' for parameter '{_name}' ({field.FieldType.Name})";
                return false;
            }

            field.SetValue(_target, parsed);
            return true;
        }
        #endregion

        #region Private Methods
        private static Dictionary<string, FieldInfo> GetFields(Type _type) {
            lock (fieldCache) {
                if (fieldCache.TryGetValue(_type, out Dictionary<string, FieldInfo> cached)) return cached;

                Dictionary<string, FieldInfo> result = new(StringComparer.OrdinalIgnoreCase);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (Type type = _type; type != null && type != typeof(object); type = type.BaseType) {
                    foreach (FieldInfo field in type.GetFields(flags)) {
                        bool exposed = field.IsPublic || field.IsDefined(typeof(SerializeField), true);
                        if (!exposed || field.IsInitOnly) continue;
                        if (!result.ContainsKey(field.Name)) result.Add(field.Name, field);
                    }
                }

                fieldCache.Add(_type, result);
                return result;
            }
        }

        private static bool TryParse(Type _type, string _value, out object _result) {
            _result = null;
            if (_value == null) return false;
            string value = _value.Trim();

            if (_type == typeof(float)) {
                if (!TryFloat(value, out float f)) return false;
                _result = f;
                return true;
            }

            if (_type == typeof(int)) {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return false;
                _result = i;
                return true;
            }

            if (_type == typeof(bool)) {
                if (!bool.TryParse(value, out bool b)) return false;
                _result = b;
                return true;
            }

            if (_type == typeof(Color)) {
                if (!ColorUtility.TryParseHtmlString(value, out Color c)) return false;
                _result = c;
                return true;
            }

            if (_type == typeof(Vector2) || _type == typeof(Vector3)) {
                return TryVector(_type, value, out _result);
            }

            if (_type.IsEnum) {
                if (value.Length == 0 || char.IsDigit(value[0]) || value[0] == '-') return false;
                if (!Enum.TryParse(_type, value, true, out object e) || !Enum.IsDefined(_type, e)) return false;
                _result = e;
                return true;
            }

            return false;
        }

        private static bool TryFloat(string _value, out float _result) {
            return float.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out _result);
        }

        private static bool TryVector(Type _type, string _value, out object _result) {
            _result = null;
            string[] parts = _value.Split(',');
            bool isVector3 = _type == typeof(Vector3);
            if (parts.Length < 2 || parts.Length > (isVector3 ? 3 : 2)) return false;

            float[] values = new float[3];
            for (int i = 0; i < parts.Length; i++) {
                if (!TryFloat(parts[i].Trim(), out values[i])) return false;
            }

            if (isVector3) _result = new Vector3(values[0], values[1], values[2]);
            else _result = new Vector2(values[0], values[1]);
            return true;
        }
        #endregion
    }
}
