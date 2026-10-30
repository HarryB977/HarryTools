using System;
using System.Linq;
using UnityEngine;

using Cysharp.Threading.Tasks;
using HarryUtils.Singleton;

using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.MultiScene {
    public class SceneLoader : Singleton<SceneLoader> {
        public class LoadingProgress : IProgress<float> {
            public event Action<float> Progressed;
            private const float ratio = 1f;
            
            public void Report(float _value) => Progressed?.Invoke(_value / ratio);
        }

        public class Properties {
            public bool reloadDupScenes = false;
            public float delayBeforeLoading = 0.35f;
            public float delayAfterLoading = 0.35f;
        }
        
        // ==================== VARIABLES ===================
        #region Public Variables
        public readonly SceneGroupHelper sceneGroupManager = new();
        #endregion
        
        #region Private Variables
        protected float TargetProgress { get; private set; }

        [Header("Scene Loader")]
        [SerializeField] private SceneGroupsConfig sceneGroupsConfig;
        #endregion
        
        // ==================== METHODS ====================
        protected virtual void BeforeLoadSceneGroup() {}
        protected virtual void AfterLoadSceneGroup() {}

        // =================================================

        public async void LoadSceneGroup(int _index, Properties _properties = default) {
            await LoadSceneGroupAsync(_index, _properties);
        }

        public async void LoadSceneGroup(string _groupName, Properties _properties = default) {
            await LoadSceneGroupAsync(_groupName, _properties);
        }
        
        // =================================================

        public async UniTask LoadSceneGroupAsync(int _index, Properties _properties = null) {
            _properties ??= new();
            TargetProgress = 1f;
            
            if (_index < 0 || _index >= sceneGroupsConfig.sceneGroups.Length) {
                Logger.LogError("SceneLoader", $"Índice de grupo de escenas invalido: {_index}");
                return;
            }

            LoadingProgress _progress = new();
            _progress.Progressed += _target => TargetProgress = Mathf.Max(_target, TargetProgress);

            BeforeLoadSceneGroup();
            await UniTask.Delay(TimeSpan.FromSeconds(_properties.delayBeforeLoading));
            await sceneGroupManager.LoadScenes(sceneGroupsConfig.sceneGroups[_index], _progress, _properties.reloadDupScenes);
            await UniTask.Delay(TimeSpan.FromSeconds(_properties.delayAfterLoading));
            AfterLoadSceneGroup();
        }

        public async UniTask LoadSceneGroupAsync(string _groupName, Properties _properties = null) {
            _properties ??= new();
            TargetProgress = 1f;
            
            if (!sceneGroupsConfig.sceneGroups.Any(sg => sg.GroupName == _groupName)) {
                Logger.LogError("SceneLoader", $"No existe el grupo de escenas: {_groupName}");
                return;
            }

            LoadingProgress _progress = new();
            _progress.Progressed += _target => TargetProgress = Mathf.Max(_target, TargetProgress);

            BeforeLoadSceneGroup();
            await UniTask.Delay(TimeSpan.FromSeconds(_properties.delayBeforeLoading));
            await sceneGroupManager.LoadScenes(sceneGroupsConfig.sceneGroups.First(sg => sg.GroupName == _groupName), _progress, _properties.reloadDupScenes);
            await UniTask.Delay(TimeSpan.FromSeconds(_properties.delayAfterLoading));
            AfterLoadSceneGroup();
        }
    }
}