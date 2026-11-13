using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarryTools.Actions;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.Flags.Tests
{
    public class FlagUsageMaintainerTests
    {
        // ==================== VARIABLES ====================

        private FlagDatabase database;
        private FlagUsageIndex index;
        private readonly List<Scene> scenes = new List<Scene>();

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            index = ScriptableObject.CreateInstance<FlagUsageIndex>();
            FlagDatabaseLocator.OverrideForTests = database;
            FlagUsageIndexLocator.OverrideForTests = index;
            FlagUsageTestScenes.EnsureFolder();
        }

        [TearDown]
        public void TearDown()
        {
            FlagUsageTestScenes.CloseAll(scenes);
            AssetDatabase.DeleteAsset(FlagUsageTestScenes.Folder);
            AssetDatabase.Refresh();
            // Queued work of the temporary assets is done while the test instances are still in place.
            FlagUsageMaintainer.FlushPending();

            FlagUsageIndexLocator.OverrideForTests = null;
            FlagDatabaseLocator.OverrideForTests = null;
            Object.DestroyImmediate(index);
            Object.DestroyImmediate(database);
        }

        // ==================== HELPERS ====================

        private FlagDefinition Create(FlagScope _scope, string _sceneGuid, string _name)
        {
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, _scope, _sceneGuid, _name, out FlagDefinition _created, out string _error), _error);
            return _created;
        }

        private static FlagRef Ref(FlagDefinition _flag) => new FlagRef(_flag.Guid, _flag.DisplayPath);

        /// <summary>A saved temporary scene whose Host runs the actions; it stays open.</summary>
        private Scene SavedScene(string _name, ActionList _actions)
        {
            Scene _scene = FlagUsageTestScenes.Open(FlagUsageTestScenes.Write(_name), scenes);
            FlagUsageTestScenes.AddRunner(FlagUsageTestScenes.Host(_scene), _actions);
            Assert.IsTrue(EditorSceneManager.SaveScene(_scene));
            return _scene;
        }

        private void Close(Scene _scene)
        {
            EditorSceneManager.CloseScene(_scene, true);
            scenes.Remove(_scene);
        }

        // ==================== TESTS ====================

        [Test]
        public void Save_ReplacesSceneEntries()
        {
            FlagDefinition _a = Create(FlagScope.Global, null, "A");
            FlagDefinition _b = Create(FlagScope.Global, null, "B");
            Scene _scene = SavedScene("Saved", new ActionList(new SetFlagAction(Ref(_a))));
            string _sceneGuid = AssetDatabase.AssetPathToGUID(_scene.path);

            FlagUsageEntry _first = index.For(_a.Guid).Single();
            Assert.AreEqual(_sceneGuid, _first.sceneGuid);
            Assert.AreEqual(FlagUsageKind.Write, _first.kind);
            Assert.IsFalse(_first.approximate);

            FlagUsageTestScenes.AddRunner(FlagUsageTestScenes.Host(_scene), new ActionList(new ClearFlagAction(Ref(_b))));
            Object.DestroyImmediate(FlagUsageTestScenes.Host(_scene).GetComponents<ActionRunner>()[0]);
            Assert.IsTrue(EditorSceneManager.SaveScene(_scene));
            FlagUsageMaintainer.FlushPending();

            Assert.IsEmpty(index.For(_a.Guid));
            Assert.AreEqual(_sceneGuid, index.For(_b.Guid).Single().sceneGuid);
            Assert.IsFalse(index.For(_b.Guid).Single().approximate);
        }

        [Test]
        public void DuplicatedScene_RefsReportedAsForeignLocal()
        {
            string _originalPath = FlagUsageTestScenes.Write("Original");
            string _originalGuid = AssetDatabase.AssetPathToGUID(_originalPath);
            FlagDefinition _local = Create(FlagScope.Local, _originalGuid, "Door");
            Scene _original = FlagUsageTestScenes.Open(_originalPath, scenes);
            FlagUsageTestScenes.AddRunner(FlagUsageTestScenes.Host(_original), new ActionList(new SetFlagAction(Ref(_local))));
            Assert.IsTrue(EditorSceneManager.SaveScene(_original));

            string _copyPath = FlagUsageTestScenes.Folder + "/Copy.unity";
            Assert.IsTrue(AssetDatabase.CopyAsset(_originalPath, _copyPath));
            string _copyGuid = AssetDatabase.AssetPathToGUID(_copyPath);
            FlagUsageMaintainer.Rebuild(new[] { FlagUsageTestScenes.Folder });

            List<FlagUsageEntry> _entries = index.For(_local.Guid).ToList();
            Assert.AreEqual(2, _entries.Count);
            FlagUsageEntry _inCopy = _entries.Single(_e => _e.sceneGuid == _copyGuid);
            FlagUsageEntry _inOriginal = _entries.Single(_e => _e.sceneGuid == _originalGuid);
            Assert.IsFalse(_inCopy.approximate, "Rebuild reads closed scenes exactly by opening them.");
            Assert.AreEqual(FlagRefState.ForeignLocal, FlagHostScope.StateOf(database, _inCopy.flagGuid, _inCopy.sceneGuid));
            Assert.AreEqual(FlagRefState.Valid, FlagHostScope.StateOf(database, _inOriginal.flagGuid, _inOriginal.sceneGuid));
            Assert.IsFalse(SceneManager.GetSceneByPath(_copyPath).isLoaded, "Rebuild closes the scenes it opened.");
        }

        [Test]
        public void DeletedScene_RemovesEntries()
        {
            FlagDefinition _flag = Create(FlagScope.Global, null, "A");
            Scene _scene = SavedScene("Deleted", new ActionList(new SetFlagAction(Ref(_flag))));
            string _path = _scene.path;
            FlagUsageMaintainer.FlushPending();
            Assert.AreEqual(1, index.For(_flag.Guid).Count());

            Close(_scene);
            Assert.IsTrue(AssetDatabase.DeleteAsset(_path));
            FlagUsageMaintainer.FlushPending();

            Assert.IsEmpty(index.For(_flag.Guid));
        }

        [Test]
        public void RenamedScene_UpdatesSceneNameInDatabase()
        {
            string _path = FlagUsageTestScenes.Write("OldName");
            string _sceneGuid = AssetDatabase.AssetPathToGUID(_path);
            FlagDefinition _local = Create(FlagScope.Local, _sceneGuid, "Door");
            Assert.AreEqual("OldName", _local.SceneName);

            Assert.IsEmpty(AssetDatabase.MoveAsset(_path, FlagUsageTestScenes.Folder + "/NewName.unity"));
            FlagUsageMaintainer.FlushPending();

            Assert.IsTrue(database.TryGet(_local.Guid, out FlagDefinition _after));
            Assert.AreEqual("NewName", _after.SceneName);
            Assert.AreEqual(_sceneGuid, _after.SceneGuid);
        }

        [Test]
        public void ImportedClosedScene_ReadAsApproximate()
        {
            FlagDefinition _flag = Create(FlagScope.Global, null, "A");
            Scene _scene = SavedScene("External", new ActionList(new SetFlagAction(Ref(_flag))));
            string _path = _scene.path;
            Close(_scene);
            index.RemoveScene(AssetDatabase.AssetPathToGUID(_path));

            // As after a git pull: the file changes while the scene is closed.
            File.SetLastWriteTimeUtc(_path, File.GetLastWriteTimeUtc(_path).AddSeconds(5));
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);
            FlagUsageMaintainer.FlushPending();

            FlagUsageEntry _entry = index.For(_flag.Guid).Single();
            Assert.IsTrue(_entry.approximate);
            Assert.AreEqual(FlagUsageKind.Other, _entry.kind);
        }

        [Test]
        public void SavedThenClosedBeforeTick_KeepsExactEntries()
        {
            FlagDefinition _flag = Create(FlagScope.Global, null, "A");
            // "Save changes?" → Save → another scene opens: the save's import is processed after the scene is closed.
            Scene _scene = SavedScene("SavedClosed", new ActionList(new SetFlagAction(Ref(_flag))));
            Close(_scene);
            FlagUsageMaintainer.FlushPending();

            FlagUsageEntry _entry = index.For(_flag.Guid).Single();
            Assert.IsFalse(_entry.approximate);
            Assert.AreEqual(FlagUsageKind.Write, _entry.kind);
            Assert.AreEqual("Host", _entry.hierarchyPath);
        }

        [Test]
        public void PrefabInstanceInScene_RebuildMatchesSave()
        {
            FlagDefinition _flag = Create(FlagScope.Global, null, "A");
            string _prefabPath = FlagUsageTestScenes.Folder + "/Switch.prefab";
            GameObject _source = new GameObject("Switch");
            FlagUsageTestScenes.AddRunner(_source, new ActionList(new SetFlagAction(Ref(_flag))));
            GameObject _prefab = PrefabUtility.SaveAsPrefabAsset(_source, _prefabPath);
            Object.DestroyImmediate(_source);

            Scene _scene = FlagUsageTestScenes.Open(FlagUsageTestScenes.Write("WithInstance"), scenes);
            string _sceneGuid = AssetDatabase.AssetPathToGUID(_scene.path);
            PrefabUtility.InstantiatePrefab(_prefab, _scene);
            Assert.IsTrue(EditorSceneManager.SaveScene(_scene));
            FlagUsageMaintainer.FlushPending();
            List<FlagUsageEntry> _fromSave = SceneEntries(_sceneGuid);
            Assert.AreEqual(1, _fromSave.Count);

            Close(_scene);
            index.RemoveScene(_sceneGuid);
            FlagUsageMaintainer.Rebuild(new[] { FlagUsageTestScenes.Folder });

            List<FlagUsageEntry> _fromRebuild = SceneEntries(_sceneGuid);
            Assert.AreEqual(_fromSave.Count, _fromRebuild.Count);
            for (int _i = 0; _i < _fromSave.Count; _i++) Assert.IsTrue(_fromSave[_i].SameAs(_fromRebuild[_i]), "Rebuild must read the prefab instance like the save did.");
        }

        private List<FlagUsageEntry> SceneEntries(string _sceneGuid) => index.Entries.Where(_e => _e.sceneGuid == _sceneGuid).ToList();

        [Test]
        public void ImportedAsset_IndexedAndRemovedOnDelete()
        {
            FlagDefinition _flag = Create(FlagScope.Global, null, "A");
            string _path = FlagUsageTestScenes.Folder + "/Holder.asset";
            FlagUsageTestAsset _asset = ScriptableObject.CreateInstance<FlagUsageTestAsset>();
            _asset.flag = Ref(_flag);
            AssetDatabase.CreateAsset(_asset, _path);
            FlagUsageMaintainer.FlushPending();

            Assert.AreEqual(_path, index.For(_flag.Guid).Single().assetPath);

            AssetDatabase.DeleteAsset(_path);
            FlagUsageMaintainer.FlushPending();

            Assert.IsEmpty(index.For(_flag.Guid));
        }

        [Test]
        public void NoDatabase_DoesNothing()
        {
            FlagDatabaseLocator.OverrideForTests = null;
            if (FlagDatabaseLocator.Find() != null) Assert.Ignore("The project has a flag database.");
            FlagUsageIndexLocator.OverrideForTests = index;

            FlagUsageMaintainer.Rebuild(new[] { FlagUsageTestScenes.Folder });
            FlagUsageMaintainer.RefreshLoadedScenes();

            Assert.IsEmpty(index.Entries);
            FlagDatabaseLocator.OverrideForTests = database;
        }
    }
}
