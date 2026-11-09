using System;
using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Starts, switches and stops the acoustic simulation. Owns the geometry and the ray budget that the modules share. </summary>
    public static class Acoustics {
        // ==================== VARIABLES ===================
        private static PropagationModule module;
        private static RoomModule room;
        private static IAcousticGeometry geometry;
        private static RayBudget budget;
        private static bool debugCapture;

        // ==================== PROPERTIES ===================
        /// <summary> The profile given to <see cref="Use"/>, before resolving platform overrides. Null when stopped. </summary>
        public static AcousticProfile Active { get; private set; }

        /// <summary> Keeps what each voice traced so the scene view gizmos can draw it. Costs a few copies per voice, so leave it off in builds. </summary>
        public static bool DebugCapture {
            get => debugCapture;
            set {
                debugCapture = value;
                if (module != null) module.CaptureDebug = value;
            }
        }

        internal static PropagationModule Module => module;
        internal static RoomModule Room => room;

        // ==================== PUBLIC API ===================
        /// <summary> Runs the simulation with the profile, or its override for the current platform. Null stops it. </summary>
        public static void Use(AcousticProfile _profile) {
            if (_profile == null) {
                Stop();
                return;
            }

            Start(_profile, geometry ?? new PhysicsGeometry3D());
        }

        /// <summary> Runs the simulation on the given geometry, replacing any running modules. Takes ownership of it. Lets tests inject a fake. </summary>
        internal static void UseWith(AcousticProfile _profile, IAcousticGeometry _geometry) {
            Stop();
            Start(_profile, _geometry);
        }

        /// <summary> Unregisters the modules and frees their buffers. Sounds keep playing and go back to neutral. </summary>
        public static void Stop() {
            Active = null;

            PropagationModule _module = module;
            RoomModule _room = room;
            IAcousticGeometry _geometry = geometry;
            module = null;
            room = null;
            geometry = null;
            budget = null;

            // The core only unregisters a module that throws by itself, so the other one has to go too.
            if (_module != null) Audio.UnregisterModule(_module);
            if (_room != null) Audio.UnregisterModule(_room);

            // Modules first: each one still has to release its batch in flight.
            try {
                _module?.Dispose();
            } finally {
                try {
                    _room?.Dispose();
                } finally {
                    _geometry?.Dispose();
                }
            }
        }

        // ==================== HELPERS ===================
        /// <summary> Called by a module whose Evaluate threw, just before the core unregisters it. A running module stops the whole simulation; any other only frees itself. </summary>
        internal static void OnModuleFailed(IAcousticModule _failed) {
            if (ReferenceEquals(_failed, module) || ReferenceEquals(_failed, room)) Stop();
            else (_failed as IDisposable)?.Dispose();
        }

        private static void Start(AcousticProfile _profile, IAcousticGeometry _geometry) {
            AcousticProfile _resolved = _profile.Resolve(Application.platform);

            geometry = _geometry;
            geometry.Configure(_resolved);
            if (budget == null) budget = new RayBudget(_resolved.RayBudgetPerFrame);
            else budget.SetLimit(_resolved.RayBudgetPerFrame);

            if (module == null) module = new PropagationModule(_resolved, geometry, budget);
            else module.Configure(_resolved);
            module.CaptureDebug = debugCapture;
            Audio.RegisterModule(module);

            if (_resolved.Reverb) {
                if (room == null) room = new RoomModule(_resolved, geometry, budget);
                else room.Configure(_resolved);
                Audio.RegisterModule(room);
            } else if (room != null) {
                Audio.UnregisterModule(room);
                room.Dispose();
                room = null;
            }

            Active = _profile;
        }

        // ==================== LIFECYCLE ===================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() {
            Application.quitting -= Stop;
            Application.quitting += Stop;
        }

        [ExecuteOnReload]
        private static void ResetOnReload() {
            debugCapture = false;
            Stop();
        }

#if UNITY_EDITOR
        // A recompile during Play skips both hooks above and would leak the native buffers.
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorHooks() {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }
#endif
    }
}
