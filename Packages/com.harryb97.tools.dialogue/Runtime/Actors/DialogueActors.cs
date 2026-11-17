using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> The enabled dialogue actors of the loaded scenes </summary>
    public static class DialogueActors {
        // ==================== VARIABLES ===================
        #region Private Variables
        [ClearOnReload(AssignNewInstance = true)] private static List<DialogueActor> all = new List<DialogueActor>();
        #endregion

        #region Public Variables
        /// <summary> Enabled actors, in the order they were enabled </summary>
        public static IReadOnlyList<DialogueActor> All => all;
        #endregion

        // ==================== QUERIES ====================
        #region Queries
        /// <summary> The enabled actor of the character nearest to the position, or the first registered one when there is no position </summary>
        /// <returns> The actor, or null when the character has none or is null </returns>
        public static DialogueActor Find(DialogueCharacter _character, Vector3? _near) {
            if (_character == null) return null;

            DialogueActor _best = null;
            float _bestDistance = float.MaxValue;
            for (int _i = 0; _i < all.Count; _i++) {
                DialogueActor _actor = all[_i];
                if (_actor == null || _actor.Character != _character) continue;
                if (!_near.HasValue) return _actor;

                float _distance = (_actor.transform.position - _near.Value).sqrMagnitude;
                if (_best == null || _distance < _bestDistance) {
                    _best = _actor;
                    _bestDistance = _distance;
                }
            }
            return _best;
        }
        #endregion

        // ==================== REGISTRY ====================
        #region Registry
        internal static void Register(DialogueActor _actor) {
            if (!all.Contains(_actor)) all.Add(_actor);
        }

        internal static void Unregister(DialogueActor _actor) {
            all.Remove(_actor);
        }
        #endregion
    }
}
