using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Localization;

namespace HarryTools.Dialogue.Tests {
    /// <summary> Keeps the tests away from the project's locales. Tests never create a locale or localization settings: importing a locale makes Localization add it and raise LocaleAdded, which reaches every collection of the project, and adding or removing one rewrites the project's locale group and build settings </summary>
    internal static class TestLocales {
        // ==================== METHODS ====================
        #region Methods
        /// <summary> Ignores the calling fixture unless the project has active localization settings with every one of the locales </summary>
        internal static void RequireOrIgnore(params string[] _codes) {
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null) Assert.Ignore("These tests need the project's localization settings. Tests never create them, so they are skipped.");

            List<string> _missing = new List<string>();
            for (int _i = 0; _i < _codes.Length; _i++) {
                if (LocalizationEditorSettings.GetLocale(_codes[_i]) == null) _missing.Add(_codes[_i]);
            }
            if (_missing.Count > 0) Assert.Ignore("These tests need the project locales " + string.Join(", ", _codes) + " and the project lacks " + string.Join(", ", _missing) + ". Tests never add locales, so they are skipped.");
        }
        #endregion
    }
}
