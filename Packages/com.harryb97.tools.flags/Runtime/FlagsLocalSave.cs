using Cysharp.Threading.Tasks;
using HarryTools.LocalData;

namespace HarryTools.Flags
{
    /// <summary>
    /// Optional helper that saves and loads the flag state through LocalDataSystem. Games with their own save system can use Flags.Capture and Flags.Restore directly.
    /// </summary>
    public static class FlagsLocalSave
    {
        // ==================== PUBLIC API ====================

        /// <summary>Writes the current flag state to a file, replacing any previous one.</summary>
        public static UniTask<bool> SaveAsync(string _directory, string _file)
        {
            return LocalDataSystem.OverwriteData(Flags.Capture(), _directory, _file);
        }

        /// <summary>Restores the flag state from a file. Returns false and leaves the state untouched if the file is missing or unreadable.</summary>
        public static async UniTask<bool> LoadAsync(string _directory, string _file)
        {
            FlagSnapshot _snapshot = await LocalDataSystem.LoadData<FlagSnapshot>(_directory, _file);
            if (_snapshot == null) return false;

            Flags.Restore(_snapshot);
            return true;
        }
    }
}
