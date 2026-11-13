namespace HarryTools.Flags.Editor
{
    internal enum FlagIssueKind
    {
        DuplicateGuid,
        DuplicateName,
        BrokenRef,
        ForeignLocalRef,
        MissingScene
    }

    /// <summary>One problem found by <see cref="FlagValidator"/>. Only duplicate guids and names block Play and builds.</summary>
    internal readonly struct FlagIssue
    {
        // ==================== VARIABLES ====================

        public readonly FlagIssueKind Kind;
        public readonly bool Blocking;
        public readonly string Message;

        /// <summary>Guid of the flag involved.</summary>
        public readonly string FlagGuid;

        /// <summary>The reference at fault, for BrokenRef and ForeignLocalRef. Null otherwise.</summary>
        public readonly FlagUsageEntry Usage;

        // ==================== CONSTRUCTOR ====================

        public FlagIssue(FlagIssueKind _kind, bool _blocking, string _message, string _flagGuid, FlagUsageEntry _usage = null)
        {
            Kind = _kind;
            Blocking = _blocking;
            Message = _message;
            FlagGuid = _flagGuid;
            Usage = _usage;
        }
    }
}
