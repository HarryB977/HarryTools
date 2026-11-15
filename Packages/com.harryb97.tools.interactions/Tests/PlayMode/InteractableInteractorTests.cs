using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Interactions.Tests {
    public class InteractableInteractorTests {
        // ==================== HELPERS ===================
        private sealed class CountingResponse : IInteractionResponse {
            public int Calls;

            public bool IsRunning => false;
            public void Respond(Interactable _source, IInteractor _interactor) => Calls++;
        }

        private readonly List<Object> created = new();
        private InteractionType primary;
        private InteractionType secondary;
        private Interactor interactor;

        [SetUp]
        public void SetUp() {
            primary = Track(ScriptableObject.CreateInstance<InteractionType>());
            secondary = Track(ScriptableObject.CreateInstance<InteractionType>());
            interactor = Track(new GameObject("Interactor")).AddComponent<Interactor>();
        }

        [TearDown]
        public void TearDown() {
            foreach (Object _object in created) if (_object != null) Object.Destroy(_object);
            created.Clear();
        }

        private T Track<T>(T _object) where T : Object {
            created.Add(_object);
            return _object;
        }

        private Interactable CreateInteractable(string _name, params Interaction[] _interactions) {
            Interactable _interactable = Track(new GameObject(_name)).AddComponent<Interactable>();
            foreach (Interaction _interaction in _interactions) _interactable.AddInteraction(_interaction);
            return _interactable;
        }

        private Interactable CreateInteractable(string _name) => CreateInteractable(_name, new Interaction(primary));

        // ==================== TESTS ===================
        [Test]
        public void SetFocus_RaisesEnterOnNew_AndExitOnPrevious_InOrder() {
            Interactable _a = CreateInteractable("A");
            Interactable _b = CreateInteractable("B");
            List<string> _log = new();
            _a.FocusEntered += _ => _log.Add("enter:A");
            _a.FocusExited += _ => _log.Add("exit:A");
            _b.FocusEntered += _ => _log.Add("enter:B");

            interactor.SetFocus(_a);
            _log.Clear();
            interactor.SetFocus(_b);

            CollectionAssert.AreEqual(new[] { "exit:A", "enter:B" }, _log);
            Assert.AreSame(_b, interactor.Focus);
        }

        [Test]
        public void SetFocus_SameTarget_RaisesNothing() {
            Interactable _a = CreateInteractable("A");
            interactor.SetFocus(_a);
            int _events = 0;
            _a.FocusEntered += _ => _events++;
            _a.FocusExited += _ => _events++;
            interactor.FocusChanged += (_, _) => _events++;

            interactor.SetFocus(_a);

            Assert.AreEqual(0, _events);
        }

        [Test]
        public void SetFocus_ToInactiveInteractable_ResultsInNullFocus() {
            Interactable _a = CreateInteractable("A");
            _a.SetActive(false);

            interactor.SetFocus(_a);

            Assert.IsNull(interactor.Focus);
        }

        [Test]
        public void SetFocus_ToInteractableWithoutActiveInteractions_ResultsInNullFocus() {
            Interaction _interaction = new(primary);
            _interaction.SetActive(false);
            Interactable _a = CreateInteractable("A", _interaction);

            interactor.SetFocus(_a);

            Assert.IsNull(interactor.Focus);
        }

        [Test]
        public void Interact_ExecutesMatchingType_AndRaisesInteractedOnBoth() {
            CountingResponse _response = new();
            Interactable _a = CreateInteractable("A", new Interaction(primary, null, _response));
            int _onInteractable = 0, _onInteractor = 0;
            _a.Interacted += (_, _) => _onInteractable++;
            interactor.Interacted += (_, _) => _onInteractor++;
            interactor.SetFocus(_a);

            bool _executed = interactor.Interact(primary);

            Assert.IsTrue(_executed);
            Assert.AreEqual(1, _response.Calls);
            Assert.AreEqual(1, _onInteractable);
            Assert.AreEqual(1, _onInteractor);
        }

        [Test]
        public void Interact_WithoutFocus_ReturnsFalse() {
            int _events = 0;
            interactor.Interacted += (_, _) => _events++;

            Assert.IsFalse(interactor.Interact(primary));
            Assert.AreEqual(0, _events);
        }

        [Test]
        public void Interact_WithNullType_ReturnsFalse() {
            interactor.SetFocus(CreateInteractable("A"));
            int _events = 0;
            interactor.Interacted += (_, _) => _events++;

            Assert.IsFalse(interactor.Interact(null));
            Assert.AreEqual(0, _events);
        }

        [Test]
        public void Interact_WithMissingType_ReturnsFalse() {
            interactor.SetFocus(CreateInteractable("A"));
            int _events = 0;
            interactor.Interacted += (_, _) => _events++;

            Assert.IsFalse(interactor.Interact(secondary));
            Assert.AreEqual(0, _events);
        }

        [Test]
        public void Interact_UsesFirstActiveInteractionOfType() {
            CountingResponse _first = new();
            CountingResponse _second = new();
            Interaction _inactive = new(primary, null, _first);
            _inactive.SetActive(false);
            interactor.SetFocus(CreateInteractable("A", _inactive, new Interaction(primary, null, _second)));

            interactor.Interact(primary);

            Assert.AreEqual(0, _first.Calls);
            Assert.AreEqual(1, _second.Calls);
        }

        [UnityTest]
        public IEnumerator FocusedInteractable_Destroyed_ClearsFocus() {
            Interactable _a = CreateInteractable("A");
            int _exits = 0;
            _a.FocusExited += _ => _exits++;
            interactor.SetFocus(_a);

            Object.Destroy(_a.gameObject);
            yield return null;

            Assert.IsTrue(interactor.Focus == null);
            Assert.AreEqual(1, _exits);
            Assert.DoesNotThrow(() => interactor.SetFocus(CreateInteractable("B")));
        }

        [Test]
        public void FocusedInteractable_Disabled_ClearsFocus() {
            Interactable _a = CreateInteractable("A");
            interactor.SetFocus(_a);

            _a.enabled = false;

            Assert.IsNull(interactor.Focus);
        }

        [Test]
        public void FocusLosesLastActiveInteraction_ClearsFocus() {
            Interaction _interaction = new(primary);
            interactor.SetFocus(CreateInteractable("A", _interaction));
            Interactable _previous = null, _current = null;
            interactor.FocusChanged += (_old, _new) => { _previous = _old; _current = _new; };

            _interaction.SetActive(false);

            Assert.IsNull(interactor.Focus);
            Assert.IsNotNull(_previous);
            Assert.IsNull(_current);
        }

        [Test]
        public void FocusTextChange_RaisesFocusUpdated() {
            Interaction _interaction = new(primary, new FixedText("Open"));
            Interactable _a = CreateInteractable("A", _interaction);
            interactor.SetFocus(_a);
            int _updates = 0;
            interactor.FocusUpdated += _ => _updates++;

            _interaction.SetText("Close");

            Assert.AreEqual(1, _updates);
            Assert.AreSame(_a, interactor.Focus);
        }

        [Test]
        public void InteractorDisabled_ClearsFocus() {
            Interactable _a = CreateInteractable("A");
            int _exits = 0;
            _a.FocusExited += _ => _exits++;
            interactor.SetFocus(_a);

            interactor.enabled = false;

            Assert.IsNull(interactor.Focus);
            Assert.AreEqual(1, _exits);
        }
    }
}
