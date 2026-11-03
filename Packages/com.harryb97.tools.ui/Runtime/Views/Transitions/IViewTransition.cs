using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace HarryTools.UI {
    /// <summary> Animación de entrada y salida de una vista. Se asigna desde el inspector con [SerializeReference] </summary>
    public interface IViewTransition {
        UniTask Enter(View _view, CancellationToken _ct);
        UniTask Exit(View _view, CancellationToken _ct);
    }

    internal static class TweenAwait {
        // ==================== METHODS ====================
        /// <summary> Espera a que termine el tween. Si se cancela, lo detiene y lanza OperationCanceledException </summary>
        public static async UniTask Run(Tween _tween, CancellationToken _ct) {
            if (!_tween.isAlive) return;

            try {
                await UniTask.WaitWhile(() => _tween.isAlive, PlayerLoopTiming.Update, _ct);
            }
            catch (OperationCanceledException) {
                if (_tween.isAlive) _tween.Stop();
                throw;
            }
        }

        /// <summary> RectTransform que se anima: el indicado, el primer hijo de la vista o la propia vista </summary>
        public static RectTransform ResolveTarget(View _view, RectTransform _target) {
            if (_target) return _target;
            if (_view.transform.childCount > 0 && _view.transform.GetChild(0) is RectTransform _child) return _child;
            return _view.RectTransform;
        }
    }
}
