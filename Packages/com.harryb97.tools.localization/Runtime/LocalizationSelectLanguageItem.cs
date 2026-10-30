using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using UnityEngine.UI;
using UnityEngine;

using TMPro;

namespace HarryTools.Localization {
    public class LocalizationSelectLanguageItem : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Public Variables
        #endregion
        
        #region Private Variables
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image flag;
        [SerializeField] private Image check;

        private Locale locale;
        #endregion
        
        // ==================== INICIO ====================
        private void OnEnable() {
            LocalizationSystem.OnSelectedLocaleChanged += UpdateItem;
        }

        private void OnDisable() {
            LocalizationSystem.OnSelectedLocaleChanged -= UpdateItem;
        }
        
        // ==================== METHODS ====================
        public LocalizationSelectLanguageItem Initialize(Locale _locale) {
            locale = _locale;
            UpdateItem();

            return this;
        }

        private void UpdateItem() {
            if (label) label.SetText(locale.Identifier.CultureInfo.NativeName);
            if (check) check.enabled = LocalizationSystem.CurrentLocale == locale;
        }

        public void Call_OnSelectLocal() {
            LocalizationSettings.SelectedLocale = locale;
        }
    }
}