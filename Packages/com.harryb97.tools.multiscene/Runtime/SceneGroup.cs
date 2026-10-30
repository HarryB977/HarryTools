using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using Eflatun.SceneReference;
using HarryUtils;

namespace HarryTools.MultiScene {
    [Serializable]
    public class SceneGroup {
        [Serializable]
        public class SceneData {
            public SceneReference reference;
            public SceneType type;
        }

        public enum SceneType {
            Default, Tool
        }

        public interface ISceneAction {
            void OnSceneLoaded(string _sceneName);
            void OnSceneGroupLoaded();
        }
        
        // ==================== VARIABLES ===================
        #region Properties
        public string GroupName => groupName;

        public SceneReference ActiveScene => activeScene;
        public List<SceneData> Scenes => scenes.IsNullOrEmpty() ? new List<SceneData>() : scenes;
        public ISceneAction[] SceneActions => sceneActions.IsNullOrEmpty() ? new ISceneAction[0] : sceneActions;
        #endregion

        #region Private Variables
        [SerializeField] private string groupName;
        [SerializeField] private SceneReference activeScene;
        [Space]
        [SerializeField] private List<SceneData> scenes;
        [SerializeReference, SubclassSelector] private ISceneAction[] sceneActions;
        #endregion
        
        // ==================== METHODS ====================
        public bool TryGetSceneData(string _sceneName, out SceneData _data) {
            _data = scenes.FirstOrDefault(s => s.reference.Name == _sceneName);
            return _data != null;
        }
    }
}