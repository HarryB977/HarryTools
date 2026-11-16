using System.Collections.Generic;
using UnityEditor;

namespace HarryTools.TextEffects.Editor {
    /// <summary> Keeps the <see cref="TextEffectsSettings"/> lists in sync with the presets and voices in the project. Never creates the settings asset </summary>
    internal sealed class TextEffectsRegistryMaintainer : AssetPostprocessor {
        // ==================== METHODS ====================
        #region Methods
        /// <summary> Makes the settings hold exactly these presets and voices: keeps the ones still present in order, drops the rest, appends the new ones </summary>
        internal static void Sync(TextEffectsSettings _settings, IEnumerable<TextEffectPreset> _presets, IEnumerable<TypingVoice> _voices) {
            if (_settings == null) return;

            List<TextEffectPreset> _wantedPresets = Distinct(_presets);
            List<TypingVoice> _wantedVoices = Distinct(_voices);
            List<TextEffectPreset> _newPresets = Merge(_settings.PresetList, _wantedPresets);
            List<TypingVoice> _newVoices = Merge(_settings.VoiceList, _wantedVoices);
            if (SameItems(_settings.PresetList, _newPresets) && SameItems(_settings.VoiceList, _newVoices)) return;

            Undo.RecordObject(_settings, "Update Text Effects Registry");
            _settings.PresetList.Clear();
            _settings.PresetList.AddRange(_newPresets);
            _settings.VoiceList.Clear();
            _settings.VoiceList.AddRange(_newVoices);
            _settings.Invalidate();
            EditorUtility.SetDirty(_settings);
        }

        internal static List<TextEffectPreset> FindAllPresets() => FindAll<TextEffectPreset>();

        internal static List<TypingVoice> FindAllVoices() => FindAll<TypingVoice>();

        /// <summary> Drops the empty settings that runtime reads cached when one of the paths is a settings asset </summary>
        internal static void ForgetFallbackOnSettingsImport(string[] _paths) {
            for (int i = 0; i < _paths.Length; i++) {
                if (!_paths[i].EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (AssetDatabase.GetMainAssetTypeAtPath(_paths[i]) != typeof(TextEffectsSettings)) continue;
                TextEffectsSettings.ForgetFallback();
                return;
            }
        }
        #endregion

        // ==================== POSTPROCESSOR ====================
        #region Postprocessor
        private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom) {
            if (!AnyAssetFile(_imported) && !AnyAssetFile(_deleted) && !AnyAssetFile(_moved)) return;

            ForgetFallbackOnSettingsImport(_imported);
            ForgetFallbackOnSettingsImport(_moved);

            TextEffectsSettings _settings = TextEffectsSettingsLocator.Find();
            if (_settings == null) return;

            Sync(_settings, FindAllPresets(), FindAllVoices());
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static bool AnyAssetFile(string[] _paths) {
            for (int i = 0; i < _paths.Length; i++) {
                if (_paths[i].EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static List<T> FindAll<T>() where T : UnityEngine.Object {
            List<T> _all = new List<T>();
            foreach (string _guid in AssetDatabase.FindAssets("t:" + typeof(T).Name)) {
                T _asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_asset != null) _all.Add(_asset);
            }
            return _all;
        }

        private static List<T> Distinct<T>(IEnumerable<T> _items) where T : UnityEngine.Object {
            List<T> _result = new List<T>();
            if (_items == null) return _result;
            foreach (T _item in _items) {
                if (_item != null && !_result.Contains(_item)) _result.Add(_item);
            }
            return _result;
        }

        private static List<T> Merge<T>(List<T> _current, List<T> _wanted) where T : UnityEngine.Object {
            List<T> _result = new List<T>();
            for (int i = 0; i < _current.Count; i++) {
                if (_current[i] != null && _wanted.Contains(_current[i]) && !_result.Contains(_current[i])) _result.Add(_current[i]);
            }
            for (int i = 0; i < _wanted.Count; i++) {
                if (!_result.Contains(_wanted[i])) _result.Add(_wanted[i]);
            }
            return _result;
        }

        private static bool SameItems<T>(List<T> _a, List<T> _b) where T : UnityEngine.Object {
            if (_a.Count != _b.Count) return false;
            for (int i = 0; i < _a.Count; i++) {
                if (_a[i] != _b[i]) return false;
            }
            return true;
        }
        #endregion
    }
}
