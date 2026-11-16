#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace HarryTools.TextEffects.Editor {
    /// <summary> Menu that validates the markup of every string table collection in the project </summary>
    internal static class StringTableValidationMenu {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string MenuPath = "Tools/HarryTools/Text Effects/Validate String Tables";
        #endregion

        // ==================== METHODS ====================
        #region Menu
        [MenuItem(MenuPath)]
        private static void Validate() {
            TextEffectsSettings settings = TextEffectsSettingsLocator.Find();
            if (settings == null) Debug.LogWarning("Text Effects settings not found: every preset and voice will be reported as unknown.");

            int entryCount = 0;
            int errorCount = 0;
            int warningCount = 0;
            List<LocaleIssue> issues = new List<LocaleIssue>();
            Dictionary<string, string> textByLocale = new Dictionary<string, string>();

            foreach (StringTableCollection collection in LocalizationEditorSettings.GetStringTableCollections()) {
                IReadOnlyList<StringTable> tables = collection.StringTables;
                string baseLocale = FindBaseLocale(tables);

                foreach (SharedTableData.SharedTableEntry shared in collection.SharedData.Entries) {
                    textByLocale.Clear();
                    for (int i = 0; i < tables.Count; i++) {
                        if (tables[i] == null) continue;
                        textByLocale[tables[i].LocaleIdentifier.Code] = tables[i].GetEntry(shared.Id)?.Value;
                    }

                    issues.Clear();
                    TextValidator.ValidateLocales(baseLocale, textByLocale, settings, issues);
                    entryCount++;

                    for (int i = 0; i < issues.Count; i++) {
                        string message = $"[{collection.TableCollectionName}/{shared.Key}] ({issues[i].Locale}) {issues[i].Issue.Message}";
                        if (issues[i].Issue.Severity == IssueSeverity.Error) {
                            errorCount++;
                            Debug.LogError(message, collection);
                        } else {
                            warningCount++;
                            Debug.LogWarning(message, collection);
                        }
                    }
                }
            }

            Debug.Log($"Text Effects: validated {entryCount} string table entries, {errorCount} errors, {warningCount} warnings.");
        }
        #endregion

        #region Helpers
        /// <summary> The project locale when the collection has a table for it, otherwise the first table's locale </summary>
        private static string FindBaseLocale(IReadOnlyList<StringTable> _tables) {
            string first = null;
            string project = LocalizationSettings.ProjectLocale?.Identifier.Code;
            for (int i = 0; i < _tables.Count; i++) {
                if (_tables[i] == null) continue;
                string code = _tables[i].LocaleIdentifier.Code;
                if (first == null) first = code;
                if (code == project) return code;
            }
            return first;
        }
        #endregion
    }
}
#endif
