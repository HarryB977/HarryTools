using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.TextEffects;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HarryTools.Dialogue {
    /// <summary> Shows one line with an <see cref="EffectText"/>. A <see cref="LineContainer"/> rents it from its pool, binds a line to it and notifies it as it enters, moves and leaves. Layout and animation belong to subclasses or to other components on the prefab </summary>
    [AddComponentMenu("HarryTools/Dialogue/Line View")]
    public class LineView : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Text that types the line. Leave its Show On Enable off"), SerializeField] private EffectText text;
        [Tooltip("Optional text for the speaker's name"), SerializeField] private TMP_Text speakerName;

        // The voice set on the prefab, used for lines whose character has none
        private TypingVoice prefabVoice;
        private bool prefabVoiceRead;

        // Increases on every bind and unbind so late localization results of older lines are dropped
        private int bindVersion;

        // Runs the text events of the lines. It belongs to the view, not to its EffectText, so going back to the pool does not cancel them
        private readonly TextEventQueue eventQueue = new TextEventQueue();
        private EventForwarder eventForwarder;
        #endregion

        #region Public Variables
        public EffectText Text {
            get => text;
            set => text = value;
        }

        public TMP_Text SpeakerName {
            get => speakerName;
            set => speakerName = value;
        }

        /// <summary> The line shown, or null while the view is in the pool </summary>
        public LinePresentation Current { get; private set; }

        /// <summary> True while the text of the line is being localized. Typing starts once it arrives </summary>
        public bool IsPreparing { get; private set; }
        #endregion

        // ==================== BINDING ====================
        #region Binding
        /// <summary> Sets up the text for the line: speaker name, the character's typing voice, the line's text events with the actor as Self, the run's clock, and then the text, which starts typing once localized </summary>
        public virtual void Bind(LinePresentation _line) {
            Current = _line;
            int _version = ++bindVersion;
            DialogueCharacter _character = _line.Line.Character;

            if (speakerName != null) {
                speakerName.text = string.Empty;
                if (_character != null) Localize(_character.DisplayName, _version, _name => speakerName.text = _name);
            }

            if (text == null) return;
            ReadPrefabVoice();
            text.DefaultVoice = _character != null && _character.TypingVoice != null ? _character.TypingVoice : prefabVoice;
            if (eventForwarder == null) eventForwarder = new EventForwarder(eventQueue);
            eventForwarder.Bind(_line);
            text.SetEventProvider(eventForwarder);
            text.SetEventContext(_line.ActorObject);
            text.TimeMode = ToTimeMode(_line.Time);

            if (Dialogue.LineTextResolver != null) {
                // Tests may keep the line preparing, as a localized text that has not arrived yet
                if (HoldTextForTests) IsPreparing = true;
                else text.SetText(Dialogue.LineTextResolver(_line.Line) ?? string.Empty);
                return;
            }
            IsPreparing = true;
            Localize(_line.Line.Text, _version, _value => {
                IsPreparing = false;
                text.SetText(_value);
            });
        }

        /// <summary> Called after <see cref="Bind"/>, once the view is part of the visible lines </summary>
        public virtual void OnEnter() { }

        /// <summary> Called when the line leaves the visible lines, before its text hides. The view goes back to the pool once hidden. When the container reuses the view, the next line is bound to it right after, without hiding </summary>
        public virtual void OnExit() { }

        /// <summary> Called when the position of the line among the visible lines changes. Index 0 is the oldest </summary>
        public virtual void OnIndexChanged(int _index, int _count) { }
        #endregion

        // ==================== INTERNAL ====================
        #region Internal
        /// <summary> Forgets the line and clears the text before the view goes back to the pool </summary>
        internal void Unbind() {
            bindVersion++;
            Current = null;
            IsPreparing = false;
            eventForwarder?.Clear();
            if (text == null) return;
            text.SetEventProvider(null);
            text.SetEventContext(null);
            if (text.IsPaused) text.Resume();
            ReadPrefabVoice();
            text.DefaultVoice = prefabVoice;
            // An empty text keeps the next enable from typing the old one again
            if (text.isActiveAndEnabled) text.SetText(string.Empty, false);
        }

        /// <summary> When true, lines bound with <see cref="Dialogue.LineTextResolver"/> stay preparing and never get their text </summary>
        [HarryTools.DomainReload.ClearOnReload] internal static bool HoldTextForTests;

        /// <summary> Event tags of the bound line's text the typing has passed, fired or not. Still readable once the view is destroyed </summary>
        internal int EventsPassed => eventForwarder != null ? eventForwarder.Passed : 0;

        /// <summary> While set, the text events that fire are handed to it instead of the view's queue. Used to put the events of a skipped line in the run's skip queue </summary>
        internal void RedirectEvents(Action<ActionList, LogicContext> _sink) {
            if (eventForwarder != null) eventForwarder.Sink = _sink;
        }

        /// <summary> The text clock that matches the run's clock </summary>
        internal static TimeMode ToTimeMode(DialogueTime _time) {
            switch (_time) {
                case DialogueTime.Scaled: return TimeMode.Scaled;
                case DialogueTime.Unscaled: return TimeMode.Unscaled;
                default: return TimeMode.Unscaled;
            }
        }
        #endregion

        // ==================== UNITY ====================
        #region Unity
        protected virtual void OnDestroy() {
            eventQueue.Cancel();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void ReadPrefabVoice() {
            if (prefabVoiceRead || text == null) return;
            prefabVoiceRead = true;
            prefabVoice = text.DefaultVoice;
        }

        /// <summary> Resolves the string and hands it over unless another line was bound meanwhile. An empty or failing string gives an empty text </summary>
        private void Localize(LocalizedString _string, int _version, Action<string> _apply) {
            if (_string == null || _string.IsEmpty) {
                _apply(string.Empty);
                return;
            }

            AsyncOperationHandle<string> _operation;
            try {
                _operation = _string.GetLocalizedStringAsync();
            } catch (Exception) {
                _apply(string.Empty);
                return;
            }

            if (_operation.IsDone) {
                _apply(Result(_operation));
                return;
            }
            _operation.Completed += _done => {
                if (this == null || _version != bindVersion) return;
                _apply(Result(_done));
            };
        }

        private static string Result(AsyncOperationHandle<string> _operation) {
            return _operation.Status == AsyncOperationStatus.Succeeded ? _operation.Result ?? string.Empty : string.Empty;
        }
        #endregion

        // ==================== EVENTS ====================
        #region Events
        /// <summary> Hands the line's text events to the EffectText as actions that only queue the real ones in the view's queue, with the context of the line's other actions. The EffectText still decides which events fire and whether skipped ones run </summary>
        private sealed class EventForwarder : ITextEventProvider {
            private readonly TextEventQueue queue;
            private readonly Dictionary<ActionList, ActionList> forwarders = new Dictionary<ActionList, ActionList>();
            private ITextEventProvider source;
            private LinePresentation line;

            public Action<ActionList, LogicContext> Sink;

            /// <summary> Event tags passed since the line was bound. The EffectText asks for every tag it passes, fired or skipped </summary>
            public int Passed { get; private set; }

            public EventForwarder(TextEventQueue _queue) {
                queue = _queue;
            }

            public void Bind(LinePresentation _line) {
                line = _line;
                source = _line.Line.Events;
                Passed = 0;
            }

            public bool TryGetEvent(string _name, out ActionList _actions, out bool _runWhenSkipped) {
                _actions = null;
                _runWhenSkipped = false;
                if (line == null) return false;
                Passed++;
                if (source == null || !source.TryGetEvent(_name, out ActionList _inner, out _runWhenSkipped) || _inner == null) return false;
                if (!forwarders.TryGetValue(_inner, out _actions)) {
                    _actions = new ActionList(new EnqueueAction(this, _inner));
                    forwarders.Add(_inner, _actions);
                }
                return true;
            }

            /// <summary> Self is the line's actor, or none, and Target the run's origin, as for the line's other actions </summary>
            public void Enqueue(ActionList _actions, LogicContext _context) {
                if (line != null) _context = new LogicContext(line.ActorObject, line.Run.Options.Origin);
                if (Sink != null) Sink(_actions, _context);
                else queue.Enqueue(_actions, _context);
            }

            public void Clear() {
                line = null;
                source = null;
                Sink = null;
                Passed = 0;
                forwarders.Clear();
            }
        }

        private sealed class EnqueueAction : IAction {
            private readonly EventForwarder forwarder;
            private readonly ActionList actions;

            public EnqueueAction(EventForwarder _forwarder, ActionList _actions) {
                forwarder = _forwarder;
                actions = _actions;
            }

            public UniTask Run(LogicContext _context, CancellationToken _token) {
                forwarder.Enqueue(actions, _context);
                return UniTask.CompletedTask;
            }
        }
        #endregion
    }
}
