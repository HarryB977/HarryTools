using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Interpolates position, rotation or scale. Does not depend on any tween library </summary>
    [Serializable, TypeMenu("Transform/Tween Transform")]
    public class TweenTransformAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject target = new();
        [SerializeField] private TransformProperty property = TransformProperty.Position;
        [SerializeField] private TransformSpace space = TransformSpace.Local;
        [Space]
        [SerializeField] private Vector3 to;
        [Tooltip("'To' is added to the start value instead of being the absolute destination"), SerializeField] private bool relative;
        [Space]
        [SerializeField, Min(0)] private float duration = 1f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool ignoreTimeScale;
        [Tooltip("Waits for it to finish before continuing with the next action"), SerializeField] private bool wait = true;
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            GameObject _gameObject = target.Resolve(_context);
            if (_gameObject == null) return UniTask.CompletedTask;

            // The tween is cancelled if the list is cancelled or the object is destroyed
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
