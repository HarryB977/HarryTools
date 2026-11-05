using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Stop Sound")]
    public class StopSoundAction : InstantAction {
        // ==================== VARIABLES ===================
        [SerializeField] private SoundAsset sound;
        [Tooltip("Fade out in seconds. Negative uses the FadeOut of the asset."), SerializeField] private float fade = -1f;

        // ==================== CONSTRUCTORS ===================
        public StopSoundAction() { }

        internal StopSoundAction(SoundAsset _sound, float _fade) {
            sound = _sound;
            fade = _fade;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            if (sound != null) Audio.Stop(sound, fade);
        }
    }
}
