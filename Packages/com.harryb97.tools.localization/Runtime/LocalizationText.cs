using UnityEngine;
using UnityEngine.Localization;

using Cysharp.Threading.Tasks;
using TMPro;

namespace HarryTools.Localization {
    public class LocalizationText : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private LocalizedString key;
        [SerializeField] private bool initializeOnStart = true;
        [Space]
        [SerializeField] private TextMeshProUGUI meshLabel = null;
        #endregion
        
        // ==================== INICIO ====================
        private void Reset() {
            TryGetComponent(out meshLabel);
        }

        private void OnEnable() {
            LocalizationSystem.OnSelectedLocaleChanged += UpdateText;
        }

        private void OnDisable() {
            LocalizationSystem.OnSelectedLocaleChanged -= UpdateText;
        }

        private void Start() {
            UpdateText();
        }
        
        // ==================== METHODS ====================
        public void SetKey(LocalizedString _key) {
            key = _key;
            UpdateText();
        }
        
        private async void UpdateText() {
            while (!LocalizationSystem.Initialized) {
                await UniTask.Yield();
            }

            if (meshLabel && key.TableReference != null) {
                meshLabel.SetText(await key.GetLocalizedStringAsync());
            }
        }
    }
}