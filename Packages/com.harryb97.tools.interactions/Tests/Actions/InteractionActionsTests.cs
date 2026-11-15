using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.Interactions.Actions;
using Object = UnityEngine.Object;

namespace HarryTools.Interactions.Tests {
    public class InteractionActionsTests {
        // ==================== HELPERS ===================
        [Serializable]
        private sealed class RecordAction : IAction {
            public int Runs;
            public LogicContext Context;

            public UniTask Run(LogicContext _context, CancellationToken _token) {
                Runs++;
                Context = _context;
                return UniTask.CompletedTask;
            }
        }

        [Serializable]
        private sealed class DelayAction : IAction {
            public float Seconds;

            public UniTask Run(LogicContext _context, CancellationToken _token) => UniTask.Delay(TimeSpan.FromSeconds(Seconds), cancellationToken: _token);
        }

        private readonly List<Object> created = new();
        private InteractionType primary;
        private Interactor interactor;

        [SetUp]
        public void SetUp() {
            primary = Track(ScriptableObject.CreateInstance<InteractionType>());
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

        private Interactable CreateInteractable(params Interaction[] _interactions) {
            Interactable _interactable = Track(new GameObject("Interactable")).AddComponent<Interactable>();
            foreach (Interaction _interaction in _interactions) _interactable.AddInteraction(_interaction);
            return _interactable;
        }

        private static void RunInstant(IAction _action, Interactable _self) => _action.Run(LogicContext.From(_self), CancellationToken.None).Forget();

        // ==================== TESTS ===================
        [Test]
        public void ActionListResponse_RunsList_WithSourceAsSelf_AndInteractorAsTarget() {
            RecordAction _record = new();
            Interactable _interactable = CreateInteractable(new Interaction(primary, null, new ActionListResponse(new ActionList(_record))));
            interactor.SetFocus(_interactable);

            interactor.Interact(primary);

            Assert.AreEqual(1, _record.Runs);
            Assert.AreSame(_interactable.gameObject, _record.Context.Self);
            Assert.AreSame(interactor.gameObject, _record.Context.Target);
        }

        [UnityTest]
        public IEnumerator ActionListResponse_IsRunning_WhileWaiting() {
            ActionListResponse _response = new(new ActionList(new DelayAction { Seconds = 0.2f }));
            Interactable _interactable = CreateInteractable(new Interaction(primary, null, _response));

            _response.Respond(_interactable, interactor);
            Assert.IsTrue(_response.IsRunning);

            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(_response.IsRunning);
        }

        [UnityTest]
        public IEnumerator SecondInteract_IsIgnored_WhileListRuns() {
            RecordAction _record = new();
            Interactable _interactable = CreateInteractable(new Interaction(primary, null, new ActionListResponse(new ActionList(_record, new DelayAction { Seconds = 0.2f }))));
            interactor.SetFocus(_interactable);

            Assert.IsTrue(interactor.Interact(primary));
            Assert.IsFalse(interactor.Interact(primary));
            Assert.AreEqual(1, _record.Runs);

            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(interactor.Interact(primary));
            Assert.AreEqual(2, _record.Runs);
        }

        [Test]
        public void SetInteractionText_ChangesFocusText_AndRaisesFocusUpdated() {
            Interaction _interaction = new(primary, new FixedText("Lock"));
            Interactable _interactable = CreateInteractable(_interaction);
            interactor.SetFocus(_interactable);
            int _updates = 0;
            interactor.FocusUpdated += _ => _updates++;

            RunInstant(new SetInteractionTextAction(new TargetComponent<Interactable>(TargetSource.Self), primary, new FixedText("Unlock")), _interactable);

            Assert.AreEqual("Unlock", _interaction.Text);
            Assert.AreEqual(1, _updates);
        }

        [Test]
        public void SetInteractionActive_False_OnLastInteraction_DropsFocus() {
            Interactable _interactable = CreateInteractable(new Interaction(primary));
            interactor.SetFocus(_interactable);

            RunInstant(new SetInteractionActiveAction(new TargetComponent<Interactable>(TargetSource.Self), primary, false), _interactable);

            Assert.IsNull(interactor.Focus);
        }

        [Test]
        public void SetInteractionActive_True_ReactivatesInactiveInteraction() {
            Interaction _interaction = new(primary);
            _interaction.SetActive(false);
            Interactable _interactable = CreateInteractable(_interaction);

            RunInstant(new SetInteractionActiveAction(new TargetComponent<Interactable>(TargetSource.Self), primary, true), _interactable);

            Assert.IsTrue(_interaction.IsActive);
        }

        [Test]
        public void SetInteractableActive_False_DropsFocus() {
            Interactable _interactable = CreateInteractable(new Interaction(primary));
            interactor.SetFocus(_interactable);

            RunInstant(new SetInteractableActiveAction(new TargetComponent<Interactable>(TargetSource.Self), false), _interactable);

            Assert.IsFalse(_interactable.IsActive);
            Assert.IsNull(interactor.Focus);
        }
    }
}
