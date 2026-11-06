using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HarryTools.Audio.Subtitles {
    /// <summary> Resolves texts with Unity Localization and reports locale changes. </summary>
    public sealed class LocalizationTextSource : ISubtitleTextSource {
        // ==================== VARIABLES ===================
        private Action languageChanged;
        private bool listening;

        // ==================== EVENTS ===================
        /// <summary> Raised when the selected locale changes. Listens to Localization only while someone is subscribed. </summary>
        public event Action LanguageChanged {
            add {
                languageChanged += value;
                if (listening || languageChanged == null) return;
                listening = true;
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
            remove {
                languageChanged -= value;
                if (!listening || languageChanged != null) return;
                listening = false;
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
        }

        // ==================== PUBLIC ===================
        public void Resolve(LocalizedString _text, Action<string> _onResolved) {
            if (_onResolved == null) return;
            if (_text == null || _text.IsEmpty) {
                _onResolved(string.Empty);
                return;
            }

            AsyncOperationHandle<string> _operation = _text.GetLocalizedStringAsync();
            if (_operation.IsDone) {
                _onResolved(ReadResult(_operation));
                return;
            }
            _operation.Completed += _done => _onResolved(ReadResult(_done));
        }

        // ==================== PRIVATE ===================
        private static string ReadResult(AsyncOperationHandle<string> _operation) {
            return _operation.Status == AsyncOperationStatus.Succeeded && _operation.Result != null ? _operation.Result : string.Empty;
        }

        private void OnLocaleChanged(Locale _locale) => languageChanged?.Invoke();
    }
}
