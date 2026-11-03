using System.Collections;
using UnityEngine;

namespace HarryTools.UI
{
    public static class ViewTransitions
    {
        private const float DEFAULT_DURATION = 0.2f;

        public static IEnumerator Parallel(params IEnumerator[] transitions)
        {
            while (true)
            {
                bool anyRunning = false;
                for (int i = 0; i < transitions.Length; i++)
                {
                    if (transitions[i] != null && transitions[i].MoveNext())
                        anyRunning = true;
                    else
                        transitions[i] = null;
                }
                if (!anyRunning) yield break;
                yield return null;
            }
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            group.alpha = from;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float n = t / duration;
                n = n * n * (3f - 2f * n);
                group.alpha = Mathf.LerpUnclamped(from, to, n);
                yield return null;
            }
            group.alpha = to;
        }

        public static IEnumerator Slide(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            rect.anchoredPosition = from;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float n = t / duration;
                n = n * n * (3f - 2f * n);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, n);
                yield return null;
            }
            rect.anchoredPosition = to;
        }

        public static IEnumerator FadeIn(MonoView view, float duration = DEFAULT_DURATION)
            => Fade(view.canvasGroup, 0f, 1f, duration);

        public static IEnumerator FadeOut(MonoView view, float duration = DEFAULT_DURATION)
            => Fade(view.canvasGroup, 1f, 0f, duration);

        public static IEnumerator SlideFromLeft(RectTransform content, float duration = DEFAULT_DURATION)
            => Slide(content, Vector2.left * content.rect.width, Vector2.zero, duration);

        public static IEnumerator SlideToLeft(RectTransform content, float duration = DEFAULT_DURATION)
            => Slide(content, Vector2.zero, Vector2.left * content.rect.width, duration);

        public static IEnumerator SlideFromRight(RectTransform content, float duration = DEFAULT_DURATION)
            => Slide(content, Vector2.right * content.rect.width, Vector2.zero, duration);

        public static IEnumerator SlideToRight(RectTransform content, float duration = DEFAULT_DURATION)
            => Slide(content, Vector2.zero, Vector2.right * content.rect.width, duration);

        public static IEnumerator SlideFromBottom(RectTransform content, float duration = DEFAULT_DURATION)
            => Slide(content, Vector2.down * content.rect.height, Vector2.zero, duration);

        public static IEnumerator SlideToBottom(RectTransform content, float duration = DEFAULT_DURATION)
            => Slide(content, Vector2.zero, Vector2.down * content.rect.height, duration);

        public static IEnumerator SlideFromTop(RectTransform content, float duration = DEFAULT_DURATION)
            => Slide(content, Vector2.up * content.rect.height, Vector2.zero, duration);

        public static IEnumerator SlideToTop(RectTransform content, float duration = DEFAULT_DURATION)
            => Slide(content, Vector2.zero, Vector2.up * content.rect.height, duration);
    }
}
