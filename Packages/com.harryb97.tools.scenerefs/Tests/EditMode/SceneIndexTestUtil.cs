using System.Collections.Generic;
using System.IO;
using HarryTools.SceneRefs.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>
    /// Temporary scenes and assets under Assets/__SceneRefsTests__ and an in-memory index, so tests never touch the user's scenes or the real index.
    /// </summary>
    internal sealed class SceneIndexTestUtil
    {
        // ==================== VARIABLES ====================

        public const string Folder = "Assets/__SceneRefsTests__";

        private readonly List<Scene> opened = new List<Scene>();

        // ==================== PROPERTIES ====================

        public SceneIdIndex Index { get; private set; }

        // ==================== SETUP ====================

        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__SceneRefsTests__");
            Index = ScriptableObject.CreateInstance<SceneIdIndex>();
            // Keeps the in-memory index alive through the asset refreshes the tests trigger.
            Index.hideFlags = HideFlags.HideAndDontSave;
            SceneIdIndex.Active = Index;
        }

        public void TearDown()
        {
            for (int _i = 0; _i < opened.Count; _i++)
                if (opened[_i].IsValid() && opened[_i].isLoaded) EditorSceneManager.CloseScene(opened[_i], true);
            opened.Clear();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            AssetDatabase.DeleteAsset(Folder);
            SceneIdIndexMaintainer.FlushPending();
            SceneIdIndex.Active = null;
            Object.DestroyImmediate(Index);
        }

        // ==================== SCENES ====================

        /// <summary>
        /// Writes an empty scene file and opens it additively. The test runner keeps an untitled scene loaded,
        /// which forbids EditorSceneManager.NewScene in additive mode.
        /// </summary>
        public Scene NewScene(string _name)
        {
            string _path = ScenePath(_name);
            File.WriteAllText(_path, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport);
            Scene _scene = EditorSceneManager.OpenScene(_path, OpenSceneMode.Additive);
            opened.Add(_scene);
            return _scene;
        }

        /// <summary>The untitled scene the test runner keeps loaded. Objects created in it must be destroyed by the test.</summary>
        public static Scene UntitledScene()
        {
            for (int _i = 0; _i < SceneManager.sceneCount; _i++)
            {
                Scene _scene = SceneManager.GetSceneAt(_i);
                if (string.IsNullOrEmpty(_scene.path)) return _scene;
            }

            return default;
        }

        public static string ScenePath(string _name) => $"{Folder}/{_name}.unity";

        public static void Save(Scene _scene)
        {
            EditorSceneManager.MarkSceneDirty(_scene);
            EditorSceneManager.SaveScene(_scene);
        }

        public void Close(Scene _scene)
        {
            EditorSceneManager.CloseScene(_scene, true);
            opened.Remove(_scene);
        }

        // ==================== OBJECTS ====================

        public static GameObject NewObject(Scene _scene, string _name, GameObject _parent = null)
        {
            // Created while the temporary scene is active so the user's active scene is never touched.
            Scene _previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(_scene);
            GameObject _go = new GameObject(_name);
            SceneManager.SetActiveScene(_previous);
            if (_parent != null) _go.transform.SetParent(_parent.transform, false);
            return _go;
        }

        public static SceneId AddId(GameObject _go, string _guid, string _id)
        {
            SceneId _sceneId = _go.AddComponent<SceneId>();
            _sceneId.Set(_guid, _id);
            return _sceneId;
        }

        // ==================== INDEX ====================

        public List<SceneIdIndexEntry> EntriesOf(string _sceneGuid)
        {
            List<SceneIdIndexEntry> _result = new List<SceneIdIndexEntry>();
            foreach (SceneIdIndexEntry _entry in Index.Entries)
                if (_entry.SceneGuid == _sceneGuid) _result.Add(_entry);
            return _result;
        }
    }
}
