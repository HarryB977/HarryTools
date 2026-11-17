using System;
using System.Collections.Generic;
using HarryTools.TextEffects;
using UnityEngine;
using UnityEngine.Localization;

namespace HarryTools.Dialogue {
    /// <summary> A spoken line </summary>
    [Serializable]
    public sealed class LineNode : DialogueNode {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private DialogueCharacter character;
        [SerializeField] private LocalizedString text = new LocalizedString();
        [Tooltip("Note for the translators, written to the string table"), SerializeField, TextArea] private string translatorNote = "";
        [Tooltip("Note for the writers, never shown or exported"), SerializeField, TextArea] private string writerNote = "";
        [Tooltip("Seconds to wait before the line starts"), SerializeField, Min(0f)] private float delayBefore;
        [Tooltip("Seconds to wait after the line ends"), SerializeField, Min(0f)] private float delayAfter;
        [Tooltip("Run before the line is shown. The dialogue waits for them"), SerializeField] private SkippableActions before = new SkippableActions();
        [Tooltip("Run when the line is shown. The dialogue does not wait for them"), SerializeField] private SkippableActions onStart = new SkippableActions();
        [Tooltip("Run when the line ends. The dialogue waits for them"), SerializeField] private SkippableActions onEnd = new SkippableActions();
        [Tooltip("Text events used by this line's markup"), SerializeField] private TextEventTable events = new TextEventTable();
        [SerializeField] private bool overrideLifetime;
        [SerializeField] private LineLifetime lifetime = LineLifetime.Default;
        [SerializeField] private bool overrideAdvance;
        [SerializeField] private AdvanceMode advance = AdvanceMode.Both;
        [SerializeField] private NodeOutput next = new NodeOutput();
        #endregion

        #region Public Variables
        public DialogueCharacter Character {
            get => character;
            internal set => character = value;
        }

        public LocalizedString Text => text;

        public string TranslatorNote {
            get => translatorNote;
            internal set => translatorNote = value;
        }

        public string WriterNote {
            get => writerNote;
            internal set => writerNote = value;
        }

        public float DelayBefore {
            get => delayBefore;
            internal set => delayBefore = value;
        }

        public float DelayAfter {
            get => delayAfter;
            internal set => delayAfter = value;
        }

        public SkippableActions Before => before;
        public SkippableActions OnStart => onStart;
        public SkippableActions OnEnd => onEnd;

        public TextEventTable Events {
            get => events;
            internal set => events = value;
        }

        public bool OverrideLifetime {
            get => overrideLifetime;
            internal set => overrideLifetime = value;
        }

        public LineLifetime Lifetime {
            get => lifetime;
            internal set => lifetime = value;
        }

        public bool OverrideAdvance {
            get => overrideAdvance;
            internal set => overrideAdvance = value;
        }

        public AdvanceMode Advance {
            get => advance;
            internal set => advance = value;
        }

        public NodeOutput Next => next;
        #endregion

        // ==================== METHODS ====================
        public override void CollectOutputs(List<NodeOutput> _into) {
            _into.Add(next);
        }
    }
}
