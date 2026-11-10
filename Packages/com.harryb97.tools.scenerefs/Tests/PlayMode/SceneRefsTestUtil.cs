using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    internal static class SceneRefsTestUtil
    {
        // ==================== METHODS ====================

        public static Scene NewScene(string _name, List<Scene> _created)
        {
            Scene _scene = SceneManager.CreateScene(_name);
            _created.Add(_scene);
            return _scene;
        }

        public static GameObject NewObject(Scene _scene, string _name, string _guid, string _id, GameObject _parent = null, bool _active = true)
        {
            GameObject _go = new GameObject(_name);
            SceneManager.MoveGameObjectToScene(_go, _scene);
            if (_parent != null) _go.transform.SetParent(_parent.transform, false);
            _go.AddComponent<SceneId>().Set(_guid, _id);
            _go.SetActive(_active);
            return _go;
        }

        public static void UnloadAll(List<Scene> _created)
        {
            foreach (Scene _scene in _created)
                if (_scene.isLoaded) SceneManager.UnloadSceneAsync(_scene);
            _created.Clear();
            SceneRefs.Clear();
        }
    }
}
