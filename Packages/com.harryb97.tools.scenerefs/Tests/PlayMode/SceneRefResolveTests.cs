using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneRefResolveTests
    {
        // ==================== VARIABLES ====================

        private const string AudioSourceType = "UnityEngine.AudioSource, UnityEngine.AudioModule";
        private const string LightType = "UnityEngine.Light, UnityEngine.CoreModule";
        private static readonly Regex SceneRefsRegex = new Regex("SceneRefs");

        private readonly List<Scene> scenes = new List<Scene>();

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp() => SceneRefs.Clear();

        [TearDown]
        public void TearDown() => SceneRefsTestUtil.UnloadAll(scenes);

        // ==================== HELPERS ====================

        private static SceneRef<T> MakeRef<T>(string _guid, string _type, int _index, string _label) where T : Object
        {
            SceneRef<T> _ref = new SceneRef<T>();
            _ref.SetForTests(null, _guid, _type, _index, _label);
            return _ref;
        }

        // ==================== TESTS ====================

        [Test]
        public void Direct_ReturnsDirect()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("ResA", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "L", "g1", "Lamp");
            Light _light = _go.AddComponent<Light>();

            Assert.AreEqual(_light, new SceneRef<Light>(_light).Value);
        }

        [Test]
        public void ByGuid_ResolvesAcrossScenes()
        {
            Scene _a = SceneRefsTestUtil.NewScene("ResB1", scenes);
            Scene _b = SceneRefsTestUtil.NewScene("ResB2", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_b, "L", "g1", "Lamp");
            Light _light = _go.AddComponent<Light>();
            SceneRefs.ScanScene(_a);
            SceneRefs.ScanScene(_b);

            Assert.AreEqual(_light, MakeRef<Light>("g1", LightType, 0, "Lamp in ResB2").Value);
        }

        [Test]
        public void ByGuid_ComponentExact()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("ResC", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "S", "g1", "Speaker");
            _go.AddComponent<AudioSource>();
            AudioSource _second = _go.AddComponent<AudioSource>();
            SceneRefs.ScanScene(_scene);

            Assert.AreEqual(_second, MakeRef<AudioSource>("g1", AudioSourceType, 1, "Speaker in ResC").Value);
        }

        [Test]
        public void ByGuid_ComponentIndex_CountsExactTypeOnly()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("ResExact", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "B", "g1", "Behaviours");
            // A subclass placed before the target must not shift its index.
            _go.AddComponent<DerivedTestBehaviour>();
            BaseTestBehaviour _base = _go.AddComponent<BaseTestBehaviour>();
            SceneRefs.ScanScene(_scene);

            string _type = $"{typeof(BaseTestBehaviour).FullName}, {typeof(BaseTestBehaviour).Assembly.GetName().Name}";
            Assert.AreSame(_base, MakeRef<BaseTestBehaviour>("g1", _type, 0, "Behaviours in ResExact").Value);
        }

        [Test]
        public void ByGuid_ComponentMissing_ReturnsNullAndWarns()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("ResD", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "S", "g1", "Speaker");
            _go.AddComponent<AudioSource>();
            SceneRefs.ScanScene(_scene);

            LogAssert.Expect(LogType.Warning, SceneRefsRegex);
            Assert.IsNull(MakeRef<AudioSource>("g1", AudioSourceType, 3, "Speaker in ResD").Value);
        }

        [Test]
        public void GameObjectT_ReturnsGameObject()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("ResE", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "G", "g1", "Thing");
            SceneRefs.ScanScene(_scene);

            Assert.AreEqual(_go, MakeRef<GameObject>("g1", "", 0, "Thing in ResE").Value);
        }

        [Test]
        public void NotLoaded_ReturnsNull_WarnsOncePerLoadVersion()
        {
            SceneRef<Light> _ref = MakeRef<Light>("missing", LightType, 0, "Lamp in Cocina");

            LogAssert.Expect(LogType.Warning, SceneRefsRegex);
            Assert.IsNull(_ref.Value);
            Assert.IsNull(_ref.Value);

            Scene _scene = SceneRefsTestUtil.NewScene("ResF", scenes);
            SceneRefsTestUtil.NewObject(_scene, "X", "other", "Other");
            SceneRefs.ScanScene(_scene);

            LogAssert.Expect(LogType.Warning, SceneRefsRegex);
            Assert.IsNull(_ref.Value);
            Assert.IsNull(_ref.Value);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NewSession_WarnsAgain_EvenWhenLoadCountRepeats()
        {
            // Domain reload off: an asset-owned SceneRef keeps its warned state between Play sessions.
            SceneRef<Light> _ref = MakeRef<Light>("missing", LightType, 0, "Lamp in Cocina");
            SceneRefs.ResetState();

            LogAssert.Expect(LogType.Warning, SceneRefsRegex);
            Assert.IsNull(_ref.Value);

            SceneRefs.ResetState();

            LogAssert.Expect(LogType.Warning, SceneRefsRegex);
            Assert.IsNull(_ref.Value);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ComponentTypeFromOtherAssembly_FallsBackToFullName()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("ResMoved", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "L", "g1", "Lamp");
            Light _light = _go.AddComponent<Light>();
            SceneRefs.ScanScene(_scene);

            // The script moved to another assembly since the reference was stored.
            Assert.AreEqual(_light, MakeRef<Light>("g1", "UnityEngine.Light, Some.Old.Assembly", 0, "Lamp in ResMoved").Value);
        }

        [Test]
        public void UnresolvableComponentType_ReturnsNullAndWarns()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("ResG", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "S", "g1", "Speaker");
            _go.AddComponent<AudioSource>();
            SceneRefs.ScanScene(_scene);
            SceneRef<Component> _ref = MakeRef<Component>("g1", "Nope.Missing, NoAssembly", 0, "Speaker in ResG");

            LogAssert.Expect(LogType.Warning, SceneRefsRegex);
            Assert.IsNull(_ref.Value);
            Assert.IsNull(_ref.Value);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NotLoaded_WarningUsesLabel()
        {
            SceneRef<Light> _ref = MakeRef<Light>("missing", LightType, 0, "Manzana01 in Cocina");

            LogAssert.Expect(LogType.Warning, new Regex("last known as Manzana01 in Cocina"));
            Assert.IsNull(_ref.Value);
        }

        [Test]
        public void ResolveBeforeLoad_ThenAfterLoad_Resolves()
        {
            SceneRef<Light> _ref = MakeRef<Light>("g1", LightType, 0, "Lamp in Late");

            LogAssert.Expect(LogType.Warning, SceneRefsRegex);
            Assert.IsNull(_ref.Value);

            Scene _scene = SceneRefsTestUtil.NewScene("Late", scenes);
            Light _light = SceneRefsTestUtil.NewObject(_scene, "L", "g1", "Lamp").AddComponent<Light>();
            SceneRefs.ScanScene(_scene);

            Assert.AreEqual(_light, _ref.Value);
        }

        [Test]
        public void Empty_ReturnsNull_NoWarning()
        {
            Assert.IsNull(new SceneRef<Light>().Value);
            Assert.IsFalse(new SceneRef<Light>().IsAssigned);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
