using System;
using System.Collections.Generic;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.DomainReload;
using TMPro;
using UnityEngine;
#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
using UnityEngine.Localization;
#endif
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.TextEffects {
    /// <summary> Typewriter, markup commands and text events on top of a TMP_Text </summary>
    [RequireComponent(typeof(TMP_Text)), DisallowMultipleComponent]
    public sealed class EffectText : MonoBehaviour, ITypewriterListener {
        // ==================== VARIABLES ===================
        #region Serialized Variables
        [SerializeField] private TextSourceMode sourceMode = TextSourceMode.Plain;
        [TextArea(2, 8), SerializeField] private string plainText;
#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
        [SerializeField] private LocalizedString localizedText;
#endif

        [Tooltip("Typewriter speed. Zero or less uses the project default"), SerializeField]
        private float charactersPerSecond = 0f;

        [SerializeField] private TimeMode timeMode = TimeMode.Default;

        [Tooltip("Start typing when the component is enabled"), SerializeField]
        private bool showOnEnable = true;

        [Tooltip("Appear effect. Empty uses the project default"), SerializeField]
        private TextEffectPreset appearPreset;

        [Tooltip("Disappear effect. Empty uses the project default"), SerializeField]
        private TextEffectPreset disappearPreset;

        [SerializeField] private HideMode hideMode = HideMode.PerCharacter;
        [SerializeField] private HideOrder hideOrder = HideOrder.Forward;

        [Tooltip("Seconds between characters when hiding per character"), SerializeField, Min(0f)]
        private float hideCharacterDelay = 0.02f;

        [Tooltip("Voice active until a voice tag changes it"), SerializeField]
        private TypingVoice defaultVoice;

        [SerializeField] private TextEventTable events = new TextEventTable();
        #endregion

        #region Public Variables
        /// <summary> Current text, markup included </summary>
        public string Text => parsed.Source;

        public EffectTextState State { get; private set; } = EffectTextState.Hidden;

        public TypingVoice DefaultVoice {
            get => defaultVoice;
            set {
                defaultVoice = value;
                if (State == EffectTextState.Hidden) currentVoice = value;
            }
        }

        public event Action<CharacterShownArgs> CharacterShown;
        public event Action<string> EventReached;
        public event Action WaitingInput;
        public event Action TypingCompleted;
        public event Action Hidden;
        #endregion

        #region Internal Variables
        internal TextEventQueue Queue { get; } = new TextEventQueue();
        internal TextVertexAnimator Animator => animator;

        /// <summary> Characters currently visible. Zero while hidden </summary>
        internal int VisibleCount => State == EffectTextState.Hidden ? 0 : typewriter.VisibleCount;

        /// <summary> Editor preview: events only fire EventReached, with no queued actions </summary>
        internal bool PreviewMode { get; set; }

        /// <summary> Replaces the real playback when assigned, so tests do not need an audio catalog </summary>
        [ClearOnReload] internal static Action<TypingVoice> PlayedForTests;

        internal TextSourceMode SourceMode => sourceMode;

        /// <summary> True until the first localized value arrives. Tests set it to simulate that wait </summary>
        internal bool AwaitingLocalized { get => awaitingLocalized; set => awaitingLocalized = value; }
#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
        internal LocalizedString LocalizedText => localizedText;
#endif
        internal bool ShowOnEnable { get => showOnEnable; set => showOnEnable = value; }
        internal string PlainText { get => plainText; set => plainText = value; }
        internal TimeMode TimeMode { get => timeMode; set => timeMode = value; }
        internal float CharactersPerSecond { get => charactersPerSecond; set => charactersPerSecond = value; }
        internal TextEventTable Events { get => events; set => events = value; }
        internal HideMode HideMode { get => hideMode; set => hideMode = value; }
        internal HideOrder HideOrder { get => hideOrder; set => hideOrder = value; }
        internal float HideCharacterDelay { get => hideCharacterDelay; set => hideCharacterDelay = value; }
        #endregion

        #region Private Variables
        private readonly Typewriter typewriter = new Typewriter();
        private readonly TextVertexAnimator animator = new TextVertexAnimator();
        private readonly ParsedText parsed = new ParsedText();
        private readonly List<TextIssue> loadIssues = new List<TextIssue>();
        private TMP_Text tmpText;
        private ITextEventProvider externalProvider;
        private int[] sourceIndices = Array.Empty<int>();
        private TypingVoice currentVoice;
        private bool unknownVoiceWarned;
        private int soundableCount;
        private bool hasText;

        // TextMeshPro does not generate the mesh of an inactive object, so the load waits for OnEnable
        private bool meshLoadPending;
        private bool loadIssuesPending;

        /// <summary> Seconds of animation since the text was last shown </summary>
        private float animationTime;

        // Requests made from listeners while the typewriter runs are applied when it returns
        private bool insideTypewriter;
        private readonly int[] requestOrder = new int[(int)Request.Count];
        private int requestCounter;
        private string pendingText;
        private bool pendingShow;
        private string pendingLocalized;

        // Set on subscribe: the first localized value is a fresh load, not a locale change
        private bool awaitingLocalized;
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Parses and loads a new text. It starts typing unless '_show' is false. The event queue is not cleared </summary>
        public void SetText(string _text, bool _show = true) {
            if (insideTypewriter) {
                pendingText = _text;
                pendingShow = _show;
                Defer(Request.SetText);
                return;
            }

            TextEffectsSettings _settings = TextEffectsSettings.Current;
            TextParser.Parse(_text, _settings, parsed);

            Tmp.text = parsed.TmpText;
            for (int _i = 0; _i < parsed.Issues.Count; _i++) LogIssue(parsed.Issues[_i], true);

            loadIssuesPending = true;
            if (CanGenerateMesh) LoadMesh();
            else meshLoadPending = true;

            hasText = true;
            unknownVoiceWarned = false;
            currentVoice = defaultVoice;
            soundableCount = 0;
            State = EffectTextState.Hidden;
            animationTime = 0f;
            if (_show) Show();
            else animator.Render(animationTime);
        }

        /// <summary> Starts typing the current text from the beginning </summary>
        public void Show() {
            if (Defer(Request.Show)) return;
            if (awaitingLocalized) {
                // Typing starts when the first localized value arrives
                State = EffectTextState.Typing;
                return;
            }
            ShowFrom(0);
        }

        /// <summary> Reveals the text up to the next waitinput, or to the end </summary>
        public void Skip() {
            if (Defer(Request.Skip)) return;
            if (State != EffectTextState.Typing || awaitingLocalized) return;
            RunTypewriter(TypewriterCall.Skip, 0f);
            animator.Render(animationTime);
        }

        /// <summary> Resumes typing after a waitinput. Does nothing when not waiting </summary>
        public void Continue() {
            if (Defer(Request.Continue)) return;
            if (State != EffectTextState.WaitingInput) return;
            typewriter.Continue();
            State = EffectTextState.Typing;
        }

        /// <summary> Plays the disappear effect on the visible characters. Without a disappear effect it hides at once </summary>
        public void Hide() {
            if (Defer(Request.Hide)) return;
            if (State != EffectTextState.Typing && State != EffectTextState.WaitingInput && State != EffectTextState.Complete) return;

            State = EffectTextState.Hiding;
            animator.BeginHide(hideMode, hideOrder, hideCharacterDelay, animationTime);
            if (animator.IsHideFinished(animationTime)) HideImmediate();
            else animator.Render(animationTime);
        }

        public void HideImmediate() {
            if (Defer(Request.HideImmediate)) return;
            if (State == EffectTextState.Hidden) return;
            State = EffectTextState.Hidden;
            currentVoice = defaultVoice;
            animator.HideAll();
            animator.Render(animationTime);
            Hidden?.Invoke();
        }

        /// <summary> Replaces the event table with another source. Null goes back to the component's own table </summary>
        public void SetEventProvider(ITextEventProvider _provider) {
            externalProvider = _provider;
        }
        #endregion

        #region Internal Methods
        /// <summary> Advances the text by '_dt' seconds. Update calls it in play mode and the editor preview calls it too </summary>
        internal void Tick(float _dt) {
            if (State == EffectTextState.Hidden) return;

            animationTime += _dt;
            if (State == EffectTextState.Typing && !awaitingLocalized) RunTypewriter(TypewriterCall.Tick, _dt);

            if (State == EffectTextState.Hiding && animator.IsHideFinished(animationTime)) {
                HideImmediate();
                return;
            }
            animator.Render(animationTime);
        }

        /// <summary> Applies a text from the localized source. Applies the locale change rules to the current state </summary>
        internal void ApplyLocalizedText(string _value) {
            if (Defer(Request.ApplyLocalized)) {
                pendingLocalized = _value;
                return;
            }

            if (awaitingLocalized) {
                // A Show made while waiting left the state in Typing
                bool _show = showOnEnable || State != EffectTextState.Hidden;
                awaitingLocalized = false;
                SetText(_value, _show);
                return;
            }

            switch (State) {
                case EffectTextState.Typing:
                case EffectTextState.WaitingInput:
                    // Events already notified keep their ordinal and are not repeated
                    int _ordinal = typewriter.NextEventOrdinal;
                    SetText(_value, false);
                    ShowFrom(_ordinal);
                    break;
                case EffectTextState.Complete:
                    SetText(_value, false);
                    ShowCompleteImmediate();
                    break;
                case EffectTextState.Hiding:
                    HideImmediate();
                    SetText(_value, false);
                    break;
                default:
                    SetText(_value, false);
                    break;
            }
        }

        /// <summary> Forgets the text the editor preview loaded, so the next OnEnable loads the source again </summary>
        internal void ResetAfterPreview() {
            TextParser.Parse(string.Empty, null, parsed);
            hasText = false;
            State = EffectTextState.Hidden;
            meshLoadPending = false;
            loadIssuesPending = false;
            animationTime = 0f;
            awaitingLocalized = false;
            Array.Clear(requestOrder, 0, requestOrder.Length);
            pendingText = null;
            pendingLocalized = null;
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable() {
            animator.Bind(Tmp);
            animator.StartListening();
            if (sourceMode == TextSourceMode.Localized) {
                EnterLocalized();
                return;
            }

            if (!hasText) {
                LoadSource();
            } else {
                // Reloading picks up a text set while inactive and any mesh change missed while disabled
                bool _wasShown = meshLoadPending && State != EffectTextState.Hidden;
                LoadMesh();
                if (_wasShown) {
                    Show();
                    return;
                }
            }

            if (showOnEnable) {
                Show();
                return;
            }

            State = EffectTextState.Hidden;
            animator.HideAll();
            animator.Render(animationTime);
        }

        private void OnDisable() {
            LeaveLocalized();
            Queue.Cancel();
            animator.StopListening();
            // Whoever waits for Hidden must not wait forever
            if (State == EffectTextState.Hiding) HideImmediate();
        }

        private void OnDestroy() {
            Queue.Cancel();
            animator.StopListening();
        }

        private void Update() {
            if (!Application.isPlaying) return;
            Tick(IsUnscaled() ? Time.unscaledDeltaTime : Time.deltaTime);
        }
        #endregion

        // ==================== LISTENER ====================
        #region ITypewriterListener
        void ITypewriterListener.OnCharacterShown(int _index, bool _skipped) {
            animator.OnCharacterShown(_index, animationTime);
            TMP_TextInfo _info = Tmp.textInfo;
            char _character = _index < _info.characterCount ? _info.characterInfo[_index].character : '\0';
            if (!_skipped && !PreviewMode) PlayTypingSound(_character);
            if (CharacterShown == null) return;
            CharacterShown(new CharacterShownArgs(_index, _character, currentVoice, _skipped));
        }

        void ITypewriterListener.OnEvent(string _name, int _ordinal, bool _skipped) {
            EventReached?.Invoke(_name);
            if (PreviewMode) return;

            ITextEventProvider _provider = externalProvider ?? events;
            if (!_provider.TryGetEvent(_name, out ActionList _actions, out bool _runWhenSkipped)) return;
            if (_actions == null) return;
            if (_skipped && !_runWhenSkipped) return;
            Queue.Enqueue(_actions, LogicContext.From(this));
        }

        void ITypewriterListener.OnVoiceChanged(string _voiceName) {
            if (TextEffectsSettings.Current.TryGetVoice(_voiceName, out TypingVoice _voice)) {
                currentVoice = _voice;
                soundableCount = 0;
                return;
            }

            if (unknownVoiceWarned) return;
            unknownVoiceWarned = true;
            Logger.LogWarning("TextEffects", "Unknown voice '" + _voiceName + "' in the text of '" + name + "'. The current voice is kept.");
        }

        void ITypewriterListener.OnWaitingInput() {
            State = EffectTextState.WaitingInput;
            WaitingInput?.Invoke();
        }

        void ITypewriterListener.OnCompleted() {
            State = EffectTextState.Complete;
            TypingCompleted?.Invoke();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private TMP_Text Tmp {
            get {
                if (tmpText == null) tmpText = GetComponent<TMP_Text>();
                return tmpText;
            }
        }

        /// <summary> TextMeshPro only generates the mesh of an active and enabled object </summary>
        private bool CanGenerateMesh => enabled && gameObject.activeInHierarchy;

        private void LoadSource() {
            SetText(sourceMode == TextSourceMode.Localized ? string.Empty : plainText, false);
        }

        /// <summary> Starts from hidden and waits for the first localized value, which may arrive on a later frame </summary>
        private void EnterLocalized() {
            if (!hasText) LoadSource();
            State = EffectTextState.Hidden;
            animator.HideAll();
            animator.Render(animationTime);

#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
            if (localizedText == null) return;
            awaitingLocalized = true;
            // Raises StringChanged with the current value when it is already loaded
            localizedText.StringChanged += OnLocalizedStringChanged;
#endif
        }

        private void LeaveLocalized() {
            awaitingLocalized = false;
#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
            if (localizedText != null) localizedText.StringChanged -= OnLocalizedStringChanged;
#endif
        }

#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
        private void OnLocalizedStringChanged(string _value) {
            ApplyLocalizedText(_value);
        }
#endif

        /// <summary> Starts typing from the first character. Events below '_suppressEventsBelowOrdinal' are not notified </summary>
        private void ShowFrom(int _suppressEventsBelowOrdinal) {
            if (!hasText) LoadSource();
            if (meshLoadPending && CanGenerateMesh) LoadMesh();
            typewriter.CharactersPerSecond = ResolveCharactersPerSecond();
            currentVoice = defaultVoice;
            soundableCount = 0;
            animationTime = 0f;
            animator.HideAll();
            typewriter.Start(_suppressEventsBelowOrdinal);
            State = EffectTextState.Typing;
            animator.Render(animationTime);
        }

        /// <summary> Shows every character at once with no events, voices or typing </summary>
        private void ShowCompleteImmediate() {
            if (meshLoadPending && CanGenerateMesh) LoadMesh();
            if (meshLoadPending) return;

            typewriter.CharactersPerSecond = ResolveCharactersPerSecond();
            animationTime = 0f;
            animator.HideAll();
            int _count = Tmp.textInfo.characterCount;
            for (int _i = 0; _i < _count; _i++) animator.OnCharacterShown(_i, animationTime);
            RunTypewriter(TypewriterCall.ShowAll, 0f);
            animator.Render(animationTime);
        }

        /// <summary> Generates the TMP mesh and loads the animator and the typewriter from it. Allocates </summary>
        private void LoadMesh() {
            TMP_Text _tmp = Tmp;
            animator.Bind(_tmp);
            loadIssues.Clear();
            animator.Load(parsed, TextEffectsSettings.Current, appearPreset, disappearPreset, loadIssues);
            if (loadIssuesPending) {
                loadIssuesPending = false;
                for (int _i = 0; _i < loadIssues.Count; _i++) LogIssue(loadIssues[_i], false);
            }

            TMP_TextInfo _info = _tmp.textInfo;
            int _count = _info.characterCount;
            if (sourceIndices.Length < _count) sourceIndices = new int[Mathf.Max(_count, sourceIndices.Length * 2)];
            for (int _i = 0; _i < _count; _i++) sourceIndices[_i] = _info.characterInfo[_i].index;
            typewriter.Load(parsed, sourceIndices, _count);
            meshLoadPending = false;
        }

        private void LogIssue(TextIssue _issue, bool _withPosition) {
            string _message = "In the text of '" + name + "'" + (_withPosition && _issue.Position >= 0 ? " at position " + _issue.Position : string.Empty) + ": " + _issue.Message;
            if (_issue.Severity == IssueSeverity.Error) Logger.LogError("TextEffects", _message);
            else Logger.LogWarning("TextEffects", _message);
        }

        /// <summary> Counts the character when the voice can sound it and plays on every N-th one, starting with the first </summary>
        private void PlayTypingSound(char _character) {
            TypingVoice _voice = currentVoice;
            if (_voice == null || !_voice.CanSound(_character)) return;

            int _count = soundableCount++;
            if (!_voice.ShouldPlay(_count)) return;

            if (PlayedForTests != null) {
                PlayedForTests(_voice);
                return;
            }
#if HARRYTOOLS_TEXTEFFECTS_AUDIO
            TMP_Text _tmp = Tmp;
            TypingVoicePlayer.Play(_voice, _tmp.transform, _tmp is TextMeshPro);
#endif
        }

        private bool IsUnscaled() {
            switch (timeMode) {
                case TimeMode.Scaled: return false;
                case TimeMode.Unscaled: return true;
                default: return TextEffectsSettings.Current.DefaultUnscaledTime;
            }
        }

        private float ResolveCharactersPerSecond() {
            return charactersPerSecond > 0f ? charactersPerSecond : TextEffectsSettings.Current.DefaultCharactersPerSecond;
        }
        #endregion

        #region Typewriter Calls
        private enum Request {
            None,
            SetText,
            Show,
            Skip,
            Hide,
            HideImmediate,
            Continue,
            ApplyLocalized,
            Count
        }

        private enum TypewriterCall {
            Tick,
            Skip,
            ShowAll
        }

        /// <summary> Advances, skips or completes the typewriter, then applies the requests its listeners made meanwhile </summary>
        private void RunTypewriter(TypewriterCall _call, float _dt) {
            insideTypewriter = true;
            try {
                switch (_call) {
                    case TypewriterCall.Tick: typewriter.Tick(_dt, this); break;
                    case TypewriterCall.Skip: typewriter.Skip(this); break;
                    case TypewriterCall.ShowAll: typewriter.ShowAllImmediate(this); break;
                }
            } catch {
                // Requests made before the failure are dropped so they do not run on a later, unrelated call
                Array.Clear(requestOrder, 0, requestOrder.Length);
                pendingText = null;
                pendingLocalized = null;
                throw;
            } finally {
                insideTypewriter = false;
            }
            ApplyPendingRequests();
        }

        /// <summary> Records the request when the typewriter is running. The last request of each kind wins </summary>
        private bool Defer(Request _request) {
            if (!insideTypewriter) return false;
            requestOrder[(int)_request] = ++requestCounter;
            return true;
        }

        /// <summary> Applies the deferred requests in the order they were last made </summary>
        private void ApplyPendingRequests() {
            while (true) {
                int _next = 0;
                for (int _i = 1; _i < requestOrder.Length; _i++) {
                    if (requestOrder[_i] > 0 && (_next == 0 || requestOrder[_i] < requestOrder[_next])) _next = _i;
                }
                if (_next == 0) return;

                requestOrder[_next] = 0;
                switch ((Request)_next) {
                    case Request.SetText:
                        string _text = pendingText;
                        pendingText = null;
                        SetText(_text, pendingShow);
                        break;
                    case Request.ApplyLocalized:
                        string _localized = pendingLocalized;
                        pendingLocalized = null;
                        ApplyLocalizedText(_localized);
                        break;
                    case Request.Show: Show(); break;
                    case Request.Skip: Skip(); break;
                    case Request.Continue: Continue(); break;
                    case Request.Hide: Hide(); break;
                    case Request.HideImmediate: HideImmediate(); break;
                }
            }
        }
        #endregion
    }
}
