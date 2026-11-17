using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Dialogue {
    public enum StopTarget {
        [Tooltip("The main dialogue")] Main,
        [Tooltip("Every dialogue running")] All,
        [Tooltip("Every run of one dialogue")] Dialogue
    }

    /// <summary> Stops running dialogues </summary>
    [Serializable, TypeMenu("Dialogue/Stop Dialogue")]
    public sealed class StopDialogueAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private StopTarget target = StopTarget.Main;
        [SerializeField] private DialogueAsset dialogue;
        #endregion

        // ==================== START ====================
        public StopDialogueAction() { }

        public StopDialogueAction(StopTarget _target, DialogueAsset _dialogue = null) {
            target = _target;
            dialogue = _dialogue;
        }

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            switch (target) {
                case StopTarget.Main:
                    if (Dialogue.Main != null) Dialogue.Main.Stop();
                    break;

                case StopTarget.All:
                    Dialogue.StopAll();
                    break;

                case StopTarget.Dialogue:
                    if (dialogue == null) break;
                    DialogueRun[] _runs = new DialogueRun[Dialogue.Active.Count];
                    for (int _i = 0; _i < _runs.Length; _i++) _runs[_i] = Dialogue.Active[_i];
                    for (int _i = 0; _i < _runs.Length; _i++) {
                        if (_runs[_i].Asset == dialogue) _runs[_i].Stop();
                    }
                    break;
            }
        }
    }
}
