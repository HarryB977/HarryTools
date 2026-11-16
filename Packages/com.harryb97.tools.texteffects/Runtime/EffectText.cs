using System;
using System.Collections.Generic;
using HarryTools.Actions;
using HarryTools.Conditions;
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
        private bool hasText;

        /// <summary> Seconds of animation since the text was last shown </summary>
        private float animationTime;

        // Requests made from listeners while the typewriter runs are applied when it returns
        private bool insideTypewriter;
        private readonly int[] requestOrder = new int[(int)Request.Count];
        private int requestCounter;
        private string pendingText;
        private bool pendingShow;
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

            TMP_Text _tmp = Tmp;
            _tmp.text = parsed.TmpText;
            animator.Bind(_tmp);
            loadIssues.Clear();
            animator.Load(parsed, _settings, appearPreset, disappearPreset, loadIssues);
            for (int _i = 0; _i < loadIssues.Count; _i++) {
                Logger.LogWarning("TextEffects", "In the text of '" + name + "': " + loadIssues[_i].Message);
            }

            TMP_TextInfo _info = _tmp.textInfo;
            int _count = _info.characterCount;
            if (sourceIndices.Length < _count) sourceIndices = new int[Mathf.Max(_count, sourceIndices.Length * 2)];
            for (int _i = 0; _i < _count; _i++) sourceIndices[_i] = _info.characterInfo[_i].index;
            typewriter.Load(parsed, sourceIndices, _count);

            hasText = true;
            unknownVoiceWarned = false;
            currentVoice = defaultVoice;
            State = EffectTextState.Hidden;
            animationTime = 0f;
            if (_show) Show();
            else animator.Render(animationTime);
        }

        /// <summary> Starts typing the current text from the beginning </summary>
        public void Show() {
            if (Defer(Request.Show)) return;
            if (!hasText) LoadSource();
            typewriter.CharactersPerSecond = ResolveCharactersPerSecond();
            currentVoice = defaultVoice;
            animationTime = 0f;
            animator.HideAll();
            typewriter.Start();
            State = EffectTextState.Typing;
            animator.Render(animationTime);
        }

        /// <summary> Reveals the text up to the next waitinput, or to the end </summary>
        public void Skip() {
            if (Defer(Request.Skip)) return;
            if (State != EffectTextState.Typing) return;
            RunTypewriter(true, 0f);
            animator.Render(animationTime);
        }

        /// <summary> Resumes typing after a waitinput. Does nothing when not waiting </summary>
        public void Continue() {
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
            if (State == EffectTextState.Typing) RunTypewriter(false, _dt);

            if (State == EffectTextState.Hiding && animator.IsHideFinished(animationTime)) {
                HideImmediate();
                return;
            }
            animator.Render(animationTime);
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable() {
            animator.Bind(Tmp);
            animator.StartListening();
            if (!hasText) LoadSource();
            if (showOnEnable) {
                Show();
                return;
            }

            State = EffectTextState.Hidden;
            animator.HideAll();
            animator.Render(animationTime);
        }

        private void OnDisable() {
            Queue.Cancel();
            animator.StopListening();
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
            if (CharacterShown == null) return;
            TMP_TextInfo _info = Tmp.textInfo;
            char _character = _index < _info.characterCount ? _info.characterInfo[_index].character : '\0';
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

        private void LoadSource() {
            SetText(plainText, false);
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
            Count
        }

        /// <summary> Advances or skips the typewriter, then applies the requests its listeners made meanwhile </summary>
        private void RunTypewriter(bool _skip, float _dt) {
            insideTypewriter = true;
            try {
                if (_skip) typewriter.Skip(this);
                else typewriter.Tick(_dt, this);
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
                    case Request.Show: Show(); break;
                    case Request.Skip: Skip(); break;
                    case Request.Hide: Hide(); break;
                    case Request.HideImmediate: HideImmediate(); break;
                }
            }
        }
        #endregion
    }
}
