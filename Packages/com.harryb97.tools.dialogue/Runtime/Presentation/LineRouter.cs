using System.Collections.Generic;
using HarryTools.DomainReload;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Dialogue {
    /// <summary> Picks the line container of each line: the main container for the main dialogue; for ambient ones, the actor's container, then the default ambient container. Without any, the line is not shown and the dialogue goes on </summary>
    internal static class LineRouter {
        // ==================== VARIABLES ===================
        #region Private Variables
        // Runs already warned about lines without a container
        [ClearOnReload(AssignNewInstance = true)] private static HashSet<DialogueRun> warned = new HashSet<DialogueRun>();
        #endregion

        // ==================== METHODS ====================
        #region Internal Methods
        internal static ILineChannel Resolve(LinePresentation _line) {
            DialogueRun _run = _line.Run;
            LineContainer _container = _run.IsMain ? LineContainer.Main : AmbientContainer(_line.Actor);
            if (_container != null) return _container;

            Warn(_run);
            return HeadlessLineChannel.Instance;
        }

        internal static void ClearForTests() {
            warned.Clear();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static LineContainer AmbientContainer(DialogueActor _actor) {
            if (_actor != null && _actor.Container != null && _actor.Container.isActiveAndEnabled) return _actor.Container;
            return LineContainer.DefaultAmbient;
        }

        private static void Warn(DialogueRun _run) {
            warned.RemoveWhere(_other => _other.State == RunState.Ended);
            if (!warned.Add(_run)) return;
            string _missing = _run.IsMain
                ? "there is no main line container"
                : "the line's actor has no line container and there is no default ambient container";
            Logger.LogWarning(Dialogue.LogCategory, $"Dialogue '{_run.Asset.name}': {_missing}. Lines without a container are not shown; the dialogue goes on.");
        }
        #endregion
    }
}
