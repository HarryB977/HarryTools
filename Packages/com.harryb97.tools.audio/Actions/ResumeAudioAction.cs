using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Resume Audio")]
    public class ResumeAudioAction : InstantAction {
        // ==================== VARIABLES ===================
        [Tooltip("Resumes every bus. Off resumes only the bus below."), SerializeField] private bool allBuses = true;
        [SerializeField] private AudioBus bus = AudioBus.SFX;

        // ==================== CONSTRUCTORS ===================
        public ResumeAudioAction() { }

        internal ResumeAudioAction(bool _allBuses, AudioBus _bus) {
            allBuses = _allBuses;
            bus = _bus;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            if (allBuses) Audio.Resume();
            else Audio.Resume(bus);
        }
    }
}
