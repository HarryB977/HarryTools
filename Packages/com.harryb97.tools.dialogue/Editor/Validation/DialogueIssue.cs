namespace HarryTools.Dialogue.Editor {
    internal enum DialogueIssueSeverity {
        Info,
        Warning
    }

    /// <summary> A problem found in a dialogue. An empty node id means the problem belongs to the dialogue itself </summary>
    internal readonly struct DialogueIssue {
        public DialogueIssueSeverity Severity { get; }
        public string NodeId { get; }
        public string Message { get; }

        public DialogueIssue(DialogueIssueSeverity _severity, string _nodeId, string _message) {
            Severity = _severity;
            NodeId = _nodeId ?? string.Empty;
            Message = _message;
        }
    }
}
