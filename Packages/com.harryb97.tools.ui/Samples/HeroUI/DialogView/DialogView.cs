using UnityEngine;

namespace HarryTools.UI.HeroUI
{
    /// <summary> Vista de diálogo de ejemplo. Su transición (fade + slide) se configura en el prefab </summary>
    public class DialogView : View
    {
        [SerializeField] RectTransform _content;
        [SerializeField] TMPro.TMP_Text _title;
        [SerializeField] TMPro.TMP_Text _message;

        public void Setup(string title, string message)
        {
            _title.text = title;
            _message.text = message;
        }
    }
}
