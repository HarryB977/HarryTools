using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Dialogue {
    public enum LaunchOn {
        [Tooltip("Only when Launch is called")] Manual,
        [Tooltip("In Start")] Start,
        [Tooltip("Every time the component is enabled")] Enable
    }

    /// <summary> Starts a dialogue from a scene object, with that object as its origin </summary>
    [AddComponentMenu("HarryTools/Dialogue/Dialogue Launcher")]
    public sealed class DialogueLauncher : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private DialogueAsset dialogue;
        [SerializeField] private LaunchOn launchOn = LaunchOn.Manual;
        [Tooltip("Main dialogues can offer choices and only one runs at a time"), SerializeField] private bool isMain = true;
        [Tooltip("Actors fixed for this dialogue, ahead of the registered ones"), SerializeField] private List<ActorBinding> actors = new List<ActorBinding>();
        #endregion

        #region Public Variables
        public DialogueAsset Dialogue {
            get => dialogue;
            set => dialogue = value;
        }

        public LaunchOn LaunchOn {
            get => launchOn;
            set => launchOn = value;
        }

        public bool IsMain {
            get => isMain;
            set => isMain = value;
        }

        public List<ActorBinding> Actors => actors;

        /// <summary> The run this launcher started, or null. An ended run stays until the next launch </summary>
        public DialogueRun Run { get; private set; }
        #endregion

        // ==================== UNITY ====================
        #region Unity
        private void Start() {
            if (launchOn == LaunchOn.Start) Launch();
        }

        private void OnEnable() {
            if (launchOn == LaunchOn.Enable) Launch();
        }
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Starts the dialogue. While its own run is still active, that run is returned and nothing starts </summary>
        /// <returns> The run, or null when the dialogue cannot start </returns>
        public DialogueRun Launch() {
            if (Run != null && Run.State != RunState.Ended) return Run;

            DialogueStartOptions _options = new DialogueStartOptions {
                IsMain = isMain,
                Origin = gameObject,
                Actors = new List<ActorBinding>(actors)
            };
            Run = global::HarryTools.Dialogue.Dialogue.Start(dialogue, _options);
            return Run;
        }

        /// <summary> Stops the run this launcher started. Other dialogues are left alone </summary>
        public void Stop() {
            if (Run != null) Run.Stop();
        }
        #endregion
    }
}
