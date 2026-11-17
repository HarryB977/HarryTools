using Cysharp.Threading.Tasks;
using HarryTools.LocalData;

namespace HarryTools.Dialogue {
    /// <summary> Optional helper that saves and loads the dialogue memory through LocalDataSystem. Games with their own save system can use DialogueMemory.Capture and DialogueMemory.Restore directly </summary>
    public static class DialogueMemoryLocalSave {
        // ==================== PUBLIC API ====================
        #region Public API
        /// <summary> Writes the current memory to a file, replacing any previous one </summary>
        public static UniTask<bool> SaveAsync(string _directory, string _file) {
            return LocalDataSystem.OverwriteData(DialogueMemory.Capture(), _directory, _file);
        }

        /// <summary> Restores the memory from a file. Returns false and leaves the memory untouched if the file is missing or unreadable </summary>
        public static async UniTask<bool> LoadAsync(string _directory, string _file) {
            DialogueMemorySnapshot _snapshot = await LocalDataSystem.LoadData<DialogueMemorySnapshot>(_directory, _file);
            if (_snapshot == null) return false;

            DialogueMemory.Restore(_snapshot);
            return true;
        }
        #endregion
    }
}
