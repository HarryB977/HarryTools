using System;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Interactions.Tests {
    public class InteractionTests {
        // ==================== HELPERS ===================
        private sealed class CountingResponse : IInteractionResponse {
            public int Calls;
            public bool Running;

            public bool IsRunning => Running;
            public void Respond(Interactable _source, IInteractor _interactor) => Calls++;
        }

        private sealed class ManualText : IInteractionText {
            public string Current = "a";

            public string Value => Current;
            public event Action Changed;

            public void Raise() => Changed?.Invoke();
        }

        private InteractionType type;

        [SetUp]
        public void SetUp() {
            type = ScriptableObject.CreateInstance<InteractionType>();
        }

        [TearDown]
        public void TearDown() {
            UnityEngine.Object.DestroyImmediate(type);
        }

        // ==================== TESTS ===================
        [Test]
        public void Text_IsEmpty_WhenNoTextAssigned() {
            Interaction _interaction = new(type);

            Assert.AreEqual("", _interaction.Text);
        }

        [Test]
        public void Text_ReturnsFixedText() {
            Interaction _interaction = new(type, new FixedText("Open"));

            Assert.AreEqual("Open", _interaction.Text);
        }

        [Test]
        public void SetText_String_RaisesChanged_AndUpdatesText() {
            Interaction _interaction = new(type, new FixedText("Open"));
            int _changes = 0;
            _interaction.Changed += _ => _changes++;

            _interaction.SetText("Lock");

            Assert.AreEqual(1, _changes);
            Assert.AreEqual("Lock", _interaction.Text);
        }

        [Test]
        public void SetActive_False_RaisesChanged_AndBlocksExecute() {
            CountingResponse _response = new();
            Interaction _interaction = new(type, null, _response);
            int _changes = 0;
            _interaction.Changed += _ => _changes++;

            _interaction.SetActive(false);

            Assert.AreEqual(1, _changes);
            Assert.IsFalse(_interaction.IsActive);
            Assert.IsFalse(_interaction.Execute(null, null));
            Assert.AreEqual(0, _response.Calls);
        }

        [Test]
        public void Execute_CallsEveryResponse_AndSkipsNullEntries() {
            CountingResponse _first = new();
            CountingResponse _second = new();
            Interaction _interaction = new(type, null, _first, null, _second);

            bool _executed = _interaction.Execute(null, null);

            Assert.IsTrue(_executed);
            Assert.AreEqual(1, _first.Calls);
            Assert.AreEqual(1, _second.Calls);
        }

        [Test]
        public void Execute_WithNoResponses_ReturnsTrue() {
            Interaction _interaction = new(type);

            Assert.IsTrue(_interaction.Execute(null, null));
        }

        [Test]
        public void Execute_IsIgnored_WhileRunning_WhenIgnoreWhileRunning() {
            CountingResponse _response = new() { Running = true };
            Interaction _interaction = new(type, null, _response);

            Assert.IsTrue(_interaction.IgnoreWhileRunning);
            Assert.IsTrue(_interaction.IsRunning);
            Assert.IsFalse(_interaction.Execute(null, null));
            Assert.AreEqual(0, _response.Calls);
        }

        [Test]
        public void TextChange_IsForwarded() {
            ManualText _text = new();
            Interaction _interaction = new(type);
            _interaction.SetText(_text);
            int _changes = 0;
            _interaction.Changed += _ => _changes++;

            _text.Current = "b";
            _text.Raise();

            Assert.AreEqual(1, _changes);
            Assert.AreEqual("b", _interaction.Text);
        }
    }
}
