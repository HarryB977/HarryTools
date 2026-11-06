using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace HarryTools.Audio.Subtitles.Demo {
    /// <summary>
    /// Writes every subtitle event to the console and exposes the demo actions on the keys 1-5. The sample draws nothing: this is
    /// what a game's own subtitle UI would subscribe to.
    /// </summary>
    [AddComponentMenu("HarryTools/Audio/Samples/Subtitle Console Logger")]
    public sealed class SubtitleConsoleLogger : MonoBehaviour {
        // ==================== VARIABLES ===================
        [Tooltip("Emitter of the NPC that says the dialogue line (key 1).")]
        public AudioEmitter Npc;
        [Tooltip("Voice line with two staggered entries (key 1).")]
        public VoiceAsset Greeting;
        [Tooltip("Sound with several clips and per-clip subtitles (key 2).")]
        public SoundAsset AttentionCall;
        [Tooltip("Log the Started, TextChanged and Ended events.")]
        public bool LogEvents = true;

        private static bool warnedAboutLocalization;

        // ==================== UNITY ===================
        private void OnEnable() {
            Subtitles.Started += OnStarted;
            Subtitles.Ended += OnEnded;
            Subtitles.TextChanged += OnTextChanged;
            WarnIfLocalizationIsMissing();
        }

        private void OnDisable() {
            Subtitles.Started -= OnStarted;
            Subtitles.Ended -= OnEnded;
            Subtitles.TextChanged -= OnTextChanged;
        }

        private void Update() {
            Keyboard _keyboard = Keyboard.current;
            if (_keyboard == null) return;
            if (_keyboard.digit1Key.wasPressedThisFrame) SayGreeting();
            if (_keyboard.digit2Key.wasPressedThisFrame) PlayRandomSound();
            if (_keyboard.digit3Key.wasPressedThisFrame) CycleLanguage();
            if (_keyboard.digit4Key.wasPressedThisFrame) ToggleDialogue();
            if (_keyboard.digit5Key.wasPressedThisFrame) ToggleCaptions();
        }

        // ==================== ACTIONS ===================
        /// <summary> Key 1: the NPC says its line. </summary>
        public void SayGreeting() {
            if (Npc == null || Greeting == null) return;
            Npc.Say(Greeting);
        }

        /// <summary> Key 2: plays the attention call; the asset picks one of its clips at random. </summary>
        public void PlayRandomSound() {
            if (AttentionCall != null) Audio.Play(AttentionCall);
        }

        /// <summary> Key 3: selects the next locale. </summary>
        public void CycleLanguage() {
            if (!LocalizationSettings.HasSettings || !LocalizationSettings.InitializationOperation.IsDone) return;
            List<Locale> _locales = LocalizationSettings.AvailableLocales.Locales;
            if (_locales.Count < 2) return;
            int _index = _locales.IndexOf(LocalizationSettings.SelectedLocale);
            Locale _next = _locales[(_index + 1) % _locales.Count];
            Debug.Log("[Subtitles demo] Language: " + _next.Identifier.Code);
            LocalizationSettings.SelectedLocale = _next;
        }

        /// <summary> Key 4: shows or hides dialogue lines (a player option, saved in PlayerPrefs). </summary>
        public void ToggleDialogue() {
            Subtitles.ShowDialogue = !Subtitles.ShowDialogue;
            Debug.Log("[Subtitles demo] ShowDialogue: " + Subtitles.ShowDialogue);
        }

        /// <summary> Key 5: shows or hides captions (a player option, saved in PlayerPrefs). </summary>
        public void ToggleCaptions() {
            Subtitles.ShowCaptions = !Subtitles.ShowCaptions;
            Debug.Log("[Subtitles demo] ShowCaptions: " + Subtitles.ShowCaptions);
        }

        // ==================== EVENTS ===================
        private void OnStarted(SubtitleInfo _info) {
            if (LogEvents) Debug.Log("[Started] " + Format(_info));
        }

        private void OnTextChanged(SubtitleInfo _info) {
            if (LogEvents) Debug.Log("[TextChanged] " + Format(_info));
        }

        private void OnEnded(SubtitleInfo _info) {
            if (LogEvents) Debug.Log("[Ended] " + Format(_info));
        }

        // ==================== PRIVATE ===================
        private static string Format(SubtitleInfo _info) {
            string _duration = _info.Duration < 0f ? "until stopped" : _info.Duration.ToString("0.0", CultureInfo.InvariantCulture) + "s";
            string _speaker = string.IsNullOrEmpty(_info.SpeakerName) ? string.Empty : _info.SpeakerName + ": ";
            return "[" + _info.Kind + "] " + _speaker + _info.Text + " (" + _duration + ") #" + _info.Id;
        }

        private static void WarnIfLocalizationIsMissing() {
            if (warnedAboutLocalization || LocalizationSettings.HasSettings) return;
            warnedAboutLocalization = true;
            HarryTools.LoggerTool.Logger.LogWarning("Audio", "The subtitles demo needs Localization Settings with the locales en and es. Create them in Edit > Project Settings > Localization (see the package README).");
        }
    }
}
