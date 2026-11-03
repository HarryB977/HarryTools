using System.Collections;
using JetBrains.Annotations;
using UnityEngine;

namespace HarryTools.UI
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class MonoView : MonoBehaviour
    {
        [SerializeField] private bool _cullWindowsBehind = false;

        public CanvasGroup canvasGroup { get; private set; }

        public ViewStack parentStack { get; internal set; }

        public bool cullWindowsBehind => _cullWindowsBehind;

        public bool isTopMost => parentStack && parentStack.top == this;

        /// <summary>
        /// Returns true if this view is currently part of a ViewStack.
        /// </summary>
        public bool isInStack => parentStack;

        public Canvas canvas { get; private set; }

        /// <summary>
        /// Called when this view is pushed onto a ViewStack, before any enter transition plays.
        /// </summary>
        public virtual void OnPushed() { }

        /// <summary>
        /// Called when this view is popped from a ViewStack, before any exit transition plays.
        /// </summary>
        public virtual void OnPopped() { }

        public void Initialize(ViewStack parentStack)
        {
            this.parentStack = parentStack;
            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void UpdateOrder(int order)
        {
            canvas.sortingOrder = order;
        }

        public void MoveToBackground()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            OnBecomeBackground();
        }

        public void MoveToForeground()
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            OnBecomeForeground();
        }

        /// <summary>
        /// Called when this view becomes the top-most interactive view in the stack.
        /// </summary>
        protected virtual void OnBecomeForeground() { }

        /// <summary>
        /// Called when this view is no longer the top-most view due to another view being pushed on top.
        /// </summary>
        protected virtual void OnBecomeBackground() { }

        public IEnumerator EnterTransition() => OnEnterTransition();

        public IEnumerator ExitTransition() => OnExitTransition();

        /// <summary>
        /// Override to provide a transition animation when this view is pushed onto the stack.
        /// </summary>
        protected virtual IEnumerator OnEnterTransition() => null;

        /// <summary>
        /// Override to provide a transition animation when this view is popped from the stack.
        /// </summary>
        protected virtual IEnumerator OnExitTransition() => null;

        public IEnumerator CulledTransition() => OnCulledTransition();

        public IEnumerator UnculledTransition() => OnUnculledTransition();

        /// <summary>
        /// Override to provide a transition animation when this view is culled by a view above it.
        /// </summary>
        protected virtual IEnumerator OnCulledTransition() => null;

        /// <summary>
        /// Override to provide a transition animation when this view is restored after being culled.
        /// </summary>
        protected virtual IEnumerator OnUnculledTransition() => null;

        internal void DestroyMe()
        {
            Destroy(gameObject);
        }

        /// <summary>
        /// Pops this view from its parent stack. Can be used as a UnityEvent callback.
        /// </summary>
        [UsedImplicitly]
        public void PopMe()
        {
            if (parentStack)
                parentStack.Pop(this);
            else
                Debug.LogError("[MonoView] View is not part of a ViewStack.", this);
        }

        /// <summary>
        /// Pops this view from its parent stack. Can be used as a UnityEvent callback.
        /// </summary>
        [UsedImplicitly]
        public void CloseMe()
        {
            PopMe();
        }
    }
}
