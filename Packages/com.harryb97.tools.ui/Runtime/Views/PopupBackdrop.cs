using UnityEngine;
using UnityEngine.EventSystems;

namespace HarryTools.UI {
    /// <summary> Fondo oscurecido de la capa de popups: al hacer clic oculta el popup superior </summary>
    public class PopupBackdrop : MonoBehaviour, IPointerClickHandler {
        internal PopupLayer Layer { get; set; }

        public void OnPointerClick(PointerEventData _eventData) {
            PopupLayer _layer = Layer ? Layer : GetComponentInParent<PopupLayer>();
            if (_layer) _layer.OnBackdropClicked();
        }
    }
}
