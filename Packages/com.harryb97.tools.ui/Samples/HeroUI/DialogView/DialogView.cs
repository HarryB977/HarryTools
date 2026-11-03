using System.Collections;
using UnityEngine;

namespace HarryTools.UI
{
    public class DialogView : MonoView
    {
        [SerializeField] RectTransform _content;
        [SerializeField] TMPro.TMP_Text _title;
        [SerializeField] TMPro.TMP_Text _message;

        protected override IEnumerator OnEnterTransition()
        {
            var fade = ViewTransitions.FadeIn(this);
            var slide = ViewTransitions.SlideFromLeft(_content);
            return ViewTransitions.Parallel(fade, slide);
        }

        protected override IEnumerator OnExitTransition()
        {
            var fade = ViewTransitions.FadeOut(this);
            var slide = ViewTransitions.SlideToRight(_content);
            return ViewTransitions.Parallel(fade, slide);
        }

        public void Setup(string title, string message)
        {
            _title.text = title;
            _message.text = message;
        }
    }
}
