using UnityEngine;

namespace HarryTools.Platformer2DController.Sample {
    [CreateAssetMenu(fileName = "Player Config", menuName = "HarryTools/Platformer 2D Controller/Sample/Player Config")]
    public class PlayerConfig : ScriptableObject {
        // ==================== VARIABLES ===================
        #region Properties
        public PlayerData GetData => data;
        #endregion

        #region Private Variables
        [SerializeField] private PlayerData data;
        #endregion
    }
}