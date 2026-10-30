using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Audio/Stop Audio Source")]
    public class StopAudioSourceAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<AudioSource> audioSource = new();
        [Tooltip("Pausa en lugar de detener"), SerializeField] private bool pause;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            AudioSource _source = audioSource.Resolve(_context);
            if (_source == null) return;

            if (pause) _source.Pause();
            else _source.Stop();
        }
    }
}
