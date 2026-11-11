using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.Flags.Tests
{
    public class FlagHostScopeTests
    {
        // ==================== VARIABLES ====================

        private const string Folder = "Assets/__FlagsTests__";
        private const string SceneA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string SceneB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private const string SceneYaml =
            "%YAML 1.1\n" +
            "%TAG !u! tag:unity3d.com,2011:\n" +
            "--- !u!1 &100\n" +
            "GameObject:\n" +
            "  m_ObjectHideFlags: 0\n" +
            "  serializedVersion: 6\n" +
            "  m_Component:\n" +
            "  - component: {fileID: 200}\n" +
            "  m_Layer: 0\n" +
            "  m_Name: Host\n" +
            "  m_IsActive: 1\n" +
            "--- !u!4 &200\n" +
            "Transform:\n" +
            "  m_GameObject: {fileID: 100}\n" +
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n" +
            "  m_LocalPosition: {x: 0, y: 0, z: 0}\n" +
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n" +
            "  m_Children: []\n" +
            "  m_Father: {fileID: 0}\n";

        private FlagDatabase database;
        private Scene previewScene;
        private readonly List<Scene> scenes = new List<Scene>();
        private readonly List<Object> created = new List<Object>();

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FlagsTests__");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Scene _scene in scenes)
                if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
            scenes.Clear();
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            previewScene = default;
            foreach (Object _object in created)
                if (_object != null) Object.DestroyImmediate(_object);
            created.Clear();
            Object.DestroyImmediate(database);
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        // ==================== SCENE GUID ====================

        [Test]
        public void SceneComponent_ReturnsItsSceneGuid()
        {
            // Written as a file and opened additively: NewScene refuses to run while the Editor has an unsaved untitled scene.
            string _path = Folder + "/HostScene.unity";
            File.WriteAllText(_path, SceneYaml);
            AssetDatabase.ImportAsset(_path);
            Scene _scene = EditorSceneManager.OpenScene(_path, OpenSceneMode.Additive);
            scenes.Add(_scene);
            GameObject _go = _scene.GetRootGameObjects().Single(_root => _root.name == "Host");

            string _expected = AssetDatabase.AssetPathToGUID(_path);
            Assert.IsNotEmpty(_expected);
            Assert.AreEqual(_expected, FlagHostScope.SceneGuidOf(_go.transform));
            Assert.AreEqual(_expected, FlagHostScope.SceneGuidOf(_go));
        }

        [Test]
        public void UnsavedScene_ReturnsNull()
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            GameObject _go = new GameObject("Host");
            SceneManager.MoveGameObjectToScene(_go, previewScene);

            Assert.IsNull(FlagHostScope.SceneGuidOf(_go.transform));
        }

        [Test]
        public void ScriptableObject_ReturnsNull()
        {
            Assert.IsNull(FlagHostScope.SceneGuidOf(database));
            Assert.IsNull(FlagHostScope.SceneGuidOf(null));
        }

        [Test]
        public void PrefabAsset_ReturnsNull()
        {
            GameObject _temp = new GameObject("Prefab");
            created.Add(_temp);
            GameObject _asset = PrefabUtility.SaveAsPrefabAsset(_temp, Folder + "/Host.prefab");

            Assert.IsNull(FlagHostScope.SceneGuidOf(_asset));
            Assert.IsNull(FlagHostScope.SceneGuidOf(_asset.transform));
        }

        // ==================== SELECTABLE / STATE ====================

        [Test]
        public void Selectable_GlobalsPlusOwnSceneLocals_Only()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "G", out FlagDefinition _g, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "LocalA", out FlagDefinition _a, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneB, "LocalB", out _, out _);

            CollectionAssert.AreEquivalent(new[] { _g, _a }, FlagHostScope.Selectable(database, SceneA).ToList());
            CollectionAssert.AreEquivalent(new[] { _g }, FlagHostScope.Selectable(database, null).ToList());
        }

        [Test]
        public void StateOf_EmptyGuid_IsEmpty()
        {
            Assert.AreEqual(FlagRefState.Empty, FlagHostScope.StateOf(database, "", SceneA));
            Assert.AreEqual(FlagRefState.Empty, FlagHostScope.StateOf(database, null, SceneA));
        }

        [Test]
        public void StateOf_GlobalAndOwnLocal_AreValid()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "G", out FlagDefinition _g, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "L", out FlagDefinition _l, out _);

            Assert.AreEqual(FlagRefState.Valid, FlagHostScope.StateOf(database, _g.Guid, SceneA));
            Assert.AreEqual(FlagRefState.Valid, FlagHostScope.StateOf(database, _g.Guid, null));
            Assert.AreEqual(FlagRefState.Valid, FlagHostScope.StateOf(database, _l.Guid, SceneA));
        }

        [Test]
        public void StateOf_LocalOfOtherScene_IsForeignLocal()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneB, "L", out FlagDefinition _l, out _);

            Assert.AreEqual(FlagRefState.ForeignLocal, FlagHostScope.StateOf(database, _l.Guid, SceneA));
        }

        [Test]
        public void StateOf_LocalFromAsset_IsForeignLocal()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "L", out FlagDefinition _l, out _);

            Assert.AreEqual(FlagRefState.ForeignLocal, FlagHostScope.StateOf(database, _l.Guid, null));
        }

        [Test]
        public void StateOf_DeletedGuid_IsDeleted()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "G", out FlagDefinition _g, out _);
            string _guid = _g.Guid;
            FlagDatabaseEditing.Delete(database, _guid);

            Assert.AreEqual(FlagRefState.Deleted, FlagHostScope.StateOf(database, _guid, SceneA));
            Assert.AreEqual(FlagRefState.Deleted, FlagHostScope.StateOf(null, _guid, SceneA));
        }
    }
}
