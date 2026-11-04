using UnityEngine;

namespace HarryTools.MultiScene {
    [CreateAssetMenu(fileName = "Scene Groups Config", menuName = "HarryTools/MultiScene/Scene Groups Config")]
    public class SceneGroupsConfig : ScriptableObject {
        #region Public Variables
        public SceneGroup[] sceneGroups;
        #endregion
    }
}