using UnityEngine;

namespace HarryTools.LocalData.Sample {
    public class LocalDataManager : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Public Variables
        public TestData Data { get; private set; }
        #endregion
        
        #region Private Variables
        [SerializeField] private string directoryName = "Local Data Directory";
        [SerializeField] private string fileName = "LocalDataFile";
        #endregion
        
        // ==================== START ====================
        private void Start() {
            LoadLocalData();
        }
        
        // ==================== METHODS ====================
        [ContextMenu("Load Local Data")]
        public async void LoadLocalData() {
            if (LocalDataSystem.CheckData(directoryName, fileName)) Data = await LocalDataSystem.LoadData<TestData>(directoryName, fileName);
            else {
                Data = new();
                await LocalDataSystem.SaveData(Data, directoryName, fileName);
            }
        }

        [ContextMenu("Save Local Data")]
        public async void SaveLocalData() {
            if (LocalDataSystem.CheckData(directoryName, fileName)) await LocalDataSystem.OverwriteData(Data, directoryName, fileName);
            else await LocalDataSystem.SaveData(Data, directoryName, fileName);
        }

        [ContextMenu("Remove Local Data")]
        public async void RemoveLocalData() {
            if (LocalDataSystem.CheckData(directoryName, fileName)) {
                await LocalDataSystem.RemoveData(directoryName, fileName);
            }
        }
    }
}