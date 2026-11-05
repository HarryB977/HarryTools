using UnityEditor;
using UnityEngine;

namespace HarryTools.Audio.Editor {
    /// <summary> Default inspector of audio assets plus play and stop buttons. </summary>
    [CustomEditor(typeof(AudioAsset), true)]
    internal sealed class AudioAssetEditor : UnityEditor.Editor {
        // ==================== EDITOR ===================
        public override void OnInspectorGUI() {
            var _asset = (AudioAsset)target;
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("▶ Play", GUILayout.Height(24f))) AudioPreviewer.Play(_asset);
                using (new EditorGUI.DisabledScope(!AudioPreviewer.IsPlaying)) {
                    if (GUILayout.Button("■ Stop", GUILayout.Height(24f))) AudioPreviewer.Stop();
                }
            }
            DrawDefaultInspector();
        }

        private void OnDisable() {
            if (AudioPreviewer.Current == target) AudioPreviewer.Stop();
        }
    }
}
