using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Components/Audio Source Is Playing")]
    public class AudioSourceIsPlaying : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<AudioSource> audioSource = new();
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) {
            AudioSource _source = audioSource.Resolve(_context);
            return _source != null && _source.isPlaying;
        }
    }
}
