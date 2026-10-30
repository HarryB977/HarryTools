namespace HarryTools.GameLoop {
    public class Loop : ILoopable {
        // ==================== VARIABLES ===================
        #region Public Variables
        public string LoopID { get; }
        public bool IsPaused { get; set; }

        public LoopSystem.LoopDelegate Update { get; private set; }
        public LoopSystem.LoopDelegate FixedUpdate { get; private set; }
        public LoopSystem.LoopDelegate LateUpdate { get; private set; }
        #endregion

        // ==================== START ====================
        public Loop(string _loopId) {
            LoopID = _loopId;
        }

        // ==================== METHODS ====================
        void ILoopable.AddUpdateListener(LoopSystem.LoopDelegate _listener) {
            Update += _listener;
        }

        void ILoopable.AddFixedUpdateListener(LoopSystem.LoopDelegate _listener) {
            FixedUpdate += _listener;
        }

        void ILoopable.AddLateUpdateListener(LoopSystem.LoopDelegate _listener) {
            LateUpdate += _listener;
        }

        // =================================================

        void ILoopable.RemoveUpdateListener(LoopSystem.LoopDelegate _listener) {
            Update -= _listener;
        }

        void ILoopable.RemoveFixedUpdateListener(LoopSystem.LoopDelegate _listener) {
            FixedUpdate -= _listener;
        }

        void ILoopable.RemoveLateUpdateListener(LoopSystem.LoopDelegate _listener) {
            LateUpdate -= _listener;
        }
    }
}