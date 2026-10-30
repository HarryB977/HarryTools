using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;

namespace HarryTools.Disposable.Editor {
    [InitializeOnLoad]
    public static class StaticDisposer {
        // ==================== START ====================
        static StaticDisposer() {
            EditorApplication.playModeStateChanged += Event_OnPlayModeStateChanged;
        }

        // ==================== EVENTS ====================
        private static void Event_OnPlayModeStateChanged(PlayModeStateChange _state) {
            if (_state == PlayModeStateChange.EnteredPlayMode) return;
            IEnumerable<MethodInfo> _methodsToInvoke = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => method.IsDefined(typeof(StaticDisposeAttribute), false));

            foreach (MethodInfo _method in _methodsToInvoke) {
                if (_method.GetParameters().Length != 0) continue;
                _method.Invoke(null, null);
            }
        }
    }
}