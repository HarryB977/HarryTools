using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Tests {
    /// <summary> Keeps the tests out of the project's Addressables. Localization puts every table in a group of its locale, and would make a new group for a locale the project does not have </summary>
    internal static class TestAddressables {
        // ==================== METHODS ====================
        #region Methods
        /// <summary> Adds a table of a locale the project does not have, without Addressables: the table is created as an asset next to the collection and put in its table list directly. Never save the collection while it holds the table: importing it would add the table to Addressables and try to create the locale </summary>
        internal static StringTable AddForeignTable(StringTableCollection _collection, LocaleIdentifier _locale) {
            Assert.IsNull(LocalizationEditorSettings.GetLocale(_locale.Code), "not a project locale");

            StringTable _table = ScriptableObject.CreateInstance<StringTable>();
            _table.SharedData = _collection.SharedData;
            _table.LocaleIdentifier = _locale;
            string _folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(_collection)).Replace('\\', '/');
            AssetDatabase.CreateAsset(_table, _folder + "/" + _collection.TableCollectionName + "_" + _locale.Code + ".asset");

            FieldInfo _field = typeof(LocalizationTableCollection).GetField("m_Tables", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_field, "LocalizationTableCollection.m_Tables");
            ((List<LazyLoadReference<LocalizationTable>>)_field.GetValue(_collection)).Add(new LazyLoadReference<LocalizationTable>(_table));
            Assert.AreSame(_table, _collection.GetTable(_locale));
            AddressableAssetSettings _settings = AddressableAssetSettingsDefaultObject.Settings;
            if (_settings != null) Assert.IsNull(_settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_table))), "the table is not in Addressables");
            return _table;
        }

        /// <summary> Writes the Addressables groups the run left dirty. A save made during a test writes the entries of its temporary tables; deleting those tables only drops the entries in memory, so the files keep them until the groups are saved again </summary>
        internal static void SaveDirtyGroups() {
            AddressableAssetSettings _settings = AddressableAssetSettingsDefaultObject.Settings;
            if (_settings == null) return;
            foreach (AddressableAssetGroup _group in _settings.groups) {
                if (_group != null) AssetDatabase.SaveAssetIfDirty(_group);
            }
        }
        #endregion
    }

    /// <summary> Runs once around every EditMode test of the package </summary>
    [SetUpFixture]
    public class DialogueTestsRun {
        [OneTimeTearDown]
        public void OneTimeTearDown() {
            TestAddressables.SaveDirtyGroups();
        }
    }
}
