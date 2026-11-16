using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.TextEffects.Editor {
    /// <summary>
    /// Plays an EffectText in edit mode: typing, effects, pauses and hiding, with events listed instead of run and no sound.
    /// The TMP text and mesh are put back on stop, and nothing is recorded in the scene or the prefab
    /// </summary>
    internal sealed class EffectTextPreview {
        // ==================== VARIABLES ===================
        #region Private Variables
        /// <summary> Longest step per editor update, so a stalled editor does not jump the text forward </summary>
        private const float MaxStep = 0.1f;

        private readonly EffectText target;
        private readonly List<string> eventLog = new List<string>();
        private readonly Action<string> onEventReached;

        private TMP_Text tmp;
        private string originalText;
        private string localeCode;
        private double lastUpdateTime;
        #endregion

        #region Public Variables
        public bool IsPlaying { get; private set; }

        /// <summary> Events reached since the last play, in order </summary>
        public IReadOnlyList<string> EventLog => eventLog;
        #endregion

        // ==================== START ====================
        public EffectTextPreview(EffectText _target) {
            target = _target;
            onEventReached = eventLog.Add;
        }

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Starts the preview from the first character. A locale code reads that locale's text when the source is localized </summary>
        public void Play(string _localeCode) {
            if (target == null || !target.isActiveAndEnabled) return;
            if (!IsPlaying) Begin();

            localeCode = _localeCode;
            eventLog.Clear();
            target.SetText(ReadText(_localeCode));
            Repaint();
        }

        public void Skip() {
            if (!CanDrive()) return;
            target.Skip();
            Repaint();
        }

        public void Continue() {
            if (!CanDrive()) return;
            target.Continue();
            Repaint();
        }

        public void Hide() {
            if (!CanDrive()) return;
            target.Hide();
            Repaint();
        }

        /// <summary> Plays again with the current text and the same locale </summary>
        public void Restart() {
            if (!IsPlaying) return;
            Play(localeCode);
        }

        /// <summary> Stops driving the text and puts the TMP text and mesh back as they were </summary>
        public void Stop() {
            if (!IsPlaying) return;
            IsPlaying = false;
            Unsubscribe();

            if (target != null) {
                target.EventReached -= onEventReached;
                // Stop listening first, so the regenerated mesh below is not animated again
                target.Animator.StopListening();
                target.Animator.Restore();
                target.PreviewMode = false;
            }

            if (tmp != null) {
                tmp.text = originalText;
                tmp.ForceMeshUpdate(false, true);
            }
            tmp = null;
            originalText = null;
            Repaint();
        }
        #endregion

        #region Internal Methods
        /// <summary> Advances the preview by '_dt' seconds. The editor update calls it, tests call it directly </summary>
        internal void Step(float _dt) {
            if (!IsPlaying) return;
            if (target == null || tmp == null || !target.isActiveAndEnabled) {
                Stop();
                return;
            }

            target.Tick(_dt);
            Repaint();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void Begin() {
            tmp = target.GetComponent<TMP_Text>();
            // Kept as a plain string and written back directly: no Undo record, so the scene is not marked as modified
            originalText = tmp.text;

            target.PreviewMode = true;
            target.EventReached += onEventReached;
            target.Animator.StartListening();
            IsPlaying = true;

            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }

        private void Unsubscribe() {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            PrefabStage.prefabSaving -= OnPrefabSaving;
        }

        private bool CanDrive() {
            if (!IsPlaying) return false;
            if (target != null && tmp != null) return true;
            Stop();
            return false;
        }

        private string ReadText(string _localeCode) {
#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
            if (target.SourceMode == TextSourceMode.Localized) return LocalizedTextSource.Read(target.LocalizedText, _localeCode) ?? string.Empty;
#endif
            return target.PlainText ?? string.Empty;
        }

        private static void Repaint() {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
        #endregion

        #region Editor Callbacks
        private void OnEditorUpdate() {
            double _now = EditorApplication.timeSinceStartup;
            float _dt = Mathf.Min((float)(_now - lastUpdateTime), MaxStep);
            lastUpdateTime = _now;
            Step(_dt);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange _change) {
            if (_change == PlayModeStateChange.ExitingEditMode) Stop();
        }

        // The preview text must never be written to disk
        private void OnSceneSaving(Scene _scene, string _path) {
            Stop();
        }

        private void OnPrefabSaving(GameObject _prefab) {
            Stop();
        }
        #endregion
    }
}
