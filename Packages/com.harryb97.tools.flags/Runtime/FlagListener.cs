using HarryTools.Actions;
using HarryTools.Conditions;
using UnityEngine;

namespace HarryTools.Flags
{
    /// <summary>
    /// Runs action lists when a flag has a value: on enable and after restores or resets (apply), and on live changes (change).
    /// </summary>
    [AddComponentMenu("HarryTools/Flags/Flag Listener")]
    public sealed class FlagListener : MonoBehaviour
    {
        // ==================== VARIABLES ====================

        [Tooltip("Flag to listen to.")]
        [FlagUsage(FlagUsageKind.Read)]
        [SerializeField] private FlagRef flag = new FlagRef();

        [Header("Apply (enable, restore, reset)")]
        [Tooltip("Runs when the object is enabled or the state is restored, if the flag is set. Also runs on a live change when On Change Set is empty.")]
        [SerializeField] private ActionList onApplySet = new ActionList();
        [Tooltip("Runs when the object is enabled or the state is restored, if the flag is cleared. Also runs on a live change when On Change Cleared is empty.")]
        [SerializeField] private ActionList onApplyCleared = new ActionList();

        [Header("Live change")]
        [Tooltip("Runs when the flag becomes set during play. If empty, On Apply Set runs instead.")]
        [SerializeField] private ActionList onChangeSet = new ActionList();
        [Tooltip("Runs when the flag becomes cleared during play. If empty, On Apply Cleared runs instead.")]
        [SerializeField] private ActionList onChangeCleared = new ActionList();

        private ActionHandle running;
        // Counts RunList calls, so a run started while another list was starting (re-entrancy) wins over the older one.
        private int runCount;

        // ==================== PROPERTIES ====================

        public FlagRef Flag => flag;

        // ==================== UNITY ====================

        private void OnEnable()
        {
            Flags.Subscribe(flag, HandleChanged);
            Apply(Flags.IsSet(flag, this));
        }

        private void OnDisable()
        {
            Flags.Unsubscribe(flag, HandleChanged);
            CancelRunning();
        }

        // ==================== PRIVATE ====================

        private void HandleChanged(FlagChange _change)
        {
            if (_change.Reason != FlagChangeReason.Live)
            {
                Apply(_change.NewValue);
                return;
            }

            ActionList _list = _change.NewValue ? onChangeSet : onChangeCleared;
            if (_list == null || _list.IsEmpty) Apply(_change.NewValue);
            else RunList(_list);
        }

        private void Apply(bool _value) => RunList(_value ? onApplySet : onApplyCleared);

        private void RunList(ActionList _list)
        {
            CancelRunning();
            if (_list == null || _list.IsEmpty) return;

            int _run = ++runCount;
            ActionHandle _handle = _list.Run(LogicContext.From(this), destroyCancellationToken);
            // An action changed this listener's flag or re-enabled the object while starting: the newer run owns `running`.
            if (_run != runCount)
            {
                _handle?.Cancel();
                return;
            }
            running = _handle;
            // An action may have disabled this object while starting, before the handle was stored.
            if (!isActiveAndEnabled) CancelRunning();
        }

        private void CancelRunning()
        {
            running?.Cancel();
            running = null;
        }

        // ==================== TESTS ====================

        internal void SetUpForTests(FlagRef _flag, ActionList _onApplySet, ActionList _onApplyCleared, ActionList _onChangeSet, ActionList _onChangeCleared)
        {
            flag = _flag;
            onApplySet = _onApplySet;
            onApplyCleared = _onApplyCleared;
            onChangeSet = _onChangeSet;
            onChangeCleared = _onChangeCleared;
        }
    }
}
