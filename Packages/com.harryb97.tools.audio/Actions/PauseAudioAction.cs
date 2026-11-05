using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Pause Audio")]
    public class PauseAudioAction : InstantAction {
        // ==================== VARIABLES ===================
        [Tooltip("Pauses every bus. Off pauses only the bus below."), SerializeField] private bool allBuses = true;
        [SerializeField] private AudioBus bus = AudioBus.SFX;

        // ==================== CONSTRUCTORS ===================
        public PauseAudioAction() { }

        internal PauseAudioAction(bool _allBuses, AudioBus _bus) {
            allBuses = _allBuses;
            bus = _bus;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            if (allBuses) Audio.Pause();
            else Audio.Pause(bus);
        }
    }
}
