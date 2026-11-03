using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HarryTools.UI
{
    /// <summary> Oculta la vista padre al hacer clic sobre este objeto (por ejemplo, un fondo) </summary>
    public class HideParentView : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Si es falso, el clic no oculta la vista.")]
        [SerializeField] private bool _canHide = true;
        [Tooltip("Si es verdadero, el clic que oculta la vista también llega a lo que haya detrás.")]
        [SerializeField] private bool _passThrough = false;

        private readonly List<RaycastResult> _results = new();

        public bool canHide
        {
            get => _canHide;
            set => _canHide = value;
        }

        /// <summary> Oculta la vista padre, si la hay y si 'canHide' es verdadero </summary>
        public void Hide()
        {
            if (!_canHide) return;

            var view = GetComponentInParent<View>();
            if (view)
                view.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_canHide)
                return;

            var view = GetComponentInParent<View>();

            Hide();

            if (_passThrough && EventSystem.current)
            {
                EventSystem.current.RaycastAll(eventData, _results);

                for (var i = 0; i < _results.Count; i++)
                {
                    var hit = _results[i].gameObject;

                    if (view && hit.transform.IsChildOf(view.transform))
                        continue;

                    ExecuteEvents.ExecuteHierarchy(hit, eventData, ExecuteEvents.pointerClickHandler);
                    break;
                }
            }
        }
    }
}
