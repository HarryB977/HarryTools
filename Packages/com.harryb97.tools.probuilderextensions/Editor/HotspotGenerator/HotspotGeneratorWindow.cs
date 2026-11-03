using System;
using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.HotspotUV;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.ProBuilderExtensions.Generator {
    /// <summary>Edits a <see cref="HotspotRecipe"/> with a live preview of the generated maps.</summary>
    public class HotspotGeneratorWindow : EditorWindow {
        private const int PreviewResolution = 512;
        private const double PreviewDelay = 0.25;
        private static readonly string[] MapNames = Enum.GetNames(typeof(HotspotMap));

        [SerializeField] private HotspotRecipe recipe;
        [SerializeField] private HotspotMap previewMap;
        [SerializeField] private bool showPatches = true;
        [SerializeField] private Vector2 scroll;

        [NonSerialized] private UnityEditor.Editor recipeEditor;
        [NonSerialized] private HotspotRenderer previewRenderer;
        [NonSerialized] private string previewError;
        [NonSerialized] private int lastDirtyCount = -1;
        [NonSerialized] private double renderAt = -1;

        [MenuItem("Tools/HarryTools/ProBuilder Extensions/Hotspot Texture Generator")]
        private static void OpenFromMenu() => Open(Selection.activeObject as HotspotRecipe);

        public static HotspotGeneratorWindow Open(HotspotRecipe recipe) {
            var window = GetWindow<HotspotGeneratorWindow>();
            window.titleContent = new GUIContent("Hotspot Texture Generator");
            window.minSize = new Vector2(640, 420);
            if (recipe != null) window.SetRecipe(recipe);
            window.Show();
            return window;
        }

        [OnOpenAsset(1)]
        public static bool OpenRecipe(int instanceID, int line) {
#pragma warning disable CS0618 // InstanceIDToObject is obsolete in 6000.3 but required for 6000.0 compatibility.
            if (EditorUtility.InstanceIDToObject(instanceID) is not HotspotRecipe asset) return false;
#pragma warning restore CS0618
            Open(asset);
            return true;
        }

        private void SetRecipe(HotspotRecipe value) {
            recipe = value;
            DestroyEditor();
            ScheduleRender(0);
        }

        private void OnEnable() {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += OnEditorUpdate;
            ScheduleRender(0);
        }

        private void OnDisable() {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            DisposePreview();
            DestroyEditor();
        }

        private void OnUndoRedo() => ScheduleRender(PreviewDelay);

        private void ScheduleRender(double delay) => renderAt = EditorApplication.timeSinceStartup + delay;

        private int DirtyCount() {
            if (recipe == null) return 0;
            var count = EditorUtility.GetDirtyCount(recipe);
            if (recipe.layout != null) count += EditorUtility.GetDirtyCount(recipe.layout);
            return count;
        }

        private void OnEditorUpdate() {
            var dirty = DirtyCount();
            if (dirty != lastDirtyCount) {
                lastDirtyCount = dirty;
                ScheduleRender(PreviewDelay);
            }
            if (renderAt >= 0 && EditorApplication.timeSinceStartup >= renderAt) {
                renderAt = -1;
                RefreshPreview();
                Repaint();
            }
        }

        private void RefreshPreview() {
            DisposePreview();
            previewError = null;
            if (recipe == null || recipe.layout == null) return;
            try {
                previewRenderer = new HotspotRenderer(recipe, PreviewResolution);
            } catch (Exception e) {
                previewError = e.Message;
            }
        }

        private void DisposePreview() {
            previewRenderer?.Dispose();
            previewRenderer = null;
        }

        private void DestroyEditor() {
            if (recipeEditor != null) DestroyImmediate(recipeEditor);
            recipeEditor = null;
        }

        private void OnGUI() {
            var leftWidth = Mathf.Max(300, position.width * 0.42f);
            GUILayout.BeginArea(new Rect(0, 0, leftWidth, position.height));
            DrawRecipe();
            GUILayout.EndArea();
            DrawPreview(new Rect(leftWidth, 0, position.width - leftWidth, position.height));
        }

        private void DrawRecipe() {
            EditorGUI.BeginChangeCheck();
            var newRecipe = (HotspotRecipe)EditorGUILayout.ObjectField("Recipe", recipe, typeof(HotspotRecipe), false);
            if (EditorGUI.EndChangeCheck()) SetRecipe(newRecipe);

            if (recipe == null) {
                EditorGUILayout.HelpBox("Select a Hotspot Recipe, or create one with Create > HarryTools > ProBuilder Extensions > Hotspot Recipe.", MessageType.Info);
                return;
            }

            foreach (var message in Problems())
                EditorGUILayout.HelpBox(message.text, message.type);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            UnityEditor.Editor.CreateCachedEditor(recipe, null, ref recipeEditor);
            EditorGUI.BeginChangeCheck();
            recipeEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) ScheduleRender(PreviewDelay);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUI.DisabledScope(recipe.layout == null)) {
                    if (GUILayout.Button("Generate", GUILayout.Height(28))) Generate();
                }
                using (new EditorGUI.DisabledScope(recipe.material == null && recipe.hotspotTexture == null)) {
                    if (GUILayout.Button("Ping Outputs", GUILayout.Height(28), GUILayout.Width(110))) PingOutputs();
                }
            }
        }

        private IEnumerable<(string text, MessageType type)> Problems() {
            if (recipe.layout == null) {
                yield return ("Assign a Hotspot Layout.", MessageType.Warning);
            } else {
                var errors = new List<string>();
                if (!recipe.layout.Validate(errors)) yield return (string.Join("\n", errors), MessageType.Error);
            }
            if (recipe.top.albedo == null) yield return ("The top layer has no albedo; it will be plain white.", MessageType.Warning);
            if (recipe.bottom.albedo == null) yield return ("The bottom layer has no albedo; it will be plain white.", MessageType.Warning);
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(recipe))) yield return ("Save the recipe as an asset to generate.", MessageType.Warning);
        }

        private void Generate() {
            try {
                EditorUtility.DisplayProgressBar("Hotspot Texture Generator", $"Generating {recipe.name}…", 0.5f);
                HotspotGenerator.Generate(recipe);
                Debug.Log($"Hotspot Generator: generated '{recipe.name}' in {HotspotGenerator.GetOutputFolder(recipe)}.");
            } catch (Exception e) {
                Debug.LogException(e);
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        private void PingOutputs() {
            Object target = recipe.material != null ? recipe.material : recipe.hotspotTexture;
            EditorGUIUtility.PingObject(target);
            Selection.activeObject = target;
        }

        private void DrawPreview(Rect area) {
            var toolbar = new Rect(area.x, area.y, area.width, EditorGUIUtility.singleLineHeight + 2);
            GUI.Box(toolbar, GUIContent.none, EditorStyles.toolbar);
            var toggleWidth = 64f;
            previewMap = (HotspotMap)GUI.Toolbar(new Rect(toolbar.x, toolbar.y, toolbar.width - toggleWidth, toolbar.height), (int)previewMap, MapNames, EditorStyles.toolbarButton);
            showPatches = GUI.Toggle(new Rect(toolbar.xMax - toggleWidth, toolbar.y, toggleWidth, toolbar.height), showPatches, "Patches", EditorStyles.toolbarButton);
            area.yMin = toolbar.yMax;

            if (previewError != null) {
                EditorGUI.HelpBox(new Rect(area.x + 8, area.y + 8, area.width - 16, 40), previewError, MessageType.Error);
                return;
            }
            if (previewRenderer == null || Event.current.type != EventType.Repaint) return;

            var size = (Vector2)previewRenderer.Size;
            var scale = Mathf.Min((area.width - 16) / size.x, (area.height - 16) / size.y);
            var rect = new Rect(0, 0, size.x * scale, size.y * scale) { center = area.center };
            EditorGUI.DrawPreviewTexture(rect, previewRenderer.Render(previewMap), null, ScaleMode.StretchToFill);

            if (showPatches && recipe != null && recipe.layout != null) DrawPatches(rect);
        }

        private void DrawPatches(Rect rect) {
            Handles.color = new Color(0f, 1f, 1f, 0.8f);
            foreach (var patch in recipe.layout.patches) {
                var r = patch.rect.Normalized();
                var p = new Rect(rect.x + r.x * rect.width, rect.y + r.y * rect.height, r.width * rect.width, r.height * rect.height);
                Handles.DrawPolyLine(p.TopLeft(), p.TopRight(), p.BottomRight(), p.BottomLeft(), p.TopLeft());
            }
        }
    }
}
