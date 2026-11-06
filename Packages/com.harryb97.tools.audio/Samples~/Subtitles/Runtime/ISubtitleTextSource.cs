using System;
using UnityEngine.Localization;

namespace HarryTools.Audio.Subtitles {
    /// <summary> Turns localized references into text. The default one uses Unity Localization. </summary>
    public interface ISubtitleTextSource {
        /// <summary> Calls back with the text, at once or later. An empty reference resolves to an empty string. </summary>
        void Resolve(LocalizedString _text, Action<string> _onResolved);

        /// <summary> Raised when the language changes and the texts must be resolved again. </summary>
        event Action LanguageChanged;
    }
}
