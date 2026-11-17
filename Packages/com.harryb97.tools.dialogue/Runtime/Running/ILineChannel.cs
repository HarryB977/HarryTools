namespace HarryTools.Dialogue {
    /// <summary> Shows the lines of a dialogue </summary>
    public interface ILineChannel {
        /// <summary> Starts showing the line and returns its handle. The run polls the handle until it is finished </summary>
        ILineHandle Present(LinePresentation _line);
    }

    /// <summary> One line being shown by an <see cref="ILineChannel"/> </summary>
    public interface ILineHandle {
        /// <summary> The line is over and the dialogue moves on </summary>
        bool IsFinished { get; }

        /// <summary> Player input: completes the line or ends it, as its advance mode allows </summary>
        void Advance();

        /// <summary> Ends the line at once </summary>
        void Skip();

        /// <summary> Freezes or unfreezes the line and its lifetime </summary>
        void SetPaused(bool _paused);

        /// <summary> The run no longer needs the line. Called once, finished or not </summary>
        void Release();
    }
}
