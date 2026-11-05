using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Set Bus Volume")]
    public class SetBusVolumeAction : InstantAction {
        // ==================== VARIABLES ===================
        [SerializeField] private AudioBus bus = AudioBus.SFX;
        [Range(0f, 1f), SerializeField] private float volume = 1f;

        // ==================== CONSTRUCTORS ===================
        public SetBusVolumeAction() { }

        internal SetBusVolumeAction(AudioBus _bus, float _volume) {
            bus = _bus;
            volume = _volume;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            Audio.SetVolume(bus, volume);
        }
    }
}
