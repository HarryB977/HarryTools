using System.Collections.Generic;
using System.Linq;
using HarryTools.UI;
using UnityEditor;
using UnityEngine;

namespace HarryTools.UI.Editor
{
    [CustomEditor(typeof(ViewCollection))]
    public class ViewCollectionEditor : UnityEditor.Editor
    {
        private SerializedProperty _autoGenerate;
        private SerializedProperty _folders;
        private SerializedProperty _views;

        private void OnEnable()
        {
            _autoGenerate = serializedObject.FindProperty("autoGenerate");
            _folders = serializedObject.FindProperty("folders");
            _views = serializedObject.FindProperty("views");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_autoGenerate);
            EditorGUILayout.Space(4);

            DrawFoldersArray();
            EditorGUILayout.Space(4);

            if (_autoGenerate.boolValue)
            {
                if (GUILayout.Button("Refresh Views"))
                {
                    serializedObject.ApplyModifiedProperties();
                    ViewCollectionUtility.RefreshViews((ViewCollection)target);
                    serializedObject.Update();
                }

                EditorGUILayout.Space(4);

                using (new EditorGUI.DisabledScope(true))
                    DrawViewsArray();
            }
            else
            {
                DrawViewsArray();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFoldersArray()
        {
            EditorGUILayout.LabelField("Search Folders", EditorStyles.boldLabel);

            int size = _folders.arraySize;

            for (int i = 0; i < size; i++)
            {
                var element = _folders.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginHorizontal();

                var newObj = EditorGUILayout.ObjectField(
                    element.objectReferenceValue,
                    typeof(DefaultAsset),
                    false
                );

                if (newObj != element.objectReferenceValue)
                {
                    if (newObj == null || IsFolder(newObj))
                        element.objectReferenceValue = newObj;
                    else
                        Debug.LogWarning("Only folders can be assigned to the search folders list.");
                }

                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    _folders.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Folder"))
            {
                _folders.InsertArrayElementAtIndex(size);
                _folders.GetArrayElementAtIndex(size).objectReferenceValue = null;
            }

            HandleFolderDragAndDrop();
        }

        private void HandleFolderDragAndDrop()
        {
            var dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
            GUI.Box(dropArea, "Drag & drop folders here", style);

            var evt = Event.current;

            if (!dropArea.Contains(evt.mousePosition))
                return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                {
                    bool anyFolder = DragAndDrop.objectReferences.Any(IsFolder);
                    DragAndDrop.visualMode = anyFolder
                        ? DragAndDropVisualMode.Copy
                        : DragAndDropVisualMode.Rejected;
                    evt.Use();
                    break;
                }
                case EventType.DragPerform:
                {
                    DragAndDrop.AcceptDrag();

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (!IsFolder(obj))
                            continue;

                        if (AlreadyContainsFolder(obj))
                            continue;

                        int index = _folders.arraySize;
                        _folders.InsertArrayElementAtIndex(index);
                        _folders.GetArrayElementAtIndex(index).objectReferenceValue = obj;
                    }

                    evt.Use();
                    break;
                }
            }
        }

        private bool AlreadyContainsFolder(Object obj)
        {
            for (int i = 0; i < _folders.arraySize; i++)
            {
                if (_folders.GetArrayElementAtIndex(i).objectReferenceValue == obj)
                    return true;
            }
            return false;
        }

        private void DrawViewsArray()
        {
            EditorGUILayout.LabelField($"Views ({_views.arraySize})", EditorStyles.boldLabel);

            for (int i = 0; i < _views.arraySize; i++)
            {
                var element = _views.GetArrayElementAtIndex(i);
                EditorGUILayout.ObjectField(element.objectReferenceValue, typeof(MonoView), false);
            }
        }

        private static bool IsFolder(Object obj)
        {
            if (obj == null) return false;
            string path = AssetDatabase.GetAssetPath(obj);
            return AssetDatabase.IsValidFolder(path);
        }
    }

    public static class ViewCollectionUtility
    {
        public static List<MonoView> FindViewsInFolders(Object[] folders)
        {
            var folderPaths = new List<string>();

            if (folders == null)
                return new List<MonoView>();

            foreach (var obj in folders)
            {
                if (obj == null) continue;
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                    folderPaths.Add(path);
            }

            if (folderPaths.Count == 0)
                return new List<MonoView>();

            var guids = AssetDatabase.FindAssets("t:Prefab", folderPaths.ToArray());
            var monoViews = new List<MonoView>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var view = prefab.GetComponent<MonoView>();
                if (view != null)
                    monoViews.Add(view);
            }

            monoViews.Sort((a, b) =>
                string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            return monoViews;
        }

        public static void RefreshViews(ViewCollection collection)
        {
            var views = FindViewsInFolders(collection.folders);
            collection.views = views.ToArray();
            EditorUtility.SetDirty(collection);
        }

        public static bool IsUnderWatchedFolders(ViewCollection collection, string assetPath)
        {
            if (collection.folders == null)
                return false;

            foreach (var folder in collection.folders)
            {
                if (folder == null) continue;
                string folderPath = AssetDatabase.GetAssetPath(folder);
                if (assetPath.StartsWith(folderPath + "/"))
                    return true;
            }

            return false;
        }
    }

    public class ViewCollectionPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var allPaths = importedAssets
                .Concat(deletedAssets)
                .Concat(movedAssets)
                .Concat(movedFromAssetPaths);

            bool anyPrefab = allPaths.Any(p => p.EndsWith(".prefab"));

            if (!anyPrefab)
                return;

            var guids = AssetDatabase.FindAssets("t:ViewCollection");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var collection = AssetDatabase.LoadAssetAtPath<ViewCollection>(path);

                if (collection == null || !collection.autoGenerate)
                    continue;

                bool relevant = allPaths.Any(p =>
                    p.EndsWith(".prefab") &&
                    ViewCollectionUtility.IsUnderWatchedFolders(collection, p));

                if (!relevant)
                    continue;

                ViewCollectionUtility.RefreshViews(collection);
            }
        }
    }
}
