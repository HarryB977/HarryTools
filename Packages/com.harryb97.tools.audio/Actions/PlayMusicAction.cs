using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Play Music")]
    public class PlayMusicAction : InstantAction {
        // ==================== VARIABLES ===================
        [SerializeField] private MusicAsset music;
        [Tooltip("Crossfade in seconds. Negative uses the default of the music channel."), SerializeField] private float crossfade = -1f;

        // ==================== CONSTRUCTORS ===================
        public PlayMusicAction() { }

        internal PlayMusicAction(MusicAsset _music, float _crossfade) {
            music = _music;
            crossfade = _crossfade;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            if (music != null) Audio.Music?.Play(music, crossfade);
        }
    }
}
