using System.Collections.Generic;
using HarryTools.Actions;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Dialogue.Tests {
    public class ActorTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<Object> created = new List<Object>();
        private DialogueSettings settings;
        private FakeLineChannel channel;
        private DialogueCharacter hero;

        [SetUp]
        public void SetUp() {
            RecordingAction.Log.Clear();
            RecordingAction.LastSelf = null;
            DialogueMemory.ClearForTests();
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            DialogueSettings.UseForTests(settings);
            Dialogue.ManualTick = true;
            channel = new FakeLineChannel();
            Dialogue.ChannelResolver = _ => channel;
            hero = Track(ScriptableObject.CreateInstance<DialogueCharacter>());
        }

        [TearDown]
        public void TearDown() {
            Dialogue.StopAll();
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            for (int _i = 0; _i < created.Count; _i++) Object.DestroyImmediate(created[_i]);
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
        private DialogueActor NewActor(DialogueCharacter _character, Vector3 _position) {
            GameObject _object = Track(new GameObject("Actor"));
            _object.transform.position = _position;
            DialogueActor _actor = _object.AddComponent<DialogueActor>();
            _actor.Character = _character;
            return _actor;
        }

        private LineNode NewLine(out DialogueAsset _asset, DialogueCharacter _character) {
            _asset = Track(ScriptableObject.CreateInstance<DialogueAsset>());
            LineNode _line = _asset.AddNode<LineNode>();
            _line.Character = _character;
            _line.OnStart.Actions = new ActionList(new RecordingAction("start"));
            _asset.Start.Next.Target = _line.Id;
            return _line;
        }

        private LinePresentation RunFirstLine(DialogueAsset _asset, DialogueStartOptions _options = null) {
            Dialogue.Start(_asset, _options);
            Dialogue.TickAll(0f, 0f);
            return channel.Last.Presentation;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Resolve_ExplicitBindingWins() {
            DialogueActor _registered = NewActor(hero, Vector3.zero);
            DialogueActor _bound = NewActor(null, new Vector3(50f, 0f, 0f));
            NewLine(out DialogueAsset _asset, hero);

            LinePresentation _presentation = RunFirstLine(_asset, new DialogueStartOptions {
                Actors = new List<ActorBinding> { new ActorBinding(hero, _bound) }
            });

            Assert.AreSame(_bound, _presentation.Actor);
            Assert.AreNotSame(_registered, _presentation.Actor);
        }

        [Test]
        public void Resolve_Nearest_ToOrigin() {
            NewActor(hero, new Vector3(100f, 0f, 0f));
            DialogueActor _near = NewActor(hero, new Vector3(2f, 0f, 0f));
            NewActor(hero, new Vector3(-30f, 0f, 0f));
            GameObject _origin = Track(new GameObject("Origin"));
            NewLine(out DialogueAsset _asset, hero);

            LinePresentation _presentation = RunFirstLine(_asset, new DialogueStartOptions { Origin = _origin });

            Assert.AreSame(_near, _presentation.Actor);
        }

        [Test]
        public void Resolve_NoOrigin_FirstRegistered() {
            DialogueActor _first = NewActor(hero, new Vector3(100f, 0f, 0f));
            NewActor(hero, Vector3.zero);
            NewLine(out DialogueAsset _asset, hero);

            LinePresentation _presentation = RunFirstLine(_asset);

            Assert.AreSame(_first, _presentation.Actor);
        }

        [Test]
        public void Resolve_None_SelfNull() {
            NewActor(Track(ScriptableObject.CreateInstance<DialogueCharacter>()), Vector3.zero);
            NewLine(out DialogueAsset _characterless, null);
            NewLine(out DialogueAsset _unmatched, hero);

            LinePresentation _a = RunFirstLine(_characterless);
            Assert.IsNull(_a.Actor);
            Assert.IsNull(RecordingAction.LastSelf);

            RecordingAction.Log.Clear();
            LinePresentation _b = RunFirstLine(_unmatched);
            Assert.IsNull(_b.Actor);
            Assert.IsNull(_b.ActorObject);
            Assert.IsNull(RecordingAction.LastSelf);
        }

        [Test]
        public void LineActions_SelfIsActor() {
            DialogueActor _actor = NewActor(hero, Vector3.zero);
            NewLine(out DialogueAsset _asset, hero);
            NewLine(out DialogueAsset _skipped, hero);

            RunFirstLine(_asset);
            Assert.AreSame(_actor.gameObject, RecordingAction.LastSelf);

            Dialogue.StopAll();
            // The skip path queues the line's actions with the same Self
            RecordingAction.LastSelf = null;
            DialogueRun _run = Dialogue.Start(_skipped);
            _run.Skip();
            Dialogue.TickAll(0f, 0f);
            Assert.AreSame(_actor.gameObject, RecordingAction.LastSelf);
        }

        [Test]
        public void DisabledActor_NotFound() {
            DialogueActor _actor = NewActor(hero, Vector3.zero);
            Assert.AreSame(_actor, DialogueActors.Find(hero, null));

            _actor.enabled = false;
            Assert.IsNull(DialogueActors.Find(hero, null));
            Assert.AreEqual(0, DialogueActors.All.Count);

            _actor.enabled = true;
            Assert.AreSame(_actor, DialogueActors.Find(hero, Vector3.one));
        }
        #endregion
    }
}
