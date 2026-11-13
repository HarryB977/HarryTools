using System.Collections.Generic;
using System.IO;
using System.Text;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.Flags.Tests
{
    public class FlagUsageIndexerTests
    {
        // ==================== VARIABLES ====================

        private const string FlagA = "0123456789abcdef0123456789abcdef";
        private const string FlagB = "fedcba9876543210fedcba9876543210";

        private FlagDatabase database;
        private FlagUsageIndex index;
        private readonly List<Scene> scenes = new List<Scene>();
        private readonly List<Object> created = new List<Object>();

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            // The maintainer reacts to the temporary scenes and assets; it must only ever see these instances.
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
            foreach (Object _object in created)
                if (_object != null) Object.DestroyImmediate(_object);
            created.Clear();
            AssetDatabase.DeleteAsset(FlagUsageTestScenes.Folder);
            AssetDatabase.Refresh();
            FlagUsageMaintainer.FlushPending();

            FlagUsageIndexLocator.OverrideForTests = null;
            FlagDatabaseLocator.OverrideForTests = null;
            Object.DestroyImmediate(index);
            Object.DestroyImmediate(database);
        }

        // ==================== HELPERS ====================

        private Scene OpenScene(string _name) => FlagUsageTestScenes.Open(FlagUsageTestScenes.Write(_name), scenes);

        private static FlagRef Ref(string _guid) => new FlagRef(_guid, "Global / Test");

        // ==================== SCENES ====================

        [Test]
        public void Scene_FindsFlagRefInsideActionListOfActionRunner()
        {
            Scene _scene = OpenScene("Runner");
            GameObject _button = FlagUsageTestScenes.Child(_scene, "Button");
            FlagUsageTestScenes.AddRunner(_button, new ActionList(new SetFlagAction(Ref(FlagA))));

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromLoadedScene(_scene);

            Assert.AreEqual(1, _entries.Count);
            FlagUsageEntry _entry = _entries[0];
            Assert.AreEqual(FlagA, _entry.flagGuid);
            Assert.AreEqual(FlagUsageKind.Write, _entry.kind);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(_scene.path), _entry.sceneGuid);
            Assert.AreEqual(string.Empty, _entry.assetPath);
            Assert.AreEqual("Host/Button", _entry.hierarchyPath);
            Assert.AreEqual(typeof(ActionRunner).FullName, _entry.componentType);
            Assert.IsFalse(_entry.approximate);
        }

        [Test]
        public void Scene_FlagListener_IsRead()
        {
            Scene _scene = OpenScene("Listener");
            FlagListener _listener = FlagUsageTestScenes.Host(_scene).AddComponent<FlagListener>();
            _listener.SetUpForTests(Ref(FlagA), new ActionList(), new ActionList(), new ActionList(), new ActionList());

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromLoadedScene(_scene);

            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual(FlagUsageKind.Read, _entries[0].kind);
            Assert.AreEqual(typeof(FlagListener).FullName, _entries[0].componentType);
            Assert.AreEqual("Host", _entries[0].hierarchyPath);
        }

        [Test]
        public void Scene_FlagIsSetInsideConditionList_IsRead()
        {
            Scene _scene = OpenScene("Condition");
            ConditionList _conditions = new ConditionList(ConditionList.EvaluationMode.All, new FlagIsSet(Ref(FlagA)));
            FlagUsageTestScenes.AddRunner(FlagUsageTestScenes.Host(_scene), new ActionList(_conditions, new ClearFlagAction(Ref(FlagB))));

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromLoadedScene(_scene);

            Assert.AreEqual(2, _entries.Count);
            Assert.AreEqual(FlagUsageKind.Read, _entries.Find(_e => _e.flagGuid == FlagA).kind);
            Assert.AreEqual(FlagUsageKind.Write, _entries.Find(_e => _e.flagGuid == FlagB).kind);
        }

        [Test]
        public void Scene_UserFieldWithAttribute_UsesAttributeKind()
        {
            Scene _scene = OpenScene("UserMarked");
            FlagUsageTestHost _host = FlagUsageTestScenes.Host(_scene).AddComponent<FlagUsageTestHost>();
            _host.marked = Ref(FlagA);

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromLoadedScene(_scene);

            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual(FlagUsageKind.Write, _entries[0].kind);
        }

        [Test]
        public void Scene_UserFieldWithoutAttribute_IsOther()
        {
            Scene _scene = OpenScene("UserPlain");
            FlagUsageTestHost _host = FlagUsageTestScenes.Host(_scene).AddComponent<FlagUsageTestHost>();
            _host.plain = Ref(FlagB);

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromLoadedScene(_scene);

            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual(FlagB, _entries[0].flagGuid);
            Assert.AreEqual(FlagUsageKind.Other, _entries[0].kind);
        }

        [Test]
        public void Scene_EmptyFlagRef_NotIndexed()
        {
            Scene _scene = OpenScene("Empty");
            FlagUsageTestScenes.AddRunner(FlagUsageTestScenes.Host(_scene), new ActionList(new SetFlagAction(new FlagRef())));

            Assert.IsEmpty(FlagUsageIndexer.FromLoadedScene(_scene));
        }

        // ==================== ASSETS ====================

        [Test]
        public void Asset_ScriptableObjectWithFlagRef_Indexed()
        {
            string _path = FlagUsageTestScenes.Folder + "/Holder.asset";
            FlagUsageTestAsset _asset = ScriptableObject.CreateInstance<FlagUsageTestAsset>();
            _asset.flag = Ref(FlagA);
            AssetDatabase.CreateAsset(_asset, _path);

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromAsset(_path);

            Assert.AreEqual(1, _entries.Count);
            FlagUsageEntry _entry = _entries[0];
            Assert.AreEqual(FlagA, _entry.flagGuid);
            Assert.AreEqual(FlagUsageKind.Read, _entry.kind);
            Assert.AreEqual(_path, _entry.assetPath);
            Assert.AreEqual(string.Empty, _entry.sceneGuid);
            Assert.AreEqual(typeof(FlagUsageTestAsset).FullName, _entry.componentType);
            Assert.IsFalse(_entry.approximate);
        }

        [Test]
        public void Asset_PrefabWithActionRunner_Indexed()
        {
            string _path = FlagUsageTestScenes.Folder + "/Holder.prefab";
            GameObject _root = new GameObject("Root");
            created.Add(_root);
            GameObject _child = new GameObject("Child");
            _child.transform.SetParent(_root.transform, false);
            FlagUsageTestScenes.AddRunner(_child, new ActionList(new ToggleFlagAction(Ref(FlagB))));
            PrefabUtility.SaveAsPrefabAsset(_root, _path);

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromAsset(_path);

            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual(FlagB, _entries[0].flagGuid);
            Assert.AreEqual(FlagUsageKind.Write, _entries[0].kind);
            Assert.AreEqual(_path, _entries[0].assetPath);
            // A prefab asset root is named after its file.
            Assert.AreEqual("Holder/Child", _entries[0].hierarchyPath);
        }

        // ==================== YAML ====================

        [Test]
        public void Yaml_FindsKnownGuid_AsApproximateOther()
        {
            Scene _scene = OpenScene("Yaml");
            FlagUsageTestScenes.AddRunner(FlagUsageTestScenes.Host(_scene), new ActionList(new SetFlagAction(Ref(FlagA))));
            string _path = _scene.path;
            Assert.IsTrue(EditorSceneManager.SaveScene(_scene));

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromSceneYaml(_path, new[] { FlagA, FlagB });

            Assert.AreEqual(1, _entries.Count);
            FlagUsageEntry _entry = _entries[0];
            Assert.AreEqual(FlagA, _entry.flagGuid);
            Assert.AreEqual(FlagUsageKind.Other, _entry.kind);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(_path), _entry.sceneGuid);
            Assert.IsTrue(_entry.approximate);
        }

        // ==================== TEXT CHECK ====================

        // Outside Assets: plain files that the AssetDatabase never imports.
        private static string TempFile(string _name) => Path.Combine("Temp", "__FlagsTests__" + _name);

        [Test]
        public void MayContainFlagRef_BinaryFile_SkippedWithoutError()
        {
            string _path = TempFile("Binary.asset");
            byte[] _bytes = new byte[64 * 1024];
            for (int _i = 0; _i < _bytes.Length; _i++) _bytes[_i] = (byte)(_i * 31);
            // Text that would look like a FlagRef if the binary file were searched.
            byte[] _fake = Encoding.ASCII.GetBytes("\n  guid: " + FlagA + "\n  label: x\n--- !u!1001 &1\n");
            System.Array.Copy(_fake, 0, _bytes, 128, _fake.Length);
            try
            {
                File.WriteAllBytes(_path, _bytes);
                Assert.IsFalse(FlagUsageIndexer.MayContainFlagRef(_path));
            }
            finally
            {
                File.Delete(_path);
            }
        }

        [Test]
        public void MayContainFlagRef_YamlFile_FindsFlagRefAndPrefabInstance()
        {
            string _path = TempFile("Text.asset");
            const string Header = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n";
            try
            {
                File.WriteAllText(_path, Header + "  flag:\n    guid: " + FlagA + "\n    label: Global / A\n");
                Assert.IsTrue(FlagUsageIndexer.MayContainFlagRef(_path), "Assigned FlagRef.");

                File.WriteAllText(_path, Header + "  flag:\n    guid: \n    label: \n  texture: {fileID: 0, guid: " + FlagB + ", type: 3}\n");
                Assert.IsFalse(FlagUsageIndexer.MayContainFlagRef(_path), "Empty FlagRef and an asset reference guid.");

                File.WriteAllText(_path, Header + "--- !u!1001 &5\nPrefabInstance:\n");
                Assert.IsTrue(FlagUsageIndexer.MayContainFlagRef(_path), "Prefab instance.");
            }
            finally
            {
                File.Delete(_path);
            }
        }

        // ==================== KIND ====================

        [Test]
        public void KindOf_AttributeWins_ThenListener_ThenOther()
        {
            Assert.AreEqual(FlagUsageKind.Write, FlagUsageIndexer.KindOf(typeof(FlagUsageTestHost), typeof(FlagUsageTestHost).GetField("marked")));
            Assert.AreEqual(FlagUsageKind.Other, FlagUsageIndexer.KindOf(typeof(FlagUsageTestHost), typeof(FlagUsageTestHost).GetField("plain")));
            Assert.AreEqual(FlagUsageKind.Read, FlagUsageIndexer.KindOf(typeof(FlagListener), null));
        }
    }
}
