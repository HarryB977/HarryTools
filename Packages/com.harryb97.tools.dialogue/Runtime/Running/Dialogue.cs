using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HarryTools.DomainReload;
using UnityEngine.Localization;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Dialogue {
    /// <summary> Starts dialogues and keeps track of the ones running </summary>
    public static class Dialogue {
        // ==================== VARIABLES ===================
        #region Private Variables
        internal const string LogCategory = "Dialogue";

        [ClearOnReload(AssignNewInstance = true)] private static List<DialogueRun> active = new List<DialogueRun>();
        // Copy of active handed out by Active, rebuilt after the list changes
        [ClearOnReload] private static ReadOnlyCollection<DialogueRun> activeSnapshot;
        [ClearOnReload(AssignNewInstance = true)] private static List<DialogueRun> tickBuffer = new List<DialogueRun>();
        [ClearOnReload] private static DialogueRun main;
        // Ambient runs paused by the main run's start, resumed when it ends
        [ClearOnReload(AssignNewInstance = true)] private static List<DialogueRun> pausedForMain = new List<DialogueRun>();

        [ClearOnReload] private static Action<DialogueRun> startedHandlers;
        [ClearOnReload] private static Action<DialogueRun, EndReason> endedHandlers;
        [ClearOnReload] private static Action<DialogueRun, LineNode> lineStartedHandlers;
        [ClearOnReload] private static Action<DialogueRun, LineNode> lineEndedHandlers;
        [ClearOnReload] private static Action<DialogueRun, ChoicePrompt> choicePresentedHandlers;
        [ClearOnReload] private static Action<DialogueRun, ChoiceOption> choiceMadeHandlers;
        #endregion

        #region Public Variables
        /// <summary> The main dialogue running, or null </summary>
        public static DialogueRun Main => main;

        /// <summary> Every dialogue running, main and ambient. A copy: it does not change when runs start or end, so the runs can be stopped while iterating it </summary>
        public static IReadOnlyList<DialogueRun> Active => activeSnapshot ?? (activeSnapshot = new ReadOnlyCollection<DialogueRun>(active.ToArray()));
        #endregion

        #region Internal Variables
        /// <summary> Picks the channel that shows a line. Null uses <see cref="LineRouter"/>; a null result uses the channel without view </summary>
        [ClearOnReload] internal static Func<LinePresentation, ILineChannel> ChannelResolver;

        /// <summary> Gives the text of a line. Null reads its localized string </summary>
        [ClearOnReload] internal static Func<LineNode, string> LineTextResolver;

        /// <summary> When true, nothing ticks the dialogues except <see cref="TickAll"/> </summary>
        [ClearOnReload] internal static bool ManualTick;

        /// <summary> Raised for the choice view when a main dialogue offers a choice </summary>
        [ClearOnReload] internal static Action<ChoicePrompt> ChoiceShown;

        /// <summary> Raised for the choice view when an offered choice closes: answered, timed out or its run ended </summary>
        [ClearOnReload] internal static Action<ChoicePrompt> ChoiceHidden;

        /// <summary> Raised for the line containers when a run starts skipping, so its lines leave the screen </summary>
        [ClearOnReload] internal static Action<DialogueRun> RunSkipped;
        #endregion

        // ==================== EVENTS ====================
        #region Events
        public static event Action<DialogueRun> Started {
            add => startedHandlers += value;
            remove => startedHandlers -= value;
        }

        public static event Action<DialogueRun, EndReason> Ended {
            add => endedHandlers += value;
            remove => endedHandlers -= value;
        }

        public static event Action<DialogueRun, LineNode> LineStarted {
            add => lineStartedHandlers += value;
            remove => lineStartedHandlers -= value;
        }

        public static event Action<DialogueRun, LineNode> LineEnded {
            add => lineEndedHandlers += value;
            remove => lineEndedHandlers -= value;
        }

        public static event Action<DialogueRun, ChoicePrompt> ChoicePresented {
            add => choicePresentedHandlers += value;
            remove => choicePresentedHandlers -= value;
        }

        /// <summary> Raised when an option is chosen, by the player, the timer or an ambient dialogue. Not raised for the no answer output </summary>
        public static event Action<DialogueRun, ChoiceOption> ChoiceMade {
            add => choiceMadeHandlers += value;
            remove => choiceMadeHandlers -= value;
        }
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Starts the dialogue. It begins on the next tick, so the run's events can be subscribed first </summary>
        /// <returns> The run, or null when the dialogue cannot start or a main dialogue is already running </returns>
        public static DialogueRun Start(DialogueAsset _asset, DialogueStartOptions _options = null) {
            if (_asset == null) {
                Logger.LogWarning(LogCategory, "Cannot start a null dialogue.");
                return null;
            }
            if (_asset.Start == null) {
                Logger.LogWarning(LogCategory, $"Dialogue '{_asset.name}' has no start node.");
                return null;
            }

            _options = _options ?? new DialogueStartOptions();
            if (_options.IsMain && main != null) {
                Logger.LogWarning(LogCategory, $"Dialogue '{_asset.name}' not started: main dialogue '{main.Asset.name}' is still running.");
                return null;
            }

            DialogueRun _run = new DialogueRun(_asset, _options);
            active.Add(_run);
            activeSnapshot = null;
            if (_run.IsMain) {
                main = _run;
                ApplyAmbientPolicies(_run);
            }

            DialogueDriver.Ensure();
            DialogueMemory.MarkPlayed(_asset);
            startedHandlers?.Invoke(_run);
            return _run;
        }

        public static void PauseAll() {
            DialogueRun[] _runs = active.ToArray();
            for (int _i = 0; _i < _runs.Length; _i++) _runs[_i].Pause();
        }

        public static void ResumeAll() {
            DialogueRun[] _runs = active.ToArray();
            for (int _i = 0; _i < _runs.Length; _i++) _runs[_i].Resume();
        }

        public static void StopAll() {
            DialogueRun[] _runs = active.ToArray();
            for (int _i = 0; _i < _runs.Length; _i++) _runs[_i].Stop();
        }
        #endregion

        // ==================== INTERNAL ====================
        #region Internal
        /// <summary> Advances every run once. Runs started during the call wait for the next one </summary>
        internal static void TickAll(float _scaledDt, float _unscaledDt) {
            tickBuffer.Clear();
            tickBuffer.AddRange(active);
            for (int _i = 0; _i < tickBuffer.Count; _i++) {
                DialogueRun _run = tickBuffer[_i];
                try {
                    _run.Tick(_scaledDt, _unscaledDt);
                } catch (Exception _exception) {
                    StopAfterError(_run, _exception);
                }
            }
            tickBuffer.Clear();
        }

        /// <summary> Text of the line in the current locale, or null when it is not set or cannot be resolved </summary>
        internal static string ResolveLineText(LineNode _line) {
            if (_line == null) return null;
            if (LineTextResolver != null) return LineTextResolver(_line);
            LocalizedString _text = _line.Text;
            if (_text == null || _text.IsEmpty) return null;
            try {
                return _text.GetLocalizedString();
            } catch (Exception) {
                return null;
            }
        }

        internal static ILineChannel ResolveChannel(LinePresentation _line) {
            ILineChannel _channel = ChannelResolver != null ? ChannelResolver(_line) : LineRouter.Resolve(_line);
            return _channel ?? HeadlessLineChannel.Instance;
        }

        /// <summary> Called by a run as it ends, before any of its end events, so their handlers can start another main dialogue and a handler that throws leaves no stale run </summary>
        internal static void Unregister(DialogueRun _run) {
            if (active.Remove(_run)) activeSnapshot = null;
            pausedForMain.Remove(_run);
            if (main != _run) return;
            main = null;
            ResumePausedForMain();
        }

        internal static void RaiseEnded(DialogueRun _run, EndReason _reason) => endedHandlers?.Invoke(_run, _reason);
        internal static void RaiseLineStarted(DialogueRun _run, LineNode _line) => lineStartedHandlers?.Invoke(_run, _line);
        internal static void RaiseLineEnded(DialogueRun _run, LineNode _line) => lineEndedHandlers?.Invoke(_run, _line);
        internal static void RaiseChoicePresented(DialogueRun _run, ChoicePrompt _prompt) => choicePresentedHandlers?.Invoke(_run, _prompt);
        internal static void RaiseChoiceMade(DialogueRun _run, ChoiceOption _option) => choiceMadeHandlers?.Invoke(_run, _option);

        /// <summary> Drops every run without ending it, the handlers, the resolver, the choice view hooks and the driver </summary>
        internal static void ClearForTests() {
            active.Clear();
            activeSnapshot = null;
            tickBuffer.Clear();
            main = null;
            pausedForMain.Clear();
            startedHandlers = null;
            endedHandlers = null;
            lineStartedHandlers = null;
            lineEndedHandlers = null;
            choicePresentedHandlers = null;
            choiceMadeHandlers = null;
            ChannelResolver = null;
            LineTextResolver = null;
            ChoiceShown = null;
            ChoiceHidden = null;
            RunSkipped = null;
            ManualTick = false;
            LineRouter.ClearForTests();
            DialogueHistory.ClearForTests();
            LineView.HoldTextForTests = false;
            DialogueDriver.DestroyInstance();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        /// <summary> Applies the ambient policy to every ambient run as a main run starts: the main run's option, else the ambient asset's override, else the settings </summary>
        private static void ApplyAmbientPolicies(DialogueRun _main) {
            DialogueRun[] _runs = active.ToArray();
            for (int _i = 0; _i < _runs.Length; _i++) {
                DialogueRun _run = _runs[_i];
                if (_run == _main || _run.IsMain || _run.State == RunState.Ended) continue;
                AmbientPolicy _policy = _main.Options.AmbientPolicy ?? AssetPolicy(_run.Asset);
                switch (_policy) {
                    case AmbientPolicy.Pause:
                        // Kept apart from a pause by hand: Resume does not lift it, and the end of the main run does not lift a pause by hand
                        _run.HoldForMain();
                        pausedForMain.Add(_run);
                        break;

                    case AmbientPolicy.Silence:
                        _run.Stop();
                        break;
                }
            }
        }

        private static AmbientPolicy AssetPolicy(DialogueAsset _asset) {
            if (_asset != null && _asset.OverrideAmbientPolicy) return _asset.AmbientPolicy;
            return DialogueSettings.Current.DefaultAmbientPolicy;
        }

        private static void ResumePausedForMain() {
            if (pausedForMain.Count == 0) return;
            DialogueRun[] _runs = pausedForMain.ToArray();
            pausedForMain.Clear();
            for (int _i = 0; _i < _runs.Length; _i++) _runs[_i].ReleaseHoldForMain();
        }

        /// <summary> A run that threw while ticking is stopped so the others keep going </summary>
        private static void StopAfterError(DialogueRun _run, Exception _exception) {
            string _name = _run.Asset != null ? _run.Asset.name : "(missing)";
            Logger.LogError(LogCategory, $"Dialogue '{_name}' failed and was stopped: {_exception}");
            try {
                _run.Stop();
            } catch (Exception _stopException) {
                Logger.LogError(LogCategory, $"Dialogue '{_name}' failed while stopping: {_stopException}");
            }
        }
        #endregion
    }
}
