namespace HarryTools.Audio.Subtitles {
    /// <summary> Receives what a SubtitleTimeline decides to show and hide. </summary>
    internal interface ISubtitleTimelineListener {
        /// <param name="_entry"> Index of the entry in the list passed to SubtitleTimeline.Reset. </param>
        /// <param name="_remaining"> Seconds the entry stays visible, or -1 when it lasts until a looping sound stops. </param>
        void OnShow(int _entry, float _remaining);

        /// <param name="_entry"> Index of the entry in the list passed to SubtitleTimeline.Reset. </param>
        void OnHide(int _entry);
    }
}
