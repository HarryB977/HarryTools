using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Stop Music")]
    public class StopMusicAction : InstantAction {
        // ==================== VARIABLES ===================
        [Tooltip("Fade out in seconds. Negative uses the FadeOut of the music asset."), SerializeField] private float fade = -1f;

        // ==================== CONSTRUCTORS ===================
        public StopMusicAction() { }

        internal StopMusicAction(float _fade) {
            fade = _fade;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            Audio.Music?.Stop(fade);
        }
    }
}
