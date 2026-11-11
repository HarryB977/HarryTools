using System.Reflection;
using NUnit.Framework;
using UnityEngine;

using HarryTools.Conditions.Variants;
using HarryTools.SceneRefs;
using Registry = HarryTools.SceneRefs.SceneRefs;

namespace HarryTools.Conditions.Tests {
    public class TargetReferenceTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly System.Collections.Generic.List<GameObject> created = new();
        #endregion

        // ==================== SETUP ====================
        [SetUp]
        public void SetUp() => Registry.Clear();

        [TearDown]
        public void TearDown() {
            foreach (GameObject _go in created) if (_go != null) Object.DestroyImmediate(_go);
            created.Clear();
            Registry.Clear();
        }

        // ==================== HELPERS ====================
        private GameObject Make(string _name) {
            GameObject _go = new(_name);
            created.Add(_go);
            return _go;
        }

        private static void SetField(object _target, string _field, object _value) {
            FieldInfo _info = _target.GetType().GetField(_field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_info, _field);
            _info.SetValue(_target, _value);
        }

        // ==================== TESTS ====================
        [Test]
        public void Custom_Direct_Resolves() {
            GameObject _go = Make("a");
            TargetGameObject _target = new();
            SetField(_target, "custom", new SceneRef<GameObject>(_go));
            Assert.AreSame(_go, _target.Resolve(LogicContext.Empty));

            Light _light = _go.AddComponent<Light>();
            TargetComponent<Light> _component = new();
            SetField(_component, "custom", new SceneRef<Light>(_light));
            Assert.AreSame(_light, _component.Resolve(LogicContext.Empty));
        }

        [Test]
        public void Custom_ByGuid_ResolvesFromScriptableObjectContext() {
            GameObject _go = Make("b");
            Light _light = _go.AddComponent<Light>();
            SceneId _sceneId = _go.AddComponent<SceneId>();
            _sceneId.Set("0123456789abcdef0123456789abcdef", "cond-test");
            Registry.Register(_sceneId);

            SceneRef<Light> _ref = new();
            _ref.SetForTests(null, "0123456789abcdef0123456789abcdef", string.Empty, 0, "b");
            TargetComponent<Light> _target = new();
            SetField(_target, "custom", _ref);

            Assert.AreSame(_light, _target.Resolve(LogicContext.Empty));
        }

        [Test]
        public void Self_And_Target_Unchanged() {
            GameObject _self = Make("self");
            GameObject _other = Make("other");
            Light _selfLight = _self.AddComponent<Light>();
            LogicContext _context = new(_self, _other);

            Assert.AreSame(_self, new TargetGameObject(TargetSource.Self).Resolve(_context));
            Assert.AreSame(_other, new TargetGameObject(TargetSource.Target).Resolve(_context));
            Assert.AreSame(_selfLight, new TargetComponent<Light>(TargetSource.Self).Resolve(_context));
            Assert.IsNull(new TargetComponent<Light>(TargetSource.Target).Resolve(_context));
        }

        [Test]
        public void Ctor_WithValue_StillWorks() {
            GameObject _go = Make("c");
            Assert.AreSame(_go.transform, new TargetComponent<Transform>(TargetSource.Custom, _go.transform).Resolve(LogicContext.Empty));
            Assert.AreSame(_go, new TargetGameObject(TargetSource.Custom, _go).Resolve(LogicContext.Empty));
            Assert.IsNull(new TargetGameObject(TargetSource.Custom).Resolve(LogicContext.Empty));
        }

        [Test]
        public void BehaviourIsEnabled_ResolvesViaTargetComponent() {
            GameObject _go = Make("d");
            Light _light = _go.AddComponent<Light>();
            BehaviourIsEnabled _condition = new();
            SetField(_condition, "behaviour", new TargetComponent<Behaviour>(TargetSource.Custom, _light));

            Assert.IsTrue(_condition.Check(LogicContext.Empty));
            _light.enabled = false;
            Assert.IsFalse(_condition.Check(LogicContext.Empty));

            BehaviourIsEnabled _self = new();
            SetField(_self, "behaviour", new TargetComponent<Behaviour>(TargetSource.Self));
            _light.enabled = true;
            Assert.IsTrue(_self.Check(new LogicContext(_go)));
        }
    }
}
