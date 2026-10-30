using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Audio/Play Clip At Point")]
    public class PlayClipAtPointAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private AudioClip clip;
        [SerializeField] private TargetGameObject position = new(TargetSource.Self);
        [SerializeField, Range(0, 1)] private float volume = 1f;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            GameObject _position = position.Resolve(_context);
            if (clip == null || _position == null) return;

            AudioSource.PlayClipAtPoint(clip, _position.transform.position, volume);
        }
    }
}
