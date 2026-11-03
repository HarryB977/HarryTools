using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.SerializeReferences;
using PrimeTween;
using UnityEngine;

namespace HarryTools.UI {
    public enum SlideDirection { Left, Right, Top, Bottom }

    /// <summary> Aparece y desaparece cambiando la opacidad del CanvasGroup de la vista </summary>
    [Serializable]
    public class FadeTransition : IViewTransition {
        public float duration = 0.2f;
        public Ease ease = Ease.OutQuad;

        public UniTask Enter(View _view, CancellationToken _ct) =>
            TweenAwait.Run(Tween.Alpha(_view.CanvasGroup, 0f, 1f, duration, ease, useUnscaledTime: true), _ct);

        public UniTask Exit(View _view, CancellationToken _ct) =>
            TweenAwait.Run(Tween.Alpha(_view.CanvasGroup, _view.CanvasGroup.alpha, 0f, duration, ease, useUnscaledTime: true), _ct);
    }

    /// <summary> Entra desde un lado y sale hacia otro moviendo el 'anchoredPosition' del objetivo </summary>
    [Serializable]
    public class SlideTransition : IViewTransition {
        /// <summary> Lado desde el que entra </summary>
        public SlideDirection from = SlideDirection.Left;
        /// <summary> Lado hacia el que sale </summary>
        public SlideDirection to = SlideDirection.Right;
        /// <summary> Si está vacío, se anima el primer hijo de la vista </summary>
        public RectTransform target;
        public float duration = 0.2f;
        public Ease ease = Ease.OutQuad;

        public UniTask Enter(View _view, CancellationToken _ct) {
            RectTransform _target = TweenAwait.ResolveTarget(_view, target);
            return TweenAwait.Run(Tween.UIAnchoredPosition(_target, Offset(_target, from), Vector2.zero, duration, ease, useUnscaledTime: true), _ct);
        }

        public UniTask Exit(View _view, CancellationToken _ct) {
            RectTransform _target = TweenAwait.ResolveTarget(_view, target);
            return TweenAwait.Run(Tween.UIAnchoredPosition(_target, _target.anchoredPosition, Offset(_target, to), duration, ease, useUnscaledTime: true), _ct);
        }

        private static Vector2 Offset(RectTransform _target, SlideDirection _direction) {
            Rect _rect = _target.rect;
            return _direction switch {
                SlideDirection.Left => Vector2.left * _rect.width,
                SlideDirection.Right => Vector2.right * _rect.width,
                SlideDirection.Top => Vector2.up * _rect.height,
                _ => Vector2.down * _rect.height
            };
        }
    }

    /// <summary> Crece desde 'fromScale' al entrar y vuelve a 'fromScale' al salir </summary>
    [Serializable]
    public class ScaleTransition : IViewTransition {
        public float fromScale = 0.9f;
        /// <summary> Si está vacío, se anima el primer hijo de la vista </summary>
        public RectTransform target;
        public float duration = 0.2f;
        public Ease ease = Ease.OutBack;

        public UniTask Enter(View _view, CancellationToken _ct) {
            RectTransform _target = TweenAwait.ResolveTarget(_view, target);
            return TweenAwait.Run(Tween.Scale(_target, Vector3.one * fromScale, Vector3.one, duration, ease, useUnscaledTime: true), _ct);
        }

        public UniTask Exit(View _view, CancellationToken _ct) {
            RectTransform _target = TweenAwait.ResolveTarget(_view, target);
            return TweenAwait.Run(Tween.Scale(_target, _target.localScale, Vector3.one * fromScale, duration, Ease.InQuad, useUnscaledTime: true), _ct);
        }
    }

    /// <summary> Ejecuta varias transiciones a la vez y termina cuando acaba la más larga </summary>
    [Serializable]
    public class ParallelTransition : IViewTransition {
        [SerializeReference, TypeSelector] public IViewTransition[] transitions = Array.Empty<IViewTransition>();

        public UniTask Enter(View _view, CancellationToken _ct) =>
            UniTask.WhenAll((transitions ?? Array.Empty<IViewTransition>()).Where(_t => _t != null).Select(_t => _t.Enter(_view, _ct)));

        public UniTask Exit(View _view, CancellationToken _ct) =>
            UniTask.WhenAll((transitions ?? Array.Empty<IViewTransition>()).Where(_t => _t != null).Select(_t => _t.Exit(_view, _ct)));
    }
}
