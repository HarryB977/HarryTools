using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Shakes the character once, fading out the intensity </summary>
    [Serializable, TypeMenu("One Shot/Shake Once")]
    public sealed class ShakeOnceEffect : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const float NoiseSpeed = 25f;

        [Tooltip("Maximum displacement, in units of the character size"), SerializeField] private float amplitude = 0.1f;
        #endregion

        public override EffectKind Kind => EffectKind.OneShot;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            float intensity = 1f - Eased(_time.Progress);
            _char.Translate(ShakeNoise.Offset(_char.Index, _time.GlobalTime * NoiseSpeed, amplitude * _char.Size * intensity));
        }
    }
}
