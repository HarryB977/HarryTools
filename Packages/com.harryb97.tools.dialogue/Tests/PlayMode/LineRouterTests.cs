using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class LineRouterTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<Object> created = new List<Object>();
        private DialogueSettings settings;

        [SetUp]
        public void SetUp() {
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            DialogueSettings.UseForTests(settings);
            Dialogue.ManualTick = true;
        }

        [TearDown]
        public void TearDown() {
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            for (int _i = created.Count - 1; _i >= 0; _i--) {
                if (created[_i] != null) Object.DestroyImmediate(created[_i]);
            }
            created.Clear();
            Object.DestroyImmediate(settings);
        }

        private T Track<T>(T _object) where T : Object {
            created.Add(_object);
            return _object;
        }
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        private LineContainer NewContainer(string _name, bool _isMain = false, bool _isDefaultAmbient = false) {
            GameObject _object = Track(new GameObject(_name));
            _object.SetActive(false);
            LineContainer _container = _object.AddComponent<LineContainer>();
            _container.IsMain = _isMain;
            _container.IsDefaultAmbient = _isDefaultAmbient;
            _object.SetActive(true);
            return _container;
        }

        private LinePresentation Presentation(bool _isMain, DialogueActor _actor = null, DialogueRun _run = null) {
            if (_run == null) {
                DialogueAsset _asset = Track(ScriptableObject.CreateInstance<DialogueAsset>());
                _asset.name = "Routed";
                _run = new DialogueRun(_asset, new DialogueStartOptions { IsMain = _isMain });
            }
            LineNode _line = new LineNode();
            return new LinePresentation(_run, _line, _actor, LineLifetime.Default, AdvanceMode.Both, DialogueTime.Unscaled);
        }

        private DialogueActor NewActor(LineContainer _container) {
            GameObject _object = Track(new GameObject("Actor"));
            DialogueActor _actor = _object.AddComponent<DialogueActor>();
            _actor.Container = _container;
            return _actor;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Route_Main_ActorContainer_DefaultAmbient_Headless() {
            LineContainer _main = NewContainer("Main", _isMain: true);
            LineContainer _ambient = NewContainer("DefaultAmbient", _isDefaultAmbient: true);
            LineContainer _own = NewContainer("Own");
            DialogueActor _withContainer = NewActor(_own);
            DialogueActor _without = NewActor(null);

            Assert.AreSame(_main, LineContainer.Main);
            Assert.AreSame(_ambient, LineContainer.DefaultAmbient);

            // The default resolver is the router
            Assert.AreSame(_main, Dialogue.ResolveChannel(Presentation(true, _withContainer)));
            Assert.AreSame(_own, Dialogue.ResolveChannel(Presentation(false, _withContainer)));
            Assert.AreSame(_ambient, Dialogue.ResolveChannel(Presentation(false, _without)));
            Assert.AreSame(_ambient, Dialogue.ResolveChannel(Presentation(false)));

            // A disabled actor container is skipped
            _own.gameObject.SetActive(false);
            Assert.AreSame(_ambient, Dialogue.ResolveChannel(Presentation(false, _withContainer)));

            // Without containers: no view, warned once per run
            _ambient.gameObject.SetActive(false);
            _main.gameObject.SetActive(false);
            Assert.IsNull(LineContainer.Main);
            Assert.IsNull(LineContainer.DefaultAmbient);

            LinePresentation _ambientLine = Presentation(false, _without);
            LogAssert.Expect(LogType.Warning, new Regex("no default ambient container"));
            Assert.AreSame(HeadlessLineChannel.Instance, Dialogue.ResolveChannel(_ambientLine));
            Assert.AreSame(HeadlessLineChannel.Instance, Dialogue.ResolveChannel(Presentation(false, _without, _ambientLine.Run)));

            LinePresentation _mainLine = Presentation(true);
            LogAssert.Expect(LogType.Warning, new Regex("no main line container"));
            Assert.AreSame(HeadlessLineChannel.Instance, Dialogue.ResolveChannel(_mainLine));
            Assert.AreSame(HeadlessLineChannel.Instance, Dialogue.ResolveChannel(Presentation(true, null, _mainLine.Run)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SeveralMains_LastEnabledWins_Warns() {
            LineContainer _first = NewContainer("First", _isMain: true);
            LogAssert.Expect(LogType.Warning, new Regex("main line container"));
            LineContainer _second = NewContainer("Second", _isMain: true);
            Assert.AreSame(_second, LineContainer.Main);

            _second.gameObject.SetActive(false);
            Assert.AreSame(_first, LineContainer.Main);
            LogAssert.NoUnexpectedReceived();
        }
        #endregion
    }
}
