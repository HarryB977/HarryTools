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
    /// The TMP text and mesh are put back on stop, and nothing is recorded in the scene or the prefab.
    /// Only one preview drives an EffectText: playing a second one stops the first
    /// </summary>
    internal sealed class EffectTextPreview {
        // ==================== VARIABLES ===================
        #region Private Variables
        /// <summary> Longest step per editor update, so a stalled editor does not jump the text forward </summary>
        private const float MaxStep = 0.1f;

        /// <summary> The preview driving each EffectText. Every preview stops before entering play mode and on assembly reload, so it never outlives them </summary>
        private static readonly Dictionary<EffectText, EffectTextPreview> active = new Dictionary<EffectText, EffectTextPreview>();

        private readonly EffectText target;
        private readonly List<string> eventLog = new List<string>();
        private readonly Action<string> onEventReached;

        private TMP_Text tmp;
        private string originalText;
        // Text the preview itself last wrote into the TMP. Any other value means someone else changed it
        private string writtenText;
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
            writtenText = tmp.text;
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

        /// <summary>
        /// Stops driving the text and puts the TMP text and mesh back as they were. A TMP text changed by someone else
        /// during the preview is kept as it is
        /// </summary>
        public void Stop() {
            if (!IsPlaying) return;
            IsPlaying = false;
            Unsubscribe();
            // Compared by reference so a destroyed target is unregistered too
            if (!ReferenceEquals(target, null) && active.TryGetValue(target, out EffectTextPreview _driver) && _driver == this) active.Remove(target);

            if (target != null) {
                target.EventReached -= onEventReached;
                // Stop listening first, so the regenerated mesh below is not animated again
                target.Animator.StopListening();
                target.Animator.Restore();
                target.PreviewMode = false;
                target.ResetAfterPreview();
            }

            if (tmp != null) {
                if (tmp.text == writtenText) tmp.text = originalText;
                tmp.ForceMeshUpdate(false, true);
            }
            tmp = null;
            originalText = null;
            writtenText = null;
            Repaint();
        }
        #endregion

        #region Internal Methods
        /// <summary> Advances the preview by '_dt' seconds. The editor update calls it, tests call it directly </summary>
        internal void Step(float _dt) {
            if (!IsPlaying) return;
            // A text changed from outside, by the user or a script, ends the preview and is kept
            if (target == null || tmp == null || !target.isActiveAndEnabled || tmp.text != writtenText) {
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
            if (active.TryGetValue(target, out EffectTextPreview _other) && _other != this) _other.Stop();
            active[target] = this;

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
            Undo.undoRedoPerformed += Stop;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }

        private void Unsubscribe() {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            Undo.undoRedoPerformed -= Stop;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            PrefabStage.prefabSaving -= OnPrefabSaving;
        }

        private bool CanDrive() {
            if (!IsPlaying) return false;
            if (target != null && tmp != null && tmp.text == writtenText) return true;
            Stop();
            return false;
        }

        /// <summary> Same source as the runtime: a localized source without the Localization package has no text </summary>
        private string ReadText(string _localeCode) {
            if (target.SourceMode == TextSourceMode.Localized) {
#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
                return LocalizedTextSource.Read(target.LocalizedText, _localeCode) ?? string.Empty;
#else
                return string.Empty;
#endif
            }
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
