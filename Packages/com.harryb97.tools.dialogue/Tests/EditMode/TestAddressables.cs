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
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    /// <summary> Keeps the tests out of the project's Addressables. Localization puts every table in a group of its locale, and would make a new group for a locale the project does not have </summary>
    internal static class TestAddressables {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static readonly HashSet<AddressableAssetGroup> dirtyBefore = new HashSet<AddressableAssetGroup>();
        private static readonly Dictionary<AddressableAssetGroup, System.DateTime> writtenBefore = new Dictionary<AddressableAssetGroup, System.DateTime>();
        #endregion

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

        /// <summary> Records the Addressables groups that were already dirty before the run, and when each group file was last written </summary>
        internal static void RecordDirtyGroups() {
            dirtyBefore.Clear();
            writtenBefore.Clear();
            AddressableAssetSettings _settings = AddressableAssetSettingsDefaultObject.Settings;
            if (_settings == null) return;
            foreach (AddressableAssetGroup _group in _settings.groups) {
                if (_group == null) continue;
                if (EditorUtility.IsDirty(_group)) dirtyBefore.Add(_group);
                writtenBefore[_group] = LastWrite(_group);
            }
        }

        /// <summary> Writes the Addressables groups the run left dirty. A group that was already dirty before the run is left alone, unless its file was written during the run: something in the run then saved it with the entries of temporary tables, and deleting those tables only drops the entries in memory, so the file keeps them until the group is saved again </summary>
        internal static void SaveDirtyGroups() {
            AddressableAssetSettings _settings = AddressableAssetSettingsDefaultObject.Settings;
            if (_settings == null) return;
            foreach (AddressableAssetGroup _group in _settings.groups) {
                if (_group == null) continue;
                bool _written = !writtenBefore.TryGetValue(_group, out System.DateTime _before) || LastWrite(_group) != _before;
                if (!dirtyBefore.Contains(_group) || _written) AssetDatabase.SaveAssetIfDirty(_group);
            }
            dirtyBefore.Clear();
            writtenBefore.Clear();
        }

        /// <summary> Writes the given assets and the shared data and tables of the collection, and nothing else of the project </summary>
        internal static void SaveOwn(StringTableCollection _collection, params Object[] _assets) {
            for (int _i = 0; _i < _assets.Length; _i++) {
                if (_assets[_i] != null) AssetDatabase.SaveAssetIfDirty(_assets[_i]);
            }
            if (_collection == null) return;
            if (_collection.SharedData != null) AssetDatabase.SaveAssetIfDirty(_collection.SharedData);
            foreach (StringTable _table in _collection.StringTables) {
                if (_table != null) AssetDatabase.SaveAssetIfDirty(_table);
            }
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static System.DateTime LastWrite(AddressableAssetGroup _group) {
            string _path = AssetDatabase.GetAssetPath(_group);
            return string.IsNullOrEmpty(_path) || !File.Exists(_path) ? default : File.GetLastWriteTimeUtc(_path);
        }
        #endregion
    }

    /// <summary> Runs once around every EditMode test of the package </summary>
    [SetUpFixture]
    public class DialogueTestsRun {
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            TestAddressables.RecordDirtyGroups();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() {
            TestAddressables.SaveDirtyGroups();
        }
    }
}
