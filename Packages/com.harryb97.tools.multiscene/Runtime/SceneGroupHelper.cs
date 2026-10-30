using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

using Cysharp.Threading.Tasks;

using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.MultiScene {
    public class SceneGroupHelper {
        public readonly struct AsyncOperationGroup {
            public float Progress => Operations.Count == 0 ? 0f : Operations.Average(o => o.progress);
            public bool IsDone => Operations.All(o => o.isDone);

            public readonly List<AsyncOperation> Operations;

            public AsyncOperationGroup(int _initialCapacity) {
                Operations = new(_initialCapacity);
            }
        }

        public readonly struct AsyncOperationHandleGroup {
            public float Progress => Handles.Count == 0 ? 0f : Handles.Average(h => h.PercentComplete);
            public bool IsDone => Handles.Count == 0 || Handles.All(h => h.IsDone);

            public readonly List<AsyncOperationHandle<SceneInstance>> Handles;

            public AsyncOperationHandleGroup(int _initialCapacity) {
                Handles = new(_initialCapacity);
            }
        }

        // ==================== VARIABLES ===================
        #region Public Variables
        public event Action<string> OnSceneLoaded = delegate { };
        public event Action<string> OnSceneUnloaded = delegate { };
        public event Action OnSceneGroupLoaded = delegate { };
        #endregion

        #region Private Variables
        private readonly AsyncOperationHandleGroup handleGroup = new(10);
        private SceneGroup activeSceneGroup;
        #endregion

        // ==================== METHODS ====================
        public async UniTask LoadScenes(SceneGroup _sceneGroup, IProgress<float> _progress, bool _reloadDupScenes = false) {
            // Descargamos el grupo activo actual
            await UnloadScenes();
            activeSceneGroup = _sceneGroup;

            List<string> _loadedScenes = new();
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                _loadedScenes.Add(SceneManager.GetSceneAt(i).name);
            }

            AsyncOperationGroup _operationGroup = new(activeSceneGroup.Scenes.Count);
            for (int i = 0; i < activeSceneGroup.Scenes.Count; i++) {
                SceneGroup.SceneData _sceneData = _sceneGroup.Scenes[i];
                if (!_reloadDupScenes && _loadedScenes.Contains(_sceneData.reference.Name)) continue;

                // Comprobamos tipo de escena
                if (_sceneData.reference.State == SceneReferenceState.Regular) {
                    AsyncOperation _operation = SceneManager.LoadSceneAsync(_sceneData.reference.Name, LoadSceneMode.Additive);
                    _operationGroup.Operations.Add(_operation);

                } else if (_sceneData.reference.State == SceneReferenceState.Addressable) {
                    AsyncOperationHandle<SceneInstance> _sceneHandle = Addressables.LoadSceneAsync(_sceneData.reference.Address, LoadSceneMode.Additive);
                    handleGroup.Handles.Add(_sceneHandle);

                } else {
                    Logger.LogWarning("SceneGroupHelper", $"La escena '{_sceneData.reference.Name}' no está en la build ni es un Addressable");
                    continue;
                }

                OnSceneLoaded?.Invoke(_sceneData.reference.Name);

                for (int j = 0; j < _sceneGroup.SceneActions.Length; j++) {
                    _sceneGroup.SceneActions[j]?.OnSceneLoaded(_sceneData.reference.Name);
                }
            }

            while (!_operationGroup.IsDone || !handleGroup.IsDone) {
                _progress?.Report((_operationGroup.Progress + handleGroup.Progress) / 2);
                await UniTask.Delay(100);
            }

            Scene _activeScene = SceneManager.GetSceneByName(activeSceneGroup.ActiveScene.Name);
            if (_activeScene.IsValid()) SceneManager.SetActiveScene(_activeScene);

            // After Load
            OnSceneGroupLoaded?.Invoke();
            for (int i = 0; i < _sceneGroup.SceneActions.Length; i++) {
                _sceneGroup.SceneActions[i]?.OnSceneGroupLoaded();
            }
        }

        public async UniTask UnloadScenes() {
            if (activeSceneGroup == null) return;
            List<string> _scenesToUnload = new();

            for (int i = 0; i < activeSceneGroup.Scenes.Count; i++) {
                if (activeSceneGroup.Scenes[i].type == SceneGroup.SceneType.Tool) continue;
                if (handleGroup.Handles.Any(h => h.IsValid() && h.Result.Scene.name == activeSceneGroup.Scenes[i].reference.Name)) continue;
                
                _scenesToUnload.Add(activeSceneGroup.Scenes[i].reference.Name);
            }

            // Creamos un grupo de operaciones para descargar las escenas
            AsyncOperationGroup _operationGroup = new(_scenesToUnload.Count);
            for (int i = 0; i < _scenesToUnload.Count; i++) {
                AsyncOperation _operation = SceneManager.UnloadSceneAsync(_scenesToUnload[i]);
                if (_operation == null) continue;

                _operationGroup.Operations.Add(_operation);
                OnSceneUnloaded?.Invoke(_scenesToUnload[i]);
            }

            // Descargamos las escenas que sean Addressables
            for (int i = 0; i < handleGroup.Handles.Count; i++) {
                if (handleGroup.Handles[i].IsValid()) {
                    await Addressables.UnloadSceneAsync(handleGroup.Handles[i]);
                }
            }

            handleGroup.Handles.Clear();

            while (!_operationGroup.IsDone) {
                await UniTask.Delay(100);
            }

            await Resources.UnloadUnusedAssets();
        }
    }
}