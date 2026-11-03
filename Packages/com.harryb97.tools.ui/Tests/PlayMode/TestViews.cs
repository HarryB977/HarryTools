using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HarryTools.UI.Tests
{
    public class TestViewA : View
    {
        public int foregroundCount;
        public int backgroundCount;
        public int shownCount;
        public int hiddenCount;

        protected override void OnBecomeForeground() => foregroundCount++;
        protected override void OnBecomeBackground() => backgroundCount++;
        protected override void OnShown() => shownCount++;
        protected override void OnHidden() => hiddenCount++;
    }

    public class TestViewB : TestViewA { }

    public class TestViewC : TestViewA { }

    /// <summary> Crea canvas, capas y plantillas para los tests; lo destruye todo en Dispose </summary>
    public sealed class UITestFixture : IDisposable
    {
        private readonly List<Object> owned = new();

        public GameObject CreateCanvas(string _name = "Canvas")
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            owned.Add(go);
            return go;
        }

        public T CreateLayer<T>(Transform _parent, UILayerKind _kind = UILayerKind.Views, int _sortingOrderBase = 0) where T : ViewLayer
        {
            var go = new GameObject(_kind.ToString(), typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(_parent, false);
            var layer = go.AddComponent<T>();
            layer.Configure(_kind, _sortingOrderBase);
            go.SetActive(true);
            return layer;
        }

        public ViewLayer CreateLayer(UILayerKind _kind = UILayerKind.Views) => CreateLayer<ViewLayer>(CreateCanvas().transform, _kind);

        /// <summary> Plantilla de vista en escena (sin capa) con un hijo 'Content' de 200x100 </summary>
        public T CreateTemplate<T>(ViewLifetime _lifetime = ViewLifetime.Cached, IViewTransition _transition = null) where T : View
        {
            var go = new GameObject(typeof(T).Name + "Template", typeof(RectTransform));
            go.SetActive(false);
            var view = go.AddComponent<T>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(go.transform, false);
            content.sizeDelta = new Vector2(200, 100);
            view.Lifetime = _lifetime;
            view.Transition = _transition;
            go.SetActive(true);
            owned.Add(go);
            return view;
        }

        public void Dispose()
        {
            foreach (var view in Object.FindObjectsByType<View>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (view) Object.DestroyImmediate(view.gameObject);
            foreach (var o in owned)
                if (o) Object.DestroyImmediate(o);
            owned.Clear();
        }
    }
}
