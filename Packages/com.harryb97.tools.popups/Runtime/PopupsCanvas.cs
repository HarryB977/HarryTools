using UnityEngine;

using HarryTools.UICanvas;

using PrimeTween;
using HarryUtils;

namespace HarryTools.Popups {
    public class PopupsCanvas : CustomParentCanvas<Popup, Popup.PopupParameters> {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Header("Popups Canvas")]
        [SerializeField] private float animationDuration = 0.35f;
        #endregion

        // ==================== METHODS ====================
        public override void ChangeBackgroundColor(Color _color) {
            Tween.Custom(backgroundImage.color, _color, animationDuration,
                x => backgroundImage.color = x
            );
        }

        public override async void ShowBackground() {
            if (IsShowingBackground) return;
            IsShowingBackground = true;
            
            background.SetInteractable(true);
            graphicRaycaster.enabled = true;
            
            await Tween.Alpha(background, 0, 1, animationDuration);
        }

        public override async void HideBackground() {
            if (!IsShowingBackground) return;
            IsShowingBackground = false;
            
            background.SetInteractable(false);
            graphicRaycaster.enabled = false;

            await Tween.Alpha(background, 1, 0, animationDuration);
        }

        #region Callbacks
        public void Call_OnBackgroundClick() {
            PopupManager.Instance.HideCurrentPopup();
        }
        #endregion
    }
}