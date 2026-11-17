using System;
using System.Collections.Generic;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.DomainReload;
using HarryTools.TextEffects;
using TMPro;
using UnityEngine;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Dialogue {
    /// <summary> Shows dialogue lines with pooled <see cref="LineView"/>s under <see cref="LineParent"/>: how many stay on screen, how long and how they advance. It only orders the views as siblings and notifies them; layout and animation belong to the views. A line stays on screen until a new line needs its place, its run skips or its run ends </summary>
    [AddComponentMenu("HarryTools/Dialogue/Line Container")]
    public class LineContainer : MonoBehaviour, ILineChannel {
        // ==================== VARIABLES ===================
        #region Serialized Variables
        [Tooltip("Receives the lines of the main dialogue and shows its choices. With several, the last enabled is used"), SerializeField]
        private bool isMain;

        [Tooltip("Receives the lines of ambient dialogues whose actor has no container. With several, the last enabled is used"), SerializeField]
        private bool isDefaultAmbient;

        [Tooltip("Most lines on screen. With 1, each line replaces the previous one; with more, the oldest leaves when a new one comes in"), SerializeField, Min(1)]
        private int maxVisible = 1;

        [Tooltip("How long a line lasts once typed, unless the line sets its own"), SerializeField]
        private LineLifetime lifetime = LineLifetime.Default;

        [Tooltip("How a line advances, unless the line sets its own"), SerializeField]
        private AdvanceMode advance = AdvanceMode.Both;

        [Tooltip("View used for each line. Views are pooled"), SerializeField]
        private LineView linePrefab;

        [Tooltip("Parent of the line views. Empty uses this object"), SerializeField]
        private RectTransform lineParent;

        [Tooltip("Shows the choices of the main dialogue while this is the main container"), SerializeField]
        private ChoiceView choiceView;
        #endregion

        #region Private Variables
        [ClearOnReload(AssignNewInstance = true)] private static List<LineContainer> mains = new List<LineContainer>();
        [ClearOnReload(AssignNewInstance = true)] private static List<LineContainer> defaultAmbients = new List<LineContainer>();

        // Lines on screen, oldest first. Both lists share their indices
        private readonly List<LineView> visible = new List<LineView>();
        private readonly List<LineHandle> handles = new List<LineHandle>();

        private readonly Stack<LineView> pool = new Stack<LineView>();
        // Lines playing their hide before their view goes back to the pool
        private readonly List<LineHandle> leaving = new List<LineHandle>();
        // Views left on screen when the container was disabled, cleaned up on the next enable
        private readonly List<LineView> stale = new List<LineView>();

        private bool warnedNoPrefab;
        #endregion

        #region Public Variables
        public bool IsMain {
            get => isMain;
            set {
                if (isMain == value) return;
                isMain = value;
                if (isActiveAndEnabled) Register(mains, this, isMain, "main");
            }
        }

        public bool IsDefaultAmbient {
            get => isDefaultAmbient;
            set {
                if (isDefaultAmbient == value) return;
                isDefaultAmbient = value;
                if (isActiveAndEnabled) Register(defaultAmbients, this, isDefaultAmbient, "default ambient");
            }
        }

        public int MaxVisible {
            get => maxVisible;
            set => maxVisible = Mathf.Max(1, value);
        }

        public LineLifetime Lifetime {
            get => lifetime;
            set => lifetime = value;
        }

        public AdvanceMode Advance {
            get => advance;
            set => advance = value;
        }

        public LineView LinePrefab {
            get => linePrefab;
            set => linePrefab = value;
        }

        public RectTransform LineParent {
            get => lineParent;
            set => lineParent = value;
        }

        public ChoiceView ChoiceView {
            get => choiceView;
            set => choiceView = value;
        }

        /// <summary> Lines on screen, oldest first. Lines playing their hide are not listed </summary>
        public IReadOnlyList<LineView> Visible => visible;

        /// <summary> Raised after a line is bound and entered </summary>
        public event Action<LineView> LineAdded;

        /// <summary> Raised when a line leaves, before its text hides </summary>
        public event Action<LineView> LineRemoved;

        /// <summary> Raised after the order or count of <see cref="Visible"/> changes and the views were notified </summary>
        public event Action LinesMoved;

        /// <summary> The enabled main container, the last one enabled when there are several. Null when there is none </summary>
        public static LineContainer Main => mains.Count > 0 ? mains[mains.Count - 1] : null;

        /// <summary> The enabled default ambient container, the last one enabled when there are several. Null when there is none </summary>
        public static LineContainer DefaultAmbient => defaultAmbients.Count > 0 ? defaultAmbients[defaultAmbients.Count - 1] : null;
        #endregion

        // ==================== CHANNEL ====================
        #region ILineChannel
        /// <summary> Shows the line in a pooled view, making room first. Returns null, so the run goes on without view, when the container is disabled or has no prefab </summary>
        public ILineHandle Present(LinePresentation _line) {
            if (!isActiveAndEnabled) return null;
            if (linePrefab == null) {
                if (!warnedNoPrefab) {
                    warnedNoPrefab = true;
                    Logger.LogWarning(Dialogue.LogCategory, $"Line container '{name}' has no line prefab. Its lines are not shown; the dialogue goes on.");
                }
                return null;
            }

            CleanStale();
            DropDestroyedViews();
            while (visible.Count >= Mathf.Max(1, maxVisible)) RemoveAt(0);

            LineLifetime _lifetime = _line.Line.OverrideLifetime ? _line.Lifetime : lifetime;
            AdvanceMode _advance = _line.Line.OverrideAdvance ? _line.Advance : advance;
            LineView _view = Rent();
            LineHandle _handle = new LineHandle(this, _view, _line, _lifetime, _advance);

            visible.Add(_view);
            handles.Add(_handle);
            _view.transform.SetAsLastSibling();
            _handle.Begin();
            _view.OnEnter();
            LineAdded?.Invoke(_view);
            NotifyMoved();
            return _handle;
        }
        #endregion

        // ==================== UNITY ====================
        #region Unity
        protected virtual void Awake() {
            // Kept while disabled so a choice closing meanwhile still hides the view
            Dialogue.ChoiceShown += OnChoiceShown;
            Dialogue.ChoiceHidden += OnChoiceHidden;
        }

        protected virtual void OnEnable() {
            // Views cannot be deactivated while this hierarchy is being activated
            if (stale.Count > 0) Invoke(nameof(CleanStale), 0f);
            if (isMain) Register(mains, this, true, "main");
            if (isDefaultAmbient) Register(defaultAmbients, this, true, "default ambient");
            Dialogue.Ended += OnRunEnded;
            Dialogue.RunSkipped += OnRunSkipped;

            // A choice offered before this container became the main one
            ChoicePrompt _open = Dialogue.Main != null ? Dialogue.Main.CurrentChoice : null;
            if (_open != null && Main == this && choiceView != null && choiceView.Prompt != _open) choiceView.Show(_open);
        }

        /// <summary> Lines on screen go on without view. The views are not touched here: the hierarchy may be deactivating or being destroyed </summary>
        protected virtual void OnDisable() {
            mains.Remove(this);
            defaultAmbients.Remove(this);
            Dialogue.Ended -= OnRunEnded;
            Dialogue.RunSkipped -= OnRunSkipped;

            for (int _i = 0; _i < handles.Count; _i++) handles[_i].Detach();
            stale.AddRange(visible);
            for (int _i = 0; _i < leaving.Count; _i++) {
                leaving[_i].StopWaitingHidden();
                stale.Add(leaving[_i].View);
            }
            visible.Clear();
            handles.Clear();
            leaving.Clear();
        }

        protected virtual void OnDestroy() {
            Dialogue.ChoiceShown -= OnChoiceShown;
            Dialogue.ChoiceHidden -= OnChoiceHidden;
        }
        #endregion

        // ==================== LINES ====================
        #region Lines
        private LineView Rent() {
            while (pool.Count > 0) {
                LineView _pooled = pool.Pop();
                if (_pooled == null) continue;
                _pooled.gameObject.SetActive(true);
                return _pooled;
            }

            Transform _parent = lineParent != null ? (Transform)lineParent : transform;
            LineView _view = Instantiate(linePrefab, _parent, false);
            if (!_view.gameObject.activeSelf) _view.gameObject.SetActive(true);
            return _view;
        }

        /// <summary> Takes the line off the visible list and hides it; it goes back to the pool once hidden </summary>
        private void Remove(LineHandle _handle) {
            int _index = handles.IndexOf(_handle);
            if (_index >= 0) RemoveAt(_index);
        }

        private void RemoveAt(int _index) {
            LineView _view = visible[_index];
            LineHandle _handle = handles[_index];
            visible.RemoveAt(_index);
            handles.RemoveAt(_index);
            _handle.LeaveScreen();

            if (_view != null) {
                _view.OnExit();
                LineRemoved?.Invoke(_view);
                Hide(_handle);
            }
            NotifyMoved();
        }

        private void Hide(LineHandle _handle) {
            EffectText _text = _handle.View.Text;
            if (_text == null || _text.State == EffectTextState.Hidden || !_text.isActiveAndEnabled) {
                Return(_handle.View);
                return;
            }

            leaving.Add(_handle);
            if (_text.IsPaused) _text.Resume();
            _text.Hidden += _handle.OnHidden;
            _text.Hide();
        }

        /// <summary> Called by a handle once its view's text is hidden </summary>
        private void Returned(LineHandle _handle) {
            leaving.Remove(_handle);
            Return(_handle.View);
        }

        private void Return(LineView _view) {
            if (_view == null) return;
            _view.Unbind();
            _view.gameObject.SetActive(false);
            pool.Push(_view);
        }

        private void NotifyMoved() {
            int _count = visible.Count;
            for (int _i = 0; _i < _count; _i++) visible[_i].OnIndexChanged(_i, _count);
            LinesMoved?.Invoke();
        }

        /// <summary> A view destroyed from outside leaves the list; its line goes on without view </summary>
        private void DropDestroyedViews() {
            for (int _i = visible.Count - 1; _i >= 0; _i--) {
                if (visible[_i] != null) continue;
                handles[_i].Detach();
                visible.RemoveAt(_i);
                handles.RemoveAt(_i);
            }
        }

        /// <summary> Puts back into the pool the views left on screen by the last disable </summary>
        private void CleanStale() {
            for (int _i = 0; _i < stale.Count; _i++) {
                LineView _view = stale[_i];
                if (_view == null) continue;
                _view.Unbind();
                _view.gameObject.SetActive(false);
                pool.Push(_view);
            }
            stale.Clear();
        }

        /// <summary> The lines of a run that ended leave the screen </summary>
        private void OnRunEnded(DialogueRun _run, EndReason _reason) {
            RemoveLinesOf(_run);
        }

        /// <summary> The lines of a run that starts skipping leave the screen </summary>
        private void OnRunSkipped(DialogueRun _run) {
            RemoveLinesOf(_run);
        }

        private void RemoveLinesOf(DialogueRun _run) {
            for (int _i = handles.Count - 1; _i >= 0; _i--) {
                if (_i < handles.Count && handles[_i].Run == _run) RemoveAt(_i);
            }
        }
        #endregion

        // ==================== CHOICES ====================
        #region Choices
        private void OnChoiceShown(ChoicePrompt _prompt) {
            if (choiceView == null || Main != this) return;
            choiceView.Show(_prompt);
        }

        private void OnChoiceHidden(ChoicePrompt _prompt) {
            if (choiceView == null || choiceView.Prompt != _prompt) return;
            choiceView.Hide();
        }
        #endregion

        // ==================== REGISTRY ====================
        #region Registry
        private static void Register(List<LineContainer> _list, LineContainer _container, bool _add, string _kind) {
            _list.Remove(_container);
            if (!_add) return;
            if (_list.Count > 0) {
                Logger.LogWarning(Dialogue.LogCategory, $"Several {_kind} line containers are enabled. '{_container.name}', the last enabled, is used.");
            }
            _list.Add(_container);
        }
        #endregion

        // ==================== HANDLE ====================
        #region Handle
        /// <summary> One line of the container. Once its view is gone it goes on as a line without view </summary>
        private sealed class LineHandle : ILineHandle, IViewlessLineHandle {
            private readonly LineContainer container;
            private readonly LineView view;
            private readonly LinePresentation presentation;
            private readonly LineLifetime lifetime;
            private readonly AdvanceMode advance;
            private float duration;
            private float typedClock;
            private bool typed;
            private bool finished;
            private bool paused;
            private bool released;
            private bool onScreen = true;
            private ILineHandle fallback;

            public LineHandle(LineContainer _container, LineView _view, LinePresentation _presentation, LineLifetime _lifetime, AdvanceMode _advance) {
                container = _container;
                view = _view;
                presentation = _presentation;
                lifetime = _lifetime;
                advance = _advance;
            }

            public DialogueRun Run => presentation.Run;
            public LineView View => view;

            public int UnfiredEventsFrom {
                get {
                    if (fallback is IViewlessLineHandle _fallback) return _fallback.UnfiredEventsFrom;
                    // Without a view, or a text in it, nothing fires the events that were not passed yet
                    return view == null || view.Text == null ? EventsPassed : -1;
                }
            }

            // Read through the managed reference, so it works once the view is destroyed
            private int EventsPassed => ReferenceEquals(view, null) ? 0 : view.EventsPassed;

            // Input also ends an automatic line that would never end on its own
            private bool AcceptsInput => advance != AdvanceMode.Auto || lifetime.Mode == LineLifetimeMode.UntilNext;

            private EffectText Text => view != null ? view.Text : null;

            public bool IsFinished {
                get {
                    if (fallback != null) return fallback.IsFinished;
                    if (finished) return true;
                    if (view == null) {
                        Detach();
                        return fallback == null || fallback.IsFinished;
                    }
                    if (!typed || advance == AdvanceMode.Input) return false;
                    return Run.Clock - typedClock >= duration;
                }
            }

            /// <summary> Subscribes to the text and binds the line </summary>
            public void Begin() {
                EffectText _text = Text;
                if (_text != null) _text.TypingCompleted += OnTyped;
                view.Bind(presentation);
                // Without a text there is nothing to wait for
                if (_text == null) OnTyped();
            }

            public void Advance() {
                if (fallback != null) {
                    fallback.Advance();
                    return;
                }
                if (finished || released) return;

                EffectText _text = Text;
                if (_text == null) {
                    if (view == null) {
                        Detach();
                        fallback?.Advance();
                    } else if (AcceptsInput) {
                        finished = true;
                    }
                    return;
                }

                switch (_text.State) {
                    case EffectTextState.WaitingInput:
                        // A waitinput in the text asks for input in every mode
                        _text.Continue();
                        break;
                    case EffectTextState.Typing:
                        if (AcceptsInput) _text.Skip();
                        break;
                    default:
                        if (view.IsPreparing) break;
                        if (AcceptsInput) finished = true;
                        break;
                }
            }

            /// <summary> Reveals the whole text, so its skipped text events fire as texteffects rules, and takes the line off the screen </summary>
            public void Skip() {
                if (fallback != null) {
                    fallback.Skip();
                    return;
                }
                finished = true;

                EffectText _text = Text;
                if (view != null && view.IsPreparing) {
                    // The text never arrived, so its events cannot fire from it: the run queues them from the line
                    Run.EnqueueTextEventsForSkip(presentation.Line);
                } else if (_text != null) {
                    // During a run skip the events join the run's skip queue, in order with what the skip queues next
                    if (Run.IsSkipping) view.RedirectEvents(OnSkippedEvent);
                    try {
                        RevealAll(_text);
                    } finally {
                        view.RedirectEvents(null);
                    }
                }
                if (onScreen) container.Remove(this);
            }

            /// <summary> Reveals up to each waitinput and continues until the text is complete. Stops when a pass changes nothing, as when the text defers the calls </summary>
            private static void RevealAll(EffectText _text) {
                for (int _pass = 0; _pass < 1000; _pass++) {
                    EffectTextState _before = _text.State;
                    if (_before == EffectTextState.WaitingInput) {
                        _text.Continue();
                        if (_text.State == EffectTextState.WaitingInput) return;
                    } else if (_before == EffectTextState.Typing) {
                        _text.Skip();
                        if (_text.State == EffectTextState.Typing) return;
                    } else {
                        return;
                    }
                }
            }

            private void OnSkippedEvent(ActionList _actions, LogicContext _context) {
                Run.EnqueueForSkip(_actions, _context.Self);
            }

            public void SetPaused(bool _paused) {
                paused = _paused;
                if (fallback != null) {
                    fallback.SetPaused(_paused);
                    return;
                }
                EffectText _text = Text;
                if (_text == null) return;
                if (_paused) _text.Pause();
                else _text.Resume();
            }

            /// <summary> The run moves on; the view stays on screen until it is replaced or its run ends </summary>
            public void Release() {
                if (released) return;
                released = true;
                if (fallback != null) {
                    fallback.Release();
                    return;
                }
                Unsubscribe();
                EffectText _text = Text;
                if (paused && _text != null) _text.Resume();
            }

            /// <summary> The view left the screen: replaced, skipped or its run ended. A line still running ends </summary>
            public void LeaveScreen() {
                onScreen = false;
                finished = true;
                Unsubscribe();
            }

            /// <summary> The container or the view is gone: a line still running goes on without view </summary>
            public void Detach() {
                onScreen = false;
                Unsubscribe();
                if (fallback != null || finished || released) return;

                LinePresentation _resolved = new LinePresentation(Run, presentation.Line, presentation.Actor, lifetime, advance, presentation.Time);
                fallback = HeadlessLineChannel.Instance.Present(_resolved, EventsPassed);
                if (paused) fallback.SetPaused(true);
            }

            public void OnHidden() {
                StopWaitingHidden();
                container.Returned(this);
            }

            public void StopWaitingHidden() {
                EffectText _text = Text;
                if (_text != null) _text.Hidden -= OnHidden;
            }

            private void OnTyped() {
                if (typed) return;
                typed = true;
                typedClock = Run.Clock;
                duration = lifetime.Resolve(CharacterCount());
            }

            private int CharacterCount() {
                EffectText _text = Text;
                if (_text == null) return 0;
                TMP_Text _tmp = _text.GetComponent<TMP_Text>();
                if (_tmp == null) return 0;
                return _tmp.textInfo != null ? _tmp.textInfo.characterCount : _tmp.text?.Length ?? 0;
            }

            private void Unsubscribe() {
                EffectText _text = Text;
                if (_text != null) _text.TypingCompleted -= OnTyped;
            }
        }
        #endregion
    }
}
