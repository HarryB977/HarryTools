using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HarryTools.UI {
    /// <summary>
    /// Capa de overlays: tapa a Views y Popups y solo tiene uno activo a la vez.
    /// Pensada para pantallas de carga con <see cref="ShowWhile{T}(UniTask, CancellationToken)"/>.
    /// </summary>
    public class OverlayLayer : ViewLayer {
        // ==================== VARIABLES ===================
        #region Properties
        /// <summary> Overlay visible, o null </summary>
        public View Current => Top;
        #endregion

        #region Private Variables
        /// <summary> Overlays pedidos por las llamadas a ShowWhile en curso (el último manda) </summary>
        private readonly System.Collections.Generic.List<View> requests = new();
        #endregion

        // ==================== METHODS ====================
        #region Show & Hide
        protected override void ShowInstance(View _view) {
            foreach (View _other in visible.ToArray()) {
                if (_other != _view) base.Hide(_other);
            }

            base.ShowInstance(_view);
        }

        /// <summary> Oculta el overlay activo, si lo hay </summary>
        public void Hide() {
            if (Current) Hide(Current);
        }
        #endregion

        // =================================================

        #region Show While
        /// <summary> Muestra el overlay T mientras dura la operación y lo oculta al terminar. Le pasa el progreso si implementa IProgress&lt;float&gt; </summary>
        public async UniTask ShowWhile<T>(AsyncOperation _operation, CancellationToken _ct = default) where T : View {
            View _view = BeginRequest<T>();
            IProgress<float> _progress = _view as IProgress<float>;

            try {
                while (!_operation.isDone) {
                    _progress?.Report(_operation.progress);
                    await UniTask.Yield(PlayerLoopTiming.Update, _ct);
                }

                _progress?.Report(1f);
            }
            finally {
                HideShown(_view);
            }
        }

        /// <summary> Muestra el overlay T mientras dura la tarea y lo oculta al terminar, aunque falle o se cancele </summary>
        public async UniTask ShowWhile<T>(UniTask _task, CancellationToken _ct = default) where T : View {
            View _view = BeginRequest<T>();

            try {
                await _task.AttachExternalCancellation(_ct);
            }
            finally {
                HideShown(_view);
            }
        }

        /// <summary> Igual que ShowWhile, pero devuelve el resultado de la tarea </summary>
        public async UniTask<TResult> ShowWhile<T, TResult>(UniTask<TResult> _task, CancellationToken _ct = default) where T : View {
            View _view = BeginRequest<T>();

            try {
                return await _task.AttachExternalCancellation(_ct);
            }
            finally {
                HideShown(_view);
            }
        }

        /// <summary> Muestra el overlay T mientras dura el proceso. El proceso recibe un IProgress que llega al overlay </summary>
        public async UniTask ShowWhile<T>(Func<IProgress<float>, CancellationToken, UniTask> _process, CancellationToken _ct = default) where T : View {
            View _view = BeginRequest<T>();
            IProgress<float> _progress = _view as IProgress<float> ?? NoProgress.Instance;

            try {
                await _process(_progress, _ct);
                _progress.Report(1f);
            }
            finally {
                HideShown(_view);
            }
        }

        private View BeginRequest<T>() where T : View {
            View _view = Show<T>();
            if (_view) requests.Add(_view);
            return _view;
        }

        /// <summary>
        /// Termina una petición de ShowWhile. El overlay solo se oculta si ninguna otra petición en curso lo usa;
        /// si una petición anterior usaba otro overlay, se vuelve a mostrar ese.
        /// </summary>
        private void HideShown(View _view) {
            if (!_view) return;

            int _index = requests.LastIndexOf(_view);
            if (_index >= 0) requests.RemoveAt(_index);
            requests.RemoveAll(_request => !_request);

            if (requests.Contains(_view)) return;

            if (requests.Count > 0) Show(requests[^1]);
            else Hide(_view);
        }

        private sealed class NoProgress : IProgress<float> {
            public static readonly NoProgress Instance = new();
            public void Report(float _value) { }
        }
        #endregion
    }
}
