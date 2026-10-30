using UnityEngine;

namespace HarryTools.Platformer2DController.Sample {
    public class Player : Entity {
        // ==================== VARIABLES ===================
        #region Properties
        public PlayerData GetData => playerConfig.GetData;
        #endregion
        
        #region Private Variables
        [SerializeField] private PlayerConfig playerConfig;
        #endregion
    }
}