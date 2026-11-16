using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.TextEffects {
    /// <summary> Project-wide registry of effect presets and typing voices, plus the default text settings </summary>
    public sealed class TextEffectsSettings : ScriptableObject, ITagLookup {
        // ==================== VARIABLES ===================
        #region Public Variables
        public const string ResourcesName = "TextEffectsSettings";

        public static TextEffectsSettings Current {
            get {
                if (current == null) current = LoadOrEmpty(() => Resources.Load<TextEffectsSettings>(ResourcesName));
                return current;
            }
        }

        public IReadOnlyList<TextIssue> RegistryIssues {
            get {
                EnsureBuilt();
                return issues;
            }
        }

        public TextEffectPreset DefaultAppear => defaultAppear;
        public TextEffectPreset DefaultDisappear => defaultDisappear;
        public bool DefaultUnscaledTime => defaultUnscaledTime;
        public float DefaultCharactersPerSecond => defaultCharactersPerSecond;
        #endregion

        #region Private Variables
        [ClearOnReload] private static TextEffectsSettings current;

        [Tooltip("Every effect preset available in the markup"), SerializeField]
        private List<TextEffectPreset> presets = new List<TextEffectPreset>();

        [Tooltip("Every typing voice available in the markup"), SerializeField]
        private List<TypingVoice> voices = new List<TypingVoice>();

        [Tooltip("Appear effect used by texts that do not set their own"), SerializeField]
        private TextEffectPreset defaultAppear;

        [Tooltip("Disappear effect used by texts that do not set their own"), SerializeField]
        private TextEffectPreset defaultDisappear;

        [Tooltip("Texts advance with unscaled time by default"), SerializeField]
        private bool defaultUnscaledTime = false;

        [Tooltip("Typewriter speed used by texts that do not set their own"), SerializeField, Min(0.01f)]
        private float defaultCharactersPerSecond = 40f;

        private readonly Dictionary<string, TextEffectPreset> presetByName = new Dictionary<string, TextEffectPreset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TypingVoice> voiceByName = new Dictionary<string, TypingVoice>(StringComparer.OrdinalIgnoreCase);
        private readonly List<TextIssue> issues = new List<TextIssue>();
        private bool built;
        #endregion

        // ==================== INTERNAL ====================
        #region Internal
        internal List<TextEffectPreset> PresetList => presets;
        internal List<TypingVoice> VoiceList => voices;

        internal TextEffectPreset DefaultAppearAsset {
            get => defaultAppear;
            set => defaultAppear = value;
        }

        internal TextEffectPreset DefaultDisappearAsset {
            get => defaultDisappear;
            set => defaultDisappear = value;
        }

        /// <summary> Loads the settings with the given function; when it returns nothing, warns and returns an empty instance </summary>
        internal static TextEffectsSettings LoadOrEmpty(Func<TextEffectsSettings> _load) {
            TextEffectsSettings _loaded = _load();
            if (_loaded != null) return _loaded;

            Logger.LogWarning("TextEffects", "No TextEffectsSettings asset found in a Resources folder, so no presets or voices are registered. Create it from Project Settings > HarryTools > Text Effects.");
            TextEffectsSettings _empty = CreateInstance<TextEffectsSettings>();
            _empty.hideFlags = HideFlags.DontSave;
            return _empty;
        }

        internal static void UseForTests(TextEffectsSettings _settings) {
            current = _settings;
        }

        internal static void ClearForTests() {
            current = null;
        }

        /// <summary> Rebuilds the name lookups and issues the next time they are read </summary>
        internal void Invalidate() {
            built = false;
        }
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        public bool TryGetPreset(string _name, out TextEffectPreset _preset) {
            EnsureBuilt();
            if (_name != null) return presetByName.TryGetValue(_name, out _preset);
            _preset = null;
            return false;
        }

        public bool TryGetVoice(string _name, out TypingVoice _voice) {
            EnsureBuilt();
            if (_name != null) return voiceByName.TryGetValue(_name, out _voice);
            _voice = null;
            return false;
        }

        public bool IsPreset(string _name) => TryGetPreset(_name, out _);

        public bool IsVoice(string _name) => TryGetVoice(_name, out _);
        #endregion

        #region Unity Callbacks
        private void OnEnable() {
            Invalidate();
        }

        private void OnValidate() {
            Invalidate();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void EnsureBuilt() {
            if (built) return;
            built = true;
            presetByName.Clear();
            voiceByName.Clear();
            issues.Clear();

            for (int i = 0; i < presets.Count; i++) {
                TextEffectPreset _preset = presets[i];
                if (_preset == null) continue;
                if (CanRegister(_preset.TagName, _preset.name, "preset")) presetByName[_preset.TagName] = _preset;
            }
            for (int i = 0; i < voices.Count; i++) {
                TypingVoice _voice = voices[i];
                if (_voice == null) continue;
                if (CanRegister(_voice.TagName, _voice.name, "voice")) voiceByName[_voice.TagName] = _voice;
            }
        }

        private bool CanRegister(string _tag, string _assetName, string _kind) {
            if (string.IsNullOrEmpty(_tag)) return Reject($"The {_kind} '{_assetName}' has no tag name and is not registered.");
            if (!IsValidName(_tag)) return Reject($"The {_kind} '{_assetName}' has the invalid tag name '{_tag}' (use letters, digits, '_' or '-') and is not registered.");
            if (ReservedTags.IsReserved(_tag)) return Reject($"The {_kind} '{_assetName}' uses the reserved tag name '{_tag}' and is not registered.");
            if (presetByName.ContainsKey(_tag) || voiceByName.ContainsKey(_tag)) return Reject($"The {_kind} '{_assetName}' repeats the tag name '{_tag}' and is not registered.");
            return true;
        }

        private bool Reject(string _message) {
            issues.Add(new TextIssue(IssueSeverity.Warning, _message, -1));
            return false;
        }

        private static bool IsValidName(string _name) {
            for (int i = 0; i < _name.Length; i++) {
                char _c = _name[i];
                if (!char.IsLetterOrDigit(_c) && _c != '_' && _c != '-') return false;
            }
            return true;
        }
        #endregion
    }
}
