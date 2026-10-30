using UnityEngine.Localization.Settings;
using UnityEngine;

namespace HarryTools.Localization {
    public class LocalizationSelectLanguage : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Public Variables
        #endregion
        
        #region Private Variables
        [SerializeField] private LocalizationSelectLanguageItem item;
        [SerializeField] private Transform content;

        private bool initialized = false;
        #endregion
        
        // ==================== INICIO ====================
        private void OnEnable() {
            LocalizationSystem.OnInitalize += Initialize;
        }

        private void OnDisable() {
            LocalizationSystem.OnInitalize -= Initialize;
        }

        private void Start() {
            if (LocalizationSystem.Initialized) Initialize();
        }
        
        // ==================== METHODS ====================
        private void Initialize() {
            if (initialized) return;
            initialized = true;
            
            for (int i = 0; i < LocalizationSettings.AvailableLocales.Locales.Count; i++) {
                Instantiate(item, content).Initialize(LocalizationSettings.AvailableLocales.Locales[i]);
            }
        }
    }
}