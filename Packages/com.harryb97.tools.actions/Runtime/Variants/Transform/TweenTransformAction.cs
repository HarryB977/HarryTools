using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Interpola posición, rotación o escala. No depende de ninguna librería de tweens </summary>
    [Serializable, TypeMenu("Transform/Tween Transform")]
    public class TweenTransformAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject target = new();
        [SerializeField] private TransformProperty property = TransformProperty.Position;
        [SerializeField] private TransformSpace space = TransformSpace.Local;
        [Space]
        [SerializeField] private Vector3 to;
        [Tooltip("'To' se suma al valor inicial en lugar de ser el destino absoluto"), SerializeField] private bool relative;
        [Space]
        [SerializeField, Min(0)] private float duration = 1f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool ignoreTimeScale;
        [Tooltip("Espera a que termine antes de continuar con la siguiente acción"), SerializeField] private bool wait = true;
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            GameObject _gameObject = target.Resolve(_context);
            if (_gameObject == null) return UniTask.CompletedTask;

            // El tween se cancela si se cancela la lista o si se destruye el objeto
            CancellationTokenSource _source = CancellationTokenSource.CreateLinkedTokenSource(_token, _gameObject.GetCancellationTokenOnDestroy());
            UniTask _tween = Tween(_gameObject.transform, _source);

            if (wait) return _tween;

            _tween.Forget();
            return UniTask.CompletedTask;
        }

        private async UniTask Tween(Transform _transform, CancellationTokenSource _source) {
            using (_source) {
                Vector3 _from = _transform.Get(property, space);
                Vector3 _to = relative ? _from + to : to;
                float _elapsed = 0;

                while (_elapsed < duration) {
                    _transform.Set(property, space, Vector3.LerpUnclamped(_from, _to, ease.Evaluate(_elapsed / duration)));

                    await UniTask.Yield(PlayerLoopTiming.Update, _source.Token);
                    _elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                }

                _transform.Set(property, space, _to);
            }
        }
    }
}
