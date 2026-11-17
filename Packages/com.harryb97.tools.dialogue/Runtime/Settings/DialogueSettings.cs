using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Dialogue {
    public enum HistoryClear {
        [Tooltip("The history is emptied when a conversation ends")] OnConversationEnd,
        [Tooltip("The history is kept until it is cleared in code")] Never
    }

    /// <summary> Project-wide defaults for dialogues. Without an asset, the defaults below apply silently </summary>
    public sealed class DialogueSettings : ScriptableObject {
        // ==================== VARIABLES ===================
        #region Public Variables
        public const string ResourcesName = "DialogueSettings";

        public static DialogueSettings Current {
            get {
                if (current == null) current = LoadOrDefaults();
                return current;
            }
        }

        public DialogueTime DefaultTime => defaultTime;
        public AmbientPolicy DefaultAmbientPolicy => defaultAmbientPolicy;
        public LineLifetime DefaultLifetime => defaultLifetime;
        public AdvanceMode DefaultAdvance => defaultAdvance;
        public bool HistoryEnabled => historyEnabled;
        public int HistoryMax => historyMax;
        public HistoryClear HistoryClear => historyClear;
        public string TablesFolder => tablesFolder;
        public string SourceLocaleCode => sourceLocaleCode;
        #endregion

        #region Private Variables
        [ClearOnReload] private static DialogueSettings current;

        [Tooltip("Clock used by dialogues that do not set their own"), SerializeField]
        private DialogueTime defaultTime = DialogueTime.Unscaled;

        [Tooltip("What ambient dialogues do while a main dialogue plays"), SerializeField]
        private AmbientPolicy defaultAmbientPolicy = AmbientPolicy.Continue;

        [Tooltip("How long a line stays on screen when it does not set its own"), SerializeField]
        private LineLifetime defaultLifetime = LineLifetime.Default;

        [Tooltip("How lines advance when they do not set their own"), SerializeField]
        private AdvanceMode defaultAdvance = AdvanceMode.Both;

        [Tooltip("Record the lines and choices that were shown"), SerializeField]
        private bool historyEnabled = false;

        [Tooltip("Most entries the history keeps"), SerializeField, Min(1)]
        private int historyMax = 200;

        [Tooltip("When the history is emptied"), SerializeField]
        private HistoryClear historyClear = HistoryClear.OnConversationEnd;

        [Tooltip("Folder where the string tables of the dialogues are created"), SerializeField]
        private string tablesFolder = "Assets/Localization/Dialogue";

        [Tooltip("Locale the dialogue text is written in. Empty uses the project locale"), SerializeField]
        private string sourceLocaleCode = "";

        // The in-memory instance used when no asset exists, which Current drops once the asset appears
        private bool isFallback;
        #endregion

        // ==================== INTERNAL ====================
        #region Internal
        internal static void UseForTests(DialogueSettings _settings) {
            current = _settings;
        }

        internal static void ClearForTests() {
            current = null;
        }

        /// <summary> When Current holds the in-memory defaults, drops them so the next read loads the asset </summary>
        internal static void ForgetFallback() {
            if (current != null && current.isFallback) current = null;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static DialogueSettings LoadOrDefaults() {
            DialogueSettings _loaded = Resources.Load<DialogueSettings>(ResourcesName);
            if (_loaded != null) return _loaded;

            DialogueSettings _defaults = CreateInstance<DialogueSettings>();
            _defaults.hideFlags = HideFlags.DontSave;
            _defaults.isFallback = true;
            return _defaults;
        }
        #endregion
    }
}
