using System;
using NUnit.Framework;
using UnityEngine.Localization;
using HarryTools.Interactions.Localization;

namespace HarryTools.Interactions.Tests {
    public class LocalizedTextTests {
        [Test]
        public void Value_IsEmpty_ForEmptyLocalizedString() {
            LocalizedText _text = new(new LocalizedString());

            Assert.AreEqual("", _text.Value);
        }

        [Test]
        public void Value_IsEmpty_ForDefaultConstructed() {
            LocalizedText _text = new();

            Assert.AreEqual("", _text.Value);
        }

        [Test]
        public void Subscribing_AndUnsubscribing_DoesNotThrow_ForEmptyLocalizedString() {
            LocalizedText _text = new(new LocalizedString());
            Action _handler = () => { };

            Assert.DoesNotThrow(() => {
                _text.Changed += _handler;
                _text.Changed -= _handler;
            });
        }
    }
}
