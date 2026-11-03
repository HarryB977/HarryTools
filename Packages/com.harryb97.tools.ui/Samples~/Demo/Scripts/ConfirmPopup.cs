using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HarryTools.UI.Demo {
    /// <summary> Popup con título, mensaje y botón de aceptar </summary>
    public class ConfirmPopup : View {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button acceptButton;
        #endregion

        // ==================== START ====================
        protected override void Awake() {
            base.Awake();
            acceptButton.onClick.AddListener(Hide);
        }

        // ==================== METHODS ====================
        public ConfirmPopup Setup(string _title, string _message) {
            title.text = _title;
            message.text = _message;
            return this;
        }
    }
}
