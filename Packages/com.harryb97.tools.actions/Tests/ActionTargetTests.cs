using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

using HarryTools.Actions.Variants;
using HarryTools.Conditions;
using HarryTools.SceneRefs;
using Registry = HarryTools.SceneRefs.SceneRefs;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Tests {
    public class ActionTargetTests {
        // ==================== TYPES ====================
        #region Types
        private class ActionHolder : ScriptableObject {
            public ActionList list;
        }

        private class CountAction : InstantAction {
            public static int Count;
            protected override void Execute(LogicContext _context) => Count++;
        }

        private class WaitForeverAction : IAction {
            public UniTask Run(LogicContext _context, CancellationToken _token) {
                return UniTask.WaitUntil(() => false, cancellationToken: _token);
            }
        }
        #endregion

        // ==================== VARIABLES ===================
        #region Private Variables
        private const string GuidA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1";
        private const string GuidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb2";
        private const string GuidMissing = "ffffffffffffffffffffffffffffffff";

        private readonly List<Object> created = new();
        private Scene other;
        #endregion

        // ==================== SETUP ====================
        [SetUp]
        public void SetUp() {
            Registry.Clear();
            CountAction.Count = 0;
            other = SceneManager.CreateScene("ActionTargetTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown() {
            foreach (Object _object in created) if (_object != null) Object.DestroyImmediate(_object);
            created.Clear();
            if (other.IsValid()) SceneManager.UnloadSceneAsync(other);
            Registry.Clear();
        }

        // ==================== HELPERS ====================
        private GameObject MakeInOther(string _name, string _guid, bool _active = true) {
            GameObject _go = new(_name);
            SceneManager.MoveGameObjectToScene(_go, other);
            created.Add(_go);
            _go.AddComponent<SceneId>().Set(_guid, _name.ToLowerInvariant());
            _go.SetActive(_active);
            return _go;
        }

        private void Scan() => Registry.ScanScene(other);

        private static void SetField(object _target, string _field, object _value) {
            FieldInfo _info = _target.GetType().GetField(_field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_info, _field);
            _info.SetValue(_target, _value);
        }

        private static TargetGameObject GameObjectByGuid(string _guid) {
            SceneRef<GameObject> _ref = new();
            _ref.SetForTests(null, _guid, string.Empty, 0, "test");
            TargetGameObject _target = new();
            SetField(_target, "custom", _ref);
            return _target;
        }

        /// <summary> Reference by guid to the Nth component of the exact type '_concrete', exposed as T </summary>
        private static TargetComponent<T> ComponentByGuid<T>(string _guid, System.Type _concrete, int _index = 0) where T : Component {
            SceneRef<T> _ref = new();
            _ref.SetForTests(null, _guid, _concrete.AssemblyQualifiedName, _index, "test");
            TargetComponent<T> _target = new();
            SetField(_target, "custom", _ref);
            return _target;
        }

        private static IEnumerator Run(IAction _action) {
            ActionHolder _holder = ScriptableObject.CreateInstance<ActionHolder>();
            _holder.list = new ActionList(_action);
            ActionHandle _handle = _holder.list.Run(LogicContext.Empty);
            yield return _handle.Task.ToCoroutine();
            Object.DestroyImmediate(_holder);
        }

        // ==================== TESTS ====================
        [UnityTest]
        public IEnumerator SetActive_ByGuid_ActivatesInactiveObjectInOtherScene() {
            GameObject _go = MakeInOther("Door", GuidA, false);
            Scan();

            SetActiveAction _action = new();
            SetField(_action, "target", GameObjectByGuid(GuidA));
            yield return Run(_action);

            Assert.IsTrue(_go.activeSelf);
        }

        [UnityTest]
        public IEnumerator SetBehaviourEnabled_ArrayOfTargets_ExactComponents() {
            GameObject _go = MakeInOther("Lamp", GuidA);
            AudioSource _first = _go.AddComponent<AudioSource>();
            AudioSource _second = _go.AddComponent<AudioSource>();
            AudioSource _direct = new GameObject("Direct").AddComponent<AudioSource>();
            created.Add(_direct.gameObject);
            Scan();

            SetBehaviourEnabledAction _action = new();
            SetField(_action, "behaviours", new[] {
                ComponentByGuid<Behaviour>(GuidA, typeof(AudioSource), 1),
                ComponentByGuid<Behaviour>(GuidMissing, typeof(AudioSource)),
                null,
                new TargetComponent<Behaviour>(TargetSource.Custom, _direct)
            });
            SetField(_action, "mode", ToggleMode.Disable);
            yield return Run(_action);

            Assert.IsTrue(_first.enabled);
            Assert.IsFalse(_second.enabled);
            Assert.IsFalse(_direct.enabled);
        }

        [UnityTest]
        public IEnumerator RunActionRunner_ByGuid_Runs() {
            GameObject _go = MakeInOther("Runner", GuidA);
            ActionRunner _runner = _go.AddComponent<ActionRunner>();
            SetField(_runner, "actions", new ActionList(new CountAction()));
            Scan();

            RunActionRunnerAction _action = new();
            SetField(_action, "runner", ComponentByGuid<ActionRunner>(GuidA, typeof(ActionRunner)));
            yield return Run(_action);

            Assert.AreEqual(1, CountAction.Count);
        }

        [UnityTest]
        public IEnumerator CancelActionRunner_ByGuid_Cancels() {
            GameObject _go = MakeInOther("Runner", GuidA);
            ActionRunner _runner = _go.AddComponent<ActionRunner>();
            SetField(_runner, "actions", new ActionList(new WaitForeverAction()));
            Scan();

            _runner.Run();
            Assert.IsTrue(_runner.IsRunning);

            CancelActionRunnerAction _action = new();
            SetField(_action, "runner", ComponentByGuid<ActionRunner>(GuidA, typeof(ActionRunner)));
            yield return Run(_action);
            yield return null;

            Assert.IsFalse(_runner.IsRunning);
        }

        [UnityTest]
        public IEnumerator Instantiate_ParentEmpty_IsSceneRoot() {
            GameObject _prefab = new("Prefab");
            created.Add(_prefab);

            InstantiateAction _action = new();
            SetField(_action, "prefab", _prefab);
            yield return Run(_action);

            GameObject _clone = GameObject.Find("Prefab(Clone)");
            Assert.IsNotNull(_clone);
            created.Add(_clone);
            Assert.IsNull(_clone.transform.parent);
        }

        [UnityTest]
        public IEnumerator Instantiate_ParentByGuid_Parents() {
            GameObject _parent = MakeInOther("Holder", GuidB);
            Scan();
            GameObject _prefab = new("Prefab");
            created.Add(_prefab);

            InstantiateAction _action = new();
            SetField(_action, "prefab", _prefab);
            SetField(_action, "parent", ComponentByGuid<Transform>(GuidB, typeof(Transform)));
            yield return Run(_action);

            Assert.AreEqual(1, _parent.transform.childCount);
            Assert.AreEqual("Prefab(Clone)", _parent.transform.GetChild(0).name);
        }
    }
}
