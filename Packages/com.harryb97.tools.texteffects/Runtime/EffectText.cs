using System;
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

        /// <summary> Characters currently visible. Zero while hidden </summary>
        internal int VisibleCount => State == EffectTextState.Hidden ? 0 : typewriter.VisibleCount;

        /// <summary> Editor preview: events only fire EventReached, with no queued actions </summary>
        internal bool PreviewMode { get; set; }

        internal bool ShowOnEnable { get => showOnEnable; set => showOnEnable = value; }
        internal string PlainText { get => plainText; set => plainText = value; }
        internal TimeMode TimeMode { get => timeMode; set => timeMode = value; }
        internal float CharactersPerSecond { get => charactersPerSecond; set => charactersPerSecond = value; }
        internal TextEventTable Events { get => events; set => events = value; }
        #endregion

        #region Private Variables
        private readonly Typewriter typewriter = new Typewriter();
        private readonly ParsedText parsed = new ParsedText();
        private TMP_Text tmpText;
        private ITextEventProvider externalProvider;
        private int[] sourceIndices = Array.Empty<int>();
        private TypingVoice currentVoice;
        private bool unknownVoiceWarned;
        private bool hasText;
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Parses and loads a new text. It starts typing unless '_show' is false. The event queue is not cleared </summary>
        public void SetText(string _text, bool _show = true) {
            TextEffectsSettings _settings = TextEffectsSettings.Current;
            TextParser.Parse(_text, _settings, parsed);

            TMP_Text _tmp = Tmp;
            _tmp.text = parsed.TmpText;
            _tmp.ForceMeshUpdate();

            TMP_TextInfo _info = _tmp.textInfo;
            int _count = _info.characterCount;
            if (sourceIndices.Length < _count) sourceIndices = new int[Mathf.Max(_count, sourceIndices.Length * 2)];
            for (int _i = 0; _i < _count; _i++) sourceIndices[_i] = _info.characterInfo[_i].index;
            typewriter.Load(parsed, sourceIndices, _count);

            hasText = true;
            unknownVoiceWarned = false;
            currentVoice = defaultVoice;
            State = EffectTextState.Hidden;
            if (_show) Show();
        }

        /// <summary> Starts typing the current text from the beginning </summary>
        public void Show() {
            if (!hasText) LoadSource();
            typewriter.CharactersPerSecond = ResolveCharactersPerSecond();
            currentVoice = defaultVoice;
            typewriter.Start();
            State = EffectTextState.Typing;
        }

        /// <summary> Reveals the text up to the next waitinput, or to the end </summary>
        public void Skip() {
            if (State != EffectTextState.Typing) return;
            typewriter.Skip(this);
        }

        /// <summary> Resumes typing after a waitinput. Does nothing when not waiting </summary>
        public void Continue() {
            if (State != EffectTextState.WaitingInput) return;
            typewriter.Continue();
            State = EffectTextState.Typing;
        }

        /// <summary> Hides the text. Without a vertex animator this is the same as HideImmediate </summary>
        public void Hide() {
            HideImmediate();
        }

        public void HideImmediate() {
            if (State == EffectTextState.Hidden) return;
            State = EffectTextState.Hidden;
            currentVoice = defaultVoice;
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
            if (State != EffectTextState.Typing) return;
            typewriter.Tick(_dt, this);
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable() {
            if (!hasText) LoadSource();
            if (showOnEnable) Show();
            else State = EffectTextState.Hidden;
        }

        private void OnDisable() {
            Queue.Cancel();
        }

        private void OnDestroy() {
            Queue.Cancel();
        }

        private void Update() {
            if (!Application.isPlaying) return;
            Tick(IsUnscaled() ? Time.unscaledDeltaTime : Time.deltaTime);
        }
        #endregion

        // ==================== LISTENER ====================
        #region ITypewriterListener
        void ITypewriterListener.OnCharacterShown(int _index, bool _skipped) {
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
    }
}
