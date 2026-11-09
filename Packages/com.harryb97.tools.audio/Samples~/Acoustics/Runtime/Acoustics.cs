using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Starts, switches and stops the acoustic simulation. </summary>
    public static class Acoustics {
        // ==================== VARIABLES ===================
        private static PropagationModule module;
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

        // ==================== PUBLIC API ===================
        /// <summary> Runs the simulation with the profile, or its override for the current platform. Null stops it. </summary>
        public static void Use(AcousticProfile _profile) {
            if (_profile == null) {
                Stop();
                return;
            }

            AcousticProfile _resolved = _profile.Resolve(Application.platform);
            if (module == null) module = new PropagationModule(_resolved, new PhysicsGeometry3D());
            else module.Configure(_resolved);
            module.CaptureDebug = debugCapture;

            // Also recovers a module the core unregistered after an exception.
            Audio.RegisterModule(module);
            Active = _profile;
        }

        /// <summary> Unregisters the module and frees its buffers. Sounds keep playing and go back to neutral. </summary>
        public static void Stop() {
            Active = null;
            if (module == null) return;
            Audio.UnregisterModule(module);
            module.Dispose();
            module = null;
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
