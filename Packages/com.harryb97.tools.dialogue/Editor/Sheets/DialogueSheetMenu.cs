using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Entry points of the Google Sheets sync for menus, windows and the settings page. The Run methods return the outcome without any dialog; the others show it </summary>
    internal static class DialogueSheetMenu {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string MenuRoot = "Window/HarryTools/Dialogue/";
        private const string DialogTitle = "Google Sheets";
        #endregion

        // ==================== MENU ====================
        #region Menu
        [MenuItem(MenuRoot + "Push all")]
        private static void PushAllMenu() {
            SyncAll(true);
        }

        [MenuItem(MenuRoot + "Pull all")]
        private static void PullAllMenu() {
            SyncAll(false);
        }

        [MenuItem(MenuRoot + "Push all", true)]
        [MenuItem(MenuRoot + "Pull all", true)]
        private static bool CanSync() {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> A sync through Google with the project's sheet settings. Null, with the reason, when they are incomplete </summary>
        internal static DialogueSheetSync CreateSync(out string _error) {
            DialogueSheetsSettings _settings = DialogueSheetsSettings.instance;
            _error = GoogleSheetGateway.ConfigurationError(_settings.Provider, _settings.SpreadsheetId);
            return _error == null ? new DialogueSheetSync(new GoogleSheetGateway(_settings.Provider, _settings.SpreadsheetId), _settings) : null;
        }

        /// <summary> Tries to reach the spreadsheet of the project's sheet settings </summary>
        internal static SheetConnection TestConnection() {
            DialogueSheetsSettings _settings = DialogueSheetsSettings.instance;
            IDialogueSheetGateway _gateway = new GoogleSheetGateway(_settings.Provider, _settings.SpreadsheetId);
            return _gateway.Test();
        }

        /// <summary> Pushes or pulls every dialogue of the project and the characters </summary>
        internal static SyncSummary RunAll(bool _push) {
            DialogueSheetSync _sync = CreateSync(out string _error);
            if (_sync == null) return new SyncSummary(_push, _error);
            return _push ? _sync.PushAll() : _sync.PullAll();
        }

        /// <summary> Pushes or pulls one dialogue </summary>
        internal static SyncResult RunOne(DialogueAsset _asset, bool _push) {
            DialogueSheetSync _sync = CreateSync(out string _error);
            if (_sync == null) return SyncResult.Failed(_asset != null ? _asset.name : "", _error);
            return _push ? _sync.Push(_asset) : _sync.Pull(_asset);
        }

        /// <summary> Pushes or pulls the character names </summary>
        internal static SyncResult RunCharacters(bool _push) {
            DialogueSheetSync _sync = CreateSync(out string _error);
            if (_sync == null) return SyncResult.Failed("Characters", _error);
            return _push ? _sync.PushCharacters() : _sync.PullCharacters();
        }

        /// <summary> Syncs every dialogue and the characters, then shows the summary </summary>
        internal static void SyncAll(bool _push) {
            SyncSummary _summary = RunAll(_push);
            EditorUtility.DisplayDialog(DialogTitle, _summary.Describe(), "OK");
        }

        /// <summary> Syncs one dialogue. A failure is shown in a dialog, a success in the console </summary>
        internal static void SyncOne(DialogueAsset _asset, bool _push) {
            Show(RunOne(_asset, _push), _push);
        }

        /// <summary> Syncs the character names. A failure is shown in a dialog, a success in the console </summary>
        internal static void SyncCharacters(bool _push) {
            Show(RunCharacters(_push), _push);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void Show(SyncResult _result, bool _push) {
            if (!_result.Succeeded) {
                EditorUtility.DisplayDialog(DialogTitle, (_push ? "Push" : "Pull") + " of " + _result.Name + " failed.\n" + _result.Error, "OK");
                return;
            }
            // Unknown rows and conflicts were already reported as a warning by the sync
            if (_result.UnknownRows == 0 && _result.Conflicts == 0) Debug.Log((_push ? "Pushed " : "Pulled ") + _result.Name + ". " + _result.Counts());
        }
        #endregion
    }
}
