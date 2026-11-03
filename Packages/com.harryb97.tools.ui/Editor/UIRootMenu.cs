using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HarryTools.UI.Editor
{
    static class UIRootMenu
    {
        [MenuItem("GameObject/HarryTools/UI Root", false, 10)]
        static void CreateUIRoot(MenuCommand command)
        {
            bool hadEventSystem = Object.FindAnyObjectByType<EventSystem>();
            var root = UIRoot.Create();

            GameObjectUtility.SetParentAndAlign(root.gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create UI Root");

            if (!hadEventSystem && Object.FindAnyObjectByType<EventSystem>() is { } eventSystem)
                Undo.RegisterCreatedObjectUndo(eventSystem.gameObject, "Create EventSystem");

            Selection.activeGameObject = root.gameObject;
        }
    }
}
