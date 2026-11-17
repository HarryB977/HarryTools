using System;
using System.Collections.Generic;
using HarryTools.Actions;
using HarryTools.Conditions;
using UnityEngine;
using UnityEngine.Localization;

namespace HarryTools.Dialogue {
    public enum AfterChosen {
        [Tooltip("The option stays as it was")] Nothing,
        [Tooltip("The option is marked as seen")] MarkSeen,
        [Tooltip("The option can no longer be chosen")] Disable,
        [Tooltip("The option is no longer shown")] Hide
    }

    public enum TimeoutRule {
        [Tooltip("When the timer ends, the selected option is chosen")] ChooseSelected,
        [Tooltip("When the timer ends, the no answer output is taken")] NoAnswer
    }

    /// <summary> One answer of a <see cref="ChoiceNode"/> </summary>
    [Serializable]
    public sealed class ChoiceOption {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private string id = "";
        [SerializeField] private LocalizedString text = new LocalizedString();
        [Tooltip("Note for the translators, written to the string table"), SerializeField, TextArea] private string translatorNote = "";
        [SerializeField] private List<ChoiceTag> tags = new List<ChoiceTag>();
        [Tooltip("The option is shown only when these conditions pass"), SerializeField] private ConditionList visible = new ConditionList();
        [Tooltip("The option can be chosen only when these conditions pass"), SerializeField] private ConditionList available = new ConditionList();
        [Tooltip("Shown when the option is visible but not available"), SerializeField] private LocalizedString lockedReason = new LocalizedString();
        [SerializeField] private bool isDefault;
        [SerializeField] private AfterChosen afterChosen = AfterChosen.Nothing;
        [SerializeField] private ActionList onChosen = new ActionList();
        [SerializeField] private NodeOutput next = new NodeOutput();
        #endregion

        #region Public Variables
        public string Id {
            get => id;
            internal set => id = value;
        }

        public LocalizedString Text => text;

        public string TranslatorNote {
            get => translatorNote;
            internal set => translatorNote = value;
        }

        public List<ChoiceTag> Tags => tags;

        public ConditionList Visible {
            get => visible;
            internal set => visible = value;
        }

        public ConditionList Available {
            get => available;
            internal set => available = value;
        }

        public LocalizedString LockedReason => lockedReason;

        public bool IsDefault {
            get => isDefault;
            internal set => isDefault = value;
        }

        public AfterChosen AfterChosen {
            get => afterChosen;
            internal set => afterChosen = value;
        }

        public ActionList OnChosen {
            get => onChosen;
            internal set => onChosen = value;
        }

        public NodeOutput Next => next;
        #endregion
    }
}
