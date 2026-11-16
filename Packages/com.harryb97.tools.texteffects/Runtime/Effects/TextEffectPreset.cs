using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Maps a tag name to an effect </summary>
    [CreateAssetMenu(menuName = "HarryTools/Text Effects/Effect Preset", fileName = "Effect Preset")]
    public sealed class TextEffectPreset : ScriptableObject {
        // ==================== VARIABLES ===================
        #region Public Variables
        public string TagName => tagName;
        public TextEffect Effect => effect;
        #endregion

        #region Private Variables
        [Tooltip("Name used in the markup to apply this effect"), SerializeField]
        private string tagName;

        [SerializeReference, TypeSelector]
        private TextEffect effect;
        #endregion
    }
}
