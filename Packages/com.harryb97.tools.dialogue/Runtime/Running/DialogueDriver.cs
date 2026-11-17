using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Hidden object that ticks every running dialogue once per frame </summary>
    [AddComponentMenu("")]
    internal sealed class DialogueDriver : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [ClearOnReload] private static DialogueDriver instance;
        #endregion

        // ==================== METHODS ====================
        #region Internal
        /// <summary> Creates the driver the first time it is needed, only while playing and when dialogues are not ticked by hand </summary>
        internal static void Ensure() {
            if (instance != null || Dialogue.ManualTick || !Application.isPlaying) return;
            GameObject _object = new GameObject("[HarryTools] Dialogue") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(_object);
            instance = _object.AddComponent<DialogueDriver>();
        }

        internal static void DestroyInstance() {
            if (instance == null) return;
            GameObject _object = instance.gameObject;
            instance = null;
            if (Application.isPlaying) Destroy(_object);
            else DestroyImmediate(_object);
        }
        #endregion

        // ==================== UNITY ====================
        #region Unity
        private void Update() {
            if (!Dialogue.ManualTick) Dialogue.TickAll(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnDestroy() {
            if (instance == this) instance = null;
        }
        #endregion
    }
}
