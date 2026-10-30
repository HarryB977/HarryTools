using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization;
using UnityEngine;

using Cysharp.Threading.Tasks;
using HarryTools.DomainReload;

using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Localization {
    public static class LocalizationSystem {
        // ==================== VARIABLES ===================
        #region Properties
        public static int GetCurrentLocaleIndex => Array.IndexOf(AllLocales, CurrentLocale);
        #endregion

        #region Public Variables
        [ClearOnReload] public static Action OnInitalize;
        [ClearOnReload] public static Action OnSelectedLocaleChanged;

        public static bool Initialized { get; private set; }
        public static Locale CurrentLocale { get; private set; }

        public static Locale[] AllLocales { get; private set; }
        #endregion
        
        #region Private Variables
        private static List<StringTable> AllStringTables;
        private static List<AssetTable> AllAssetTables;
        #endregion

        // ==================== RESET ====================
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            LocalizationSettings.SelectedLocaleChanged -= SelectedLocaleChanged;

            Initialized = false;
            CurrentLocale = null;
            AllLocales = null;
            AllStringTables = null;
            AllAssetTables = null;

            OnInitalize = null;
            OnSelectedLocaleChanged = null;
        }
#endif

        // ==================== METHODS ====================
        public static async UniTask Initialize(string _firstLocaleCode = "") {
            Initialized = false;

#if UNITY_EDITOR0
            // Reseteamos para forzar que se reejecuten los Startup Selectors.
            LocalizationSettings.Instance.ResetState();
#endif

            await LocalizationSettings.InitializationOperation;

            // Recogemos todos los locales que hay en el juego
            AllLocales = LocalizationSettings.AvailableLocales.Locales.ToArray();

            // Si se especifica el identificador del local, se carga
            if (!string.IsNullOrEmpty(_firstLocaleCode)) ChangeLocale(_firstLocaleCode);
            
            AllStringTables = (List<StringTable>)await LocalizationSettings.StringDatabase.GetAllTables();
            AllAssetTables = (List<AssetTable>)await LocalizationSettings.AssetDatabase.GetAllTables();
            CurrentLocale = LocalizationSettings.SelectedLocale;

            LocalizationSettings.SelectedLocaleChanged -= SelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += SelectedLocaleChanged;

            Initialized = true;
            OnInitalize?.Invoke();
        }

        private static async void SelectedLocaleChanged(Locale _locale) {
            AllStringTables = (List<StringTable>)await LocalizationSettings.StringDatabase.GetAllTables(_locale);
            AllAssetTables = (List<AssetTable>)await LocalizationSettings.AssetDatabase.GetAllTables();
            CurrentLocale = _locale;

            OnSelectedLocaleChanged?.Invoke();
        }

        // =================================================

        public static void ChangeLocale(Locale _locale) {
            LocalizationSettings.SelectedLocale = _locale;
        }

        public static void ChangeLocale(string _code) {
            LocalizationSettings.SelectedLocale = AllLocales.FirstOrDefault(l => l.Identifier.Code == _code);
        }

        public static void ChangeLocale(int _index) {
            LocalizationSettings.SelectedLocale = AllLocales[_index];
        }
        
        // =================================================

        /// <summary> Devuelve la cantidad de strings cuyas 'key' contengan la palabra que se le pase como parámetro </summary>
        public static int GetStringsCount(string _contains) {
            int _count = 0;
            for (int i = 0; i < AllStringTables.Count; i++) {
                foreach (StringTableEntry _entry in AllStringTables[i].Values) {
                    if (_entry.Key.Contains(_contains)) _count++;
                }
            }
            
            return _count;
        }

        public static string GetString(string _keyId) {
            try {
                for (int i = 0; i < AllStringTables.Count; i++) {
                    foreach (StringTableEntry _entry in AllStringTables[i].Values) {
                        if (_entry.Key == _keyId) return _entry.LocalizedValue;
                    }
                }

            } catch (Exception _exc) { Logger.LogError("Localization", _exc.Message); }

            return "";
        }

        public static string GetString(string _keyId, params object[] _args) {
            try {
                for (int i = 0; i < AllStringTables.Count; i++) {
                    foreach (StringTableEntry _entry in AllStringTables[i].Values) {
                        if (_entry.Key == _keyId) return _entry.GetLocalizedString(_args);
                    }
                }

            } catch (Exception _exc) { Logger.LogError("Localization", _exc.Message); }

            return "";
        }

        public static async UniTask<Sprite> GetSprite(string _keyId) {
            try {
                for (int i = 0; i < AllAssetTables.Count; i++) {
                    foreach (AssetTableEntry _entry in AllAssetTables[i].Values) {
                        if (_entry.Key != _keyId) continue;

                        return await Addressables.LoadAssetAsync<Sprite>(_entry.Address);
                    }
                }
                
            } catch (Exception _exc) { Logger.LogError("Localization", _exc.Message); }

            return null;
        }

        // =================================================

        public static string GetLocalizedStringKey(LocalizedString _localizedString) {
            return LocalizationSettings.StringDatabase.GetTableEntry(_localizedString.TableReference, _localizedString.TableEntryReference).Entry.SharedEntry.Key;
        }
    }
}