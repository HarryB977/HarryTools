using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.UI
{
    public class ViewStack : MonoBehaviour, IPaletteProvider
    {
        [SerializeField] private Transform _parent;
        [SerializeField, HideInInspector, Obsolete] private ViewCollection _prefabs;
        [SerializeField] private List<ViewCollection> _prefabCollections;
        [SerializeField] private ColorPalette _colorPalette;
        [SerializeField] private MonoView _pushOnStart;
        [SerializeField] private int _orderOffset;

        private readonly List<MonoView> _stack = new();
        private readonly Dictionary<MonoView, Coroutine> _cullCoroutines = new();
        private readonly Dictionary<MonoView, Coroutine> _uncullCoroutines = new();

        public MonoView top => _stack.Count > 0 ? _stack[^1] : null;

        /// <summary>
        /// Number of views currently in the stack.
        /// </summary>
        public int count => _stack.Count;

        /// <summary>
        /// Read-only access to the views currently in the stack, ordered bottom to top.
        /// </summary>
        public IReadOnlyList<MonoView> views => _stack;

        /// <summary>
        /// Raised after a view has been instantiated, pushed onto the stack and initialized.
        /// </summary>
        public event Action<MonoView> onViewPushed;

        /// <summary>
        /// Raised after a view has been removed from the stack, before its exit transition completes.
        /// </summary>
        public event Action<MonoView> onViewPopped;

        public event Action onColorChange;

        private ColorPalette _subscribedPalette;

        public ColorPalette palette
        {
            get
            {
                return _colorPalette;
            }
            set
            {
                if (_colorPalette == value)
                    return;
                _colorPalette = value;
                SubscribeToPalette();
                onColorChange?.Invoke();
            }
        }

        private void Awake()
        {
            SubscribeToPalette();
        }

        private void SubscribeToPalette()
        {
            if (_subscribedPalette == _colorPalette)
                return;

            if (_subscribedPalette)
                _subscribedPalette.onChange -= OnColorPaletteDirty;

            _subscribedPalette = _colorPalette;

            if (_subscribedPalette)
                _subscribedPalette.onChange += OnColorPaletteDirty;
        }

        private void OnColorPaletteDirty()
        {
            onColorChange?.Invoke();
        }

        private void Start()
        {
            if (_pushOnStart)
                Push(_pushOnStart);
        }

#if UNITY_EDITOR
#pragma warning disable CS0612 // Type or member is obsolete
        private void OnValidate()
        {
            // this is entirely for backwards compatibility
            if (_prefabs)
            {
                _prefabCollections ??= new List<ViewCollection>();
                if (!_prefabCollections.Contains(_prefabs))
                    _prefabCollections.Add(_prefabs);
                _prefabs = null;
            }

            SubscribeToPalette();
            onColorChange?.Invoke();
        }
#pragma warning restore CS0612 // Type or member is obsolete
#endif

        private void Reset()
        {
            _parent = transform;
        }

        private void UpdateOrder(int fromIdx)
        {
            for (var i = fromIdx; i < _stack.Count; i++)
                _stack[i].UpdateOrder(i + _orderOffset);
        }

        private bool TryGet<T>(out T prefab) where T : MonoView
        {
            if (_prefabCollections == null)
            {
                prefab = null;
                return false;
            }

            for (int i = 0; i < _prefabCollections.Count; i++)
            {
                var prefabs = _prefabCollections[i];

                if (!prefabs)
                    continue;

                for (var j = 0; j < prefabs.views.Length; j++)
                {
                    var window = prefabs.views[j];
                    if (window is T typedWindow)
                    {
                        prefab = typedWindow;
                        return true;
                    }
                }
            }

            prefab = null;
            return false;
        }

        /// <summary>
        /// Searches the stack from top to bottom for the first instance of the specified type and returns it.
        /// If no instance of that type is found, returns null.
        /// </summary>
        public T FindWindow<T>() where T : MonoView
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i] is T instance)
                    return instance;
            }
            return null;
        }

        /// <summary>
        /// Adds a new window to the top of the stack using the provided prefab.
        /// The prefab must be a child of the WindowStack GameObject or its descendants.
        /// </summary>
        public T Push<T>(T prefab) where T : MonoView
        {
            if (!Application.isPlaying)
                return null;

            if (prefab == null)
            {
                Debug.LogError("[WindowStack] Provided prefab is null.", this);
                return null;
            }

            if (_stack.Count > 0)
                _stack[^1].MoveToBackground();

            return AddToStack(prefab);
        }

        private void UpdateVisibility()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                var view = _stack[i];

                bool wasCulled = !view.canvas.enabled;

                if (_cullCoroutines.TryGetValue(view, out var cullCoroutine))
                {
                    StopCoroutine(cullCoroutine);
                    _cullCoroutines.Remove(view);
                    wasCulled = true;
                }

                if (wasCulled)
                {
                    view.canvas.enabled = true;

                    var enterTransition = view.UnculledTransition();
                    if (enterTransition != null)
                    {
                        var coroutine = StartCoroutine(RunUncullTransition(view, enterTransition));
                        _uncullCoroutines[view] = coroutine;
                    }
                }

                if (view.cullWindowsBehind)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        StopUncullTransition(_stack[j]);
                        _stack[j].canvas.enabled = false;
                    }
                    break;
                }
            }
        }

        private void StopUncullTransition(MonoView view)
        {
            if (_uncullCoroutines.TryGetValue(view, out var coroutine))
            {
                StopCoroutine(coroutine);
                _uncullCoroutines.Remove(view);
            }
        }

        private void StopViewCoroutines(MonoView view)
        {
            if (_cullCoroutines.TryGetValue(view, out var coroutine))
            {
                StopCoroutine(coroutine);
                _cullCoroutines.Remove(view);
            }
            StopUncullTransition(view);
        }

        private void ApplyCulling(bool instant)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].cullWindowsBehind)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var view = _stack[j];

                        if (!view.canvas.enabled || _cullCoroutines.ContainsKey(view))
                            continue;

                        StopUncullTransition(view);

                        var transition = view.CulledTransition();
                        if (transition != null)
                        {
                            _cullCoroutines[view] = StartCoroutine(RunCullTransition(view, transition));
                            continue;
                        }

                        if (instant)
                            view.canvas.enabled = false;
                    }
                    break;
                }
            }
        }

        private IEnumerator RunCullTransition(MonoView view, IEnumerator transition)
        {
            yield return transition;
            _cullCoroutines.Remove(view);
            if (view) view.canvas.enabled = false;
        }

        private IEnumerator RunUncullTransition(MonoView view, IEnumerator transition)
        {
            yield return transition;
            _uncullCoroutines.Remove(view);
        }

        /// <summary>
        /// Adds a new window of the specified type to the top of the stack.
        /// The prefab must be included in the WindowPrefabs collection.
        /// If no prefab of that type is found, logs an error and does nothing.
        /// </summary>
        public T Push<T>() where T : MonoView
        {
            if (!TryGet<T>(out var prefab))
            {
                Debug.LogError($"[WindowStack] No window prefab of type `{typeof(T)}` found in WindowPrefabs.", this);
                return null;
            }

            return (T)Push(prefab);
        }

        /// <summary>
        /// Removes the top window from the stack. If the stack is empty, does nothing.
        /// </summary>
        public void Pop()
        {
            if (!Application.isPlaying)
                return;

            if (_stack.Count == 0)
            {
                Debug.LogError("[WindowStack] No windows to pop.", this);
                return;
            }

            RemoveTop();

            if (_stack.Count > 0)
            {
                var newTopIdx = _stack.Count - 1;
                var newTop = _stack[newTopIdx];
                if (newTop)
                {
                    newTop.transform.SetAsLastSibling();
                    newTop.MoveToForeground();
                    newTop.UpdateOrder(newTopIdx + _orderOffset);
                }
            }
            UpdateVisibility();
        }

        /// <summary>
        /// Removes all windows from the stack. Windows are removed in order, so exit transitions will play correctly.
        /// </summary>
        public void Clear()
        {
            bool canAnimate = Application.isPlaying && isActiveAndEnabled;

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                var view = _stack[i];
                if (!view)
                    continue;

                StopViewCoroutines(view);
                view.OnPopped();
                view.parentStack = null;
                onViewPopped?.Invoke(view);

                var transition = canAnimate ? view.ExitTransition() : null;
                if (transition != null)
                {
                    view.canvasGroup.interactable = false;
                    view.canvasGroup.blocksRaycasts = false;
                    StartCoroutine(RunExitTransition(view, transition));
                }
                else
                {
                    view.DestroyMe();
                }
            }
            _stack.Clear();
        }

        /// <summary>
        /// Removes the specified instance from the stack, regardless of its position. If the instance is not in the stack, does nothing.
        /// </summary>
        /// <param name="instance"></param>
        public void Pop(MonoView instance)
        {
            if (!Application.isPlaying)
                return;

            int idx = _stack.IndexOf(instance);

            if (idx == -1)
            {
                Debug.LogError("[WindowStack] The provided window instance is not in the stack.", this);
                return;
            }

            // If it's the top window, use the regular Pop method
            if (idx == _stack.Count - 1)
            {
                Pop();
                return;
            }

            RemoveFromStack(idx);

            UpdateOrder(idx);
            UpdateVisibility();
        }

        /// <summary>
        /// Replaces the top window with a new window instantiated from the provided prefab.
        /// The old top plays its exit transition while the new one plays its enter transition.
        /// Views below are not affected (no unnecessary uncull/re-cull).
        /// </summary>
        public T Replace<T>(T prefab) where T : MonoView
        {
            if (!Application.isPlaying)
                return null;

            if (prefab == null)
            {
                Debug.LogError("[WindowStack] Provided prefab is null.", this);
                return null;
            }

            if (_stack.Count > 0)
                RemoveTop();

            return AddToStack(prefab);
        }

        /// <summary>
        /// Replaces the top window with a new window of the specified type.
        /// The old top plays its exit transition while the new one plays its enter transition.
        /// Views below are not affected (no unnecessary uncull/re-cull).
        /// </summary>
        public T Replace<T>() where T : MonoView
        {
            if (!TryGet<T>(out var prefab))
            {
                Debug.LogError($"[WindowStack] No window prefab of type `{typeof(T)}` found in WindowPrefabs.", this);
                return null;
            }

            return (T)Replace(prefab);
        }

        /// <summary>
        /// Replaces the specified instance in the stack with a new window instantiated from the provided prefab.
        /// The old instance plays its exit transition while the new one plays its enter transition.
        /// The new window takes the same position in the stack as the old one.
        /// </summary>
        public T Replace<T>(MonoView instance, T prefab) where T : MonoView
        {
            if (!Application.isPlaying)
                return null;

            if (prefab == null)
            {
                Debug.LogError("[WindowStack] Provided prefab is null.", this);
                return null;
            }

            int idx = _stack.IndexOf(instance);

            if (idx == -1)
            {
                Debug.LogError("[WindowStack] The provided window instance is not in the stack.", this);
                return null;
            }

            // If it's the top window, use the regular Replace method
            if (idx == _stack.Count - 1)
                return Replace(prefab);

            // Remove the instance at its position
            RemoveFromStack(idx);

            // Insert new instance at the same position
            return InsertIntoStack(prefab, idx);
        }

        /// <summary>
        /// Replaces the specified instance in the stack with a new window of the specified type.
        /// The old instance plays its exit transition while the new one plays its enter transition.
        /// The new window takes the same position in the stack as the old one.
        /// </summary>
        public T Replace<T>(MonoView instance) where T : MonoView
        {
            if (!TryGet<T>(out var prefab))
            {
                Debug.LogError($"[WindowStack] No window prefab of type `{typeof(T)}` found in WindowPrefabs.", this);
                return null;
            }

            return (T)Replace(instance, prefab);
        }

        /// <summary>
        /// Replaces the specified instance in the stack with a new window instantiated from the provided prefab.
        /// If the instance is no longer part of the stack, pushes the new window instead.
        /// </summary>
        public T ReplaceOrPush<T>(MonoView instance, T prefab) where T : MonoView
        {
            if (!Application.isPlaying)
                return null;

            if (prefab == null)
            {
                Debug.LogError("[WindowStack] Provided prefab is null.", this);
                return null;
            }

            int idx = _stack.IndexOf(instance);

            if (idx == -1)
                return Push(prefab);

            return Replace(instance, prefab);
        }

        /// <summary>
        /// Replaces the specified instance in the stack with a new window of the specified type.
        /// If the instance is no longer part of the stack, pushes the new window instead.
        /// </summary>
        // `T` is the type to create, not the type being replaced - they are usually different.
        public T ReplaceOrPush<T>(MonoView instance) where T : MonoView
        {
            if (!TryGet<T>(out var prefab))
            {
                Debug.LogError($"[WindowStack] No window prefab of type `{typeof(T)}` found in WindowPrefabs.", this);
                return null;
            }

            return ReplaceOrPush(instance, prefab);
        }

        /// <summary>
        /// Moves the specified instance to the top of the stack. If the instance is not in the stack, does nothing.
        /// </summary>
        /// <param name="instance"></param>
        public bool MoveToTop(MonoView instance)
        {
            int idx = _stack.IndexOf(instance);
            if (idx == -1)
            {
                Debug.LogError("[WindowStack] The provided window instance is not in the stack.", this);
                return false;
            }

            if (idx == _stack.Count - 1)
                return false;

            _stack.RemoveAt(idx);
            _stack.Add(instance);
            instance.transform.SetAsLastSibling();
            UpdateOrder(idx);
            UpdateVisibility();
            return true;
        }

        public bool TryMoveToTop(MonoView instance)
        {
            int idx = _stack.IndexOf(instance);
            if (idx == -1)
                return false;

            if (idx == _stack.Count - 1)
                return false;

            _stack.RemoveAt(idx);
            _stack.Add(instance);
            instance.transform.SetAsLastSibling();
            UpdateOrder(idx);
            UpdateVisibility();
            return true;
        }

        /// <summary>
        /// Moves the first instance of the specified type to the top of the stack. If no instance of that type is found, does nothing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public bool MoveToTop<T>() where T : MonoView
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i] is T instance)
                    return MoveToTop(instance);
            }

            return false;
        }

        /// <summary>
        /// Moves the first instance of the specified type to the top of the stack. If no instance of that type is found, does nothing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public bool TryMoveToTop<T>() where T : MonoView
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i] is T instance)
                    return TryMoveToTop(instance);
            }

            return false;
        }

        /// <summary>
        /// Returns true if the stack contains at least one instance of the specified type, false otherwise.
        /// </summary>
        public bool Contains<T>() where T : MonoView
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i] is T)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the first instance of the specified type in the stack, or null if no instance of that type is found.
        /// </summary>
        public T GetFirstView<T>() where T : MonoView
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i] is T view)
                {
                    return view;
                }
            }

            return null;
        }

        private void RemoveTop()
        {
            RemoveFromStack(_stack.Count - 1);
        }

        private void RemoveFromStack(int idx)
        {
            var view = _stack[idx];
            _stack.RemoveAt(idx);
            StopViewCoroutines(view);
            view.OnPopped();
            view.parentStack = null;
            onViewPopped?.Invoke(view);

            var transition = view.ExitTransition();
            if (transition != null)
            {
                view.canvasGroup.interactable = false;
                view.canvasGroup.blocksRaycasts = false;
                StartCoroutine(RunExitTransition(view, transition));
            }
            else
            {
                view.DestroyMe();
            }
        }

        private T AddToStack<T>(T prefab) where T : MonoView
        {
            return InsertIntoStack(prefab, _stack.Count);
        }

        private T InsertIntoStack<T>(T prefab, int idx) where T : MonoView
        {
            var instance = Instantiate(prefab, _parent);
            _stack.Insert(idx, instance);
            instance.Initialize(this);
            UpdateOrder(idx);
            instance.OnPushed();
            onViewPushed?.Invoke(instance);

            var transition = instance.EnterTransition();
            if (transition != null)
            {
                instance.canvasGroup.interactable = false;
                instance.canvasGroup.blocksRaycasts = false;
                StartCoroutine(RunEnterTransition(instance, transition));
                ApplyCulling(false);
            }
            else
            {
                ApplyCulling(true);
            }

            return instance;
        }

        private IEnumerator RunEnterTransition(MonoView view, IEnumerator transition)
        {
            yield return transition;
            if (view)
            {
                view.canvasGroup.interactable = true;
                view.canvasGroup.blocksRaycasts = true;
            }
            ApplyCulling(true);
        }

        private static IEnumerator RunExitTransition(MonoView view, IEnumerator transition)
        {
            yield return transition;
            if (view) view.DestroyMe();
        }

        private void OnDestroy()
        {
            if (_subscribedPalette)
            {
                _subscribedPalette.onChange -= OnColorPaletteDirty;
                _subscribedPalette = null;
            }
            Clear();
        }
    }
}
