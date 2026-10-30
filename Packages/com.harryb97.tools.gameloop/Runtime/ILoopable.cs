namespace HarryTools.GameLoop {
    public interface ILoopable {
        // ==================== VARIABLES ===================
        string LoopID { get; }
        bool IsPaused { get; set; }

        LoopSystem.LoopDelegate Update { get; }
        LoopSystem.LoopDelegate FixedUpdate { get; }
        LoopSystem.LoopDelegate LateUpdate { get; }

        // ==================== METHODS ====================
        void AddUpdateListener(LoopSystem.LoopDelegate _listener);
        void AddFixedUpdateListener(LoopSystem.LoopDelegate _listener);
        void AddLateUpdateListener(LoopSystem.LoopDelegate _listener);

        void RemoveUpdateListener(LoopSystem.LoopDelegate _listener);
        void RemoveFixedUpdateListener(LoopSystem.LoopDelegate _listener);
        void RemoveLateUpdateListener(LoopSystem.LoopDelegate _listener);
    }
}