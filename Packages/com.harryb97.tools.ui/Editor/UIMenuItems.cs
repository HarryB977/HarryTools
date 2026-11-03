using HarryTools.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HarryTools.UI.Editor
{
    static class UIMenuItems
    {
#if UNITY_6000_0_OR_NEWER
        const string UI_CREATE_PATH = "GameObject/UI (Canvas)/";
#else
        const string UI_CREATE_PATH = "GameObject/UI/";
#endif

        [MenuItem(UI_CREATE_PATH + "Rectangle Graphic", false, 2100)]
        static void CreateRectangle(MenuCommand menuCommand)
        {
            CreateUIElement<RectangleGraphic>("Rectangle", menuCommand);
        }

        [MenuItem(UI_CREATE_PATH + "Glow Graphic", false, 2101)]
        static void CreateGlow(MenuCommand menuCommand)
        {
            CreateUIElement<GlowGraphic>("Glow", menuCommand);
        }

        static void CreateUIElement<T>(string name, MenuCommand menuCommand) where T : Graphic
        {
            var parent = menuCommand.context as GameObject;

            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
#if UNITY_2022_1_OR_NEWER
                var canvas = Object.FindAnyObjectByType<Canvas>();
#else
                var canvas = Object.FindObjectOfType<Canvas>();
#endif
                if (canvas != null && canvas.gameObject.activeInHierarchy)
                    parent = canvas.gameObject;
                else
                    parent = CreateCanvas();
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            GameObjectUtility.SetParentAndAlign(go, parent);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 100f);

            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeGameObject = go;
        }

        static GameObject CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            EnsureEventSystem();

            return canvasGo;
        }

        static void EnsureEventSystem()
        {
#if UNITY_2022_1_OR_NEWER
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;
#else
            if (Object.FindObjectOfType<EventSystem>() != null)
                return;
#endif

            var esGo = new GameObject("EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));

            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
        }
    }
}
