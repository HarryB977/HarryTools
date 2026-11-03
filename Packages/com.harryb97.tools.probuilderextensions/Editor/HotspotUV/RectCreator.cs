using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;

namespace HarryTools.ProBuilderExtensions.HotspotUV.Editor {
    /// <summary>Draws and edits the patches of a Hotspot Layout, or the pixel rects of a Hotspot Texture without one.</summary>
    public class RectCreator : EditorWindow {

        [MenuItem("Tools/HarryTools/ProBuilder Extensions/Hotspot Rect Creator")]
        private static void OpenFromMenu() => ShowWindow();

        private static RectCreator ShowWindow() {
            var window = GetWindow<RectCreator>();
            window.titleContent = new GUIContent("Hotspot Rect Creator");
            window.Show();
            return window;
        }

        public static RectCreator Open(UnityEngine.Object asset) {
            var window = ShowWindow();
            window.SetTarget(asset);
            return window;
        }

        [OnOpenAsset(1)]
        public static bool OpenHotspotAsset(int instanceID, int line) {
#pragma warning disable CS0618 // InstanceIDToObject is obsolete in 6000.3 but required for 6000.0 compatibility.
            var asset = EditorUtility.InstanceIDToObject(instanceID);
#pragma warning restore CS0618
            if (asset is HotspotTexture || asset is HotspotLayout) {
                Open(asset);
                return true;
            }
            return false;
        }

        private static readonly int[] GridSizes = { 2, 4, 8, 16, 32, 64, 128, 256, 512 };

        // Serialized so the window keeps its target after a domain reload.
        [SerializeField] private HotspotTexture editingHotspotTexture;
        [SerializeField] private HotspotLayout editingLayout;
        [SerializeField] private Texture previewTexture;
        [SerializeField] private int selected = -1;
        [SerializeField] private int gridLevel = 4;
        [SerializeField] private float zoomScale = 1.0f;
        [SerializeField] private Vector2 _zoomCoordsOrigin = new Vector2(-20, -20);

        [NonSerialized] private Rect toolWindowRect = new Rect(10, 0, 300f, 0f);
        [NonSerialized] private Rect _zoomArea;
        [NonSerialized] private bool isDragging;
        [NonSerialized] private Vector2 dragStart;

        private int GridStep => GridSizes[Mathf.Clamp(gridLevel, 0, GridSizes.Length - 1)];

        /// <summary>The layout being edited: the asset itself, or the layout of the Hotspot Texture.</summary>
        private HotspotLayout Layout => editingLayout != null ? editingLayout : editingHotspotTexture != null ? editingHotspotTexture.layout : null;

        private UnityEngine.Object UndoTarget => Layout != null ? Layout : editingHotspotTexture;

        private bool HasTarget => UndoTarget != null;

        private Texture BackgroundTexture {
            get {
                if (editingHotspotTexture != null && editingHotspotTexture.target != null) return editingHotspotTexture.target;
                return previewTexture;
            }
        }

        private Vector2 TextureSize {
            get {
                if (editingHotspotTexture != null && editingHotspotTexture.target != null)
                    return new Vector2(editingHotspotTexture.target.width, editingHotspotTexture.target.height);
                if (Layout != null) return Layout.GetTextureSize(Layout.recommendedResolution);
                return new Vector2(512, 512);
            }
        }

        private int Count => Layout != null ? Layout.patches.Count : editingHotspotTexture != null ? editingHotspotTexture.rects.Count : 0;

        private Rect GetPixelRect(int index) =>
            Layout != null ? Layout.GetPixelRect(index, TextureSize) : editingHotspotTexture.rects[index].Normalized();

        private void AddPixelRect(Rect rect) {
            Undo.RecordObject(UndoTarget, "Add Hotspot Rect");
            if (Layout != null) {
                var size = TextureSize;
                Layout.patches.Add(HotspotPatch.Default(new Rect(rect.x / size.x, rect.y / size.y, rect.width / size.x, rect.height / size.y)));
            } else {
                editingHotspotTexture.rects.Add(rect);
            }
            selected = Count - 1;
            EditorUtility.SetDirty(UndoTarget);
        }

        private void RemoveAt(int index) {
            Undo.RecordObject(UndoTarget, "Delete Hotspot Rect");
            if (Layout != null) Layout.patches.RemoveAt(index);
            else editingHotspotTexture.rects.RemoveAt(index);
            selected = -1;
            EditorUtility.SetDirty(UndoTarget);
        }

        private void SetTarget(UnityEngine.Object target) {
            editingHotspotTexture = target as HotspotTexture;
            editingLayout = target as HotspotLayout;
            isDragging = false;
            selected = -1;

            // Pick a grid that splits the texture in ~16 cells.
            var size = Mathf.Min(TextureSize.x, TextureSize.y);
            gridLevel = 0;
            while (gridLevel < GridSizes.Length - 1 && GridSizes[gridLevel] * 16 < size) gridLevel++;
            Repaint();
        }

        private void OnEnable() {
            Undo.undoRedoPerformed += Repaint;
            wantsMouseMove = true;
        }
        private void OnDisable() {
            Undo.undoRedoPerformed -= Repaint;
        }

        private void OnGUI() {
            _zoomArea = new Rect(0f, 0, position.width, position.height);
            var mouseOverToolWindow = toolWindowRect.Contains(Event.current.mousePosition);

            EditorZoomArea.Begin(zoomScale, _zoomArea);
            var textureSize = TextureSize;
            var drawRect = new Rect(-_zoomCoordsOrigin, textureSize);
            if (BackgroundTexture != null)
                EditorGUI.DrawPreviewTexture(drawRect, BackgroundTexture, null, ScaleMode.StretchToFill, 0);
            DrawGrid(drawRect);
            HandleRects(mouseOverToolWindow);
            EditorZoomArea.End();

            HandleZoom();
            ToolWindow();
        }

        private void DrawGrid(Rect textureRect) {
            var step = GridStep;
            using (new HandlesColorScope(new Color(1, 1, 1, 0.35f))) {
                for (float x = 0; x < textureRect.width; x += step)
                    Handles.DrawLine(new Vector2(textureRect.x + x, textureRect.y), new Vector2(textureRect.x + x, textureRect.yMax));
                for (float y = 0; y < textureRect.height; y += step)
                    Handles.DrawLine(new Vector2(textureRect.x, textureRect.y + y), new Vector2(textureRect.xMax, textureRect.y + y));
            }
            using (new HandlesColorScope(Color.white))
                DrawRectOutline(textureRect);
        }

        private void ToolWindow() {
            toolWindowRect.y = position.height - toolWindowRect.height - 10;
            BeginWindows();
            toolWindowRect = GUILayout.Window(32, toolWindowRect, DrawWindow, "Tool");
            EndWindows();
        }

        private Vector2 ConvertScreenCoordsToZoomCoords(Vector2 screenCoords) {
            return (screenCoords - _zoomArea.TopLeft()) / zoomScale + _zoomCoordsOrigin;
        }

        struct HandlesColorScope : IDisposable {
            public Color previousColor;

            public HandlesColorScope(Color color) {
                this.previousColor = Handles.color;
                Handles.color = color;
            }

            public void Dispose() {
                Handles.color = this.previousColor;
            }
        }

        private static void DrawRectOutline(Rect rect) {
            Handles.DrawLine(rect.TopLeft(), rect.TopRight());
            Handles.DrawLine(rect.TopRight(), rect.BottomRight());
            Handles.DrawLine(rect.BottomRight(), rect.BottomLeft());
            Handles.DrawLine(rect.BottomLeft(), rect.TopLeft());
        }

        private static Color RectColor(int index) {
            var c = Color.HSVToRGB(Mathf.Repeat(index * 0.618034f, 1f), 0.7f, 1f);
            c.a = 0.3f;
            return c;
        }

        private static string Tags(HotspotPatch patch) {
            var tags = string.Empty;
            if (patch.allowRotation) tags += "R";
            if (patch.allowMirrorX) tags += "X";
            if (patch.allowMirrorY) tags += "Y";
            if (patch.allowTiling) tags += "T";
            return tags;
        }

        /// <summary>Mouse position (already in zoom space) converted to texture pixels, snapped and clamped.</summary>
        private Vector2 SnappedTexturePosition(Vector2 mousePosition) {
            var p = mousePosition + _zoomCoordsOrigin;
            var size = TextureSize;
            var step = GridStep;
            p.x = Mathf.Clamp(Mathf.Round(p.x / step) * step, 0, size.x);
            p.y = Mathf.Clamp(Mathf.Round(p.y / step) * step, 0, size.y);
            return p;
        }

        private int RectUnderMouse(Vector2 mousePosition) {
            var p = mousePosition + _zoomCoordsOrigin;
            // Last drawn is on top.
            for (int i = Count - 1; i >= 0; i--)
                if (GetPixelRect(i).Contains(p)) return i;
            return -1;
        }

        private void HandleRects(bool mouseOverToolWindow) {
            if (!HasTarget) return;

            var e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            var hovered = mouseOverToolWindow || isDragging ? -1 : RectUnderMouse(e.mousePosition);

            if (e.type == EventType.Repaint) {
                var label = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.white } };
                for (int i = 0; i < Count; i++) {
                    var drawRect = GetPixelRect(i);
                    drawRect.position -= _zoomCoordsOrigin;
                    var c = RectColor(i);
                    if (i == hovered || i == selected) c.a = 0.55f;
                    EditorGUI.DrawRect(drawRect, c);
                    using (new HandlesColorScope(i == selected ? Color.cyan : i == hovered ? Color.yellow : Color.green))
                        DrawRectOutline(drawRect);
                    if (Layout != null)
                        GUI.Label(new Rect(drawRect.x + 2, drawRect.y + 1, 60, 16), Tags(Layout.patches[i]), label);
                }

                if (isDragging) {
                    var end = SnappedTexturePosition(e.mousePosition);
                    var preview = Rect.MinMaxRect(
                        Mathf.Min(dragStart.x, end.x), Mathf.Min(dragStart.y, end.y),
                        Mathf.Max(dragStart.x, end.x), Mathf.Max(dragStart.y, end.y));
                    preview.position -= _zoomCoordsOrigin;
                    using (new HandlesColorScope(Color.cyan))
                        DrawRectOutline(preview);
                }

                if (!mouseOverToolWindow) {
                    using (new HandlesColorScope(new Color(0, 1, 1, 0.5f)))
                        Handles.DrawSolidDisc(SnappedTexturePosition(e.mousePosition) - _zoomCoordsOrigin, Vector3.forward, 5 / zoomScale);
                }
            }

            switch (e.GetTypeForControl(controlId)) {
                case EventType.MouseDown:
                    if (mouseOverToolWindow) break;
                    if (e.button == 0) {
                        isDragging = true;
                        dragStart = SnappedTexturePosition(e.mousePosition);
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    } else if (e.button == 1 && hovered >= 0) {
                        RemoveAt(hovered);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId) {
                        Repaint();
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId && e.button == 0) {
                        GUIUtility.hotControl = 0;
                        isDragging = false;

                        var end = SnappedTexturePosition(e.mousePosition);
                        var rect = Rect.MinMaxRect(
                            Mathf.Min(dragStart.x, end.x), Mathf.Min(dragStart.y, end.y),
                            Mathf.Max(dragStart.x, end.x), Mathf.Max(dragStart.y, end.y));

                        // A click without dragging selects the patch under the mouse.
                        if (rect.width > 0 && rect.height > 0) AddPixelRect(rect);
                        else selected = RectUnderMouse(e.mousePosition);
                        e.Use();
                    }
                    break;
                case EventType.MouseMove:
                    Repaint();
                    break;
            }
        }

        private void HandleZoom() {
            var e = Event.current;
            switch (e.type) {
                case EventType.ScrollWheel:
                    Vector2 zoomCoordsMousePos = ConvertScreenCoordsToZoomCoords(e.mousePosition);
                    float oldZoom = zoomScale;
                    zoomScale = Mathf.Clamp(zoomScale * (1f - e.delta.y * 0.05f), 1 / 25f, 10f);
                    _zoomCoordsOrigin += (zoomCoordsMousePos - _zoomCoordsOrigin) - (oldZoom / zoomScale) * (zoomCoordsMousePos - _zoomCoordsOrigin);
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (e.button == 2) {
                        _zoomCoordsOrigin -= e.delta / zoomScale;
                        e.Use();
                    }
                    break;
            }
        }

        private void FrameTexture() {
            var size = TextureSize;
            var available = new Vector2(position.width - 40, position.height - 60);
            zoomScale = Mathf.Clamp(Mathf.Min(available.x / size.x, available.y / size.y), 1 / 25f, 10f);
            _zoomCoordsOrigin = new Vector2(-20, -20) / zoomScale;
        }

        private void DrawWindow(int id) {
            EditorGUI.BeginChangeCheck();
            var current = editingLayout != null ? (UnityEngine.Object)editingLayout : editingHotspotTexture;
            var newTarget = EditorGUILayout.ObjectField("Asset", current, typeof(ScriptableObject), false);
            if (EditorGUI.EndChangeCheck() && (newTarget == null || newTarget is HotspotLayout || newTarget is HotspotTexture))
                SetTarget(newTarget);

            if (!HasTarget) {
                EditorGUILayout.HelpBox("Select or double-click a Hotspot Layout or Hotspot Texture asset.", MessageType.Info);
                return;
            }

            if (editingHotspotTexture != null && editingHotspotTexture.layout != null)
                EditorGUILayout.HelpBox($"This Hotspot Texture uses the layout '{editingHotspotTexture.layout.name}'; you are editing the layout.", MessageType.Info);
            else if (editingHotspotTexture != null && editingHotspotTexture.target == null)
                EditorGUILayout.HelpBox("The Hotspot Texture has no texture assigned.", MessageType.Warning);

            if (editingLayout != null)
                previewTexture = (Texture)EditorGUILayout.ObjectField(new GUIContent("Preview", "Background texture shown only in this window."), previewTexture, typeof(Texture), false);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Grid Size (px)", GridStep.ToString());
                if (GUILayout.Button("<", GUILayout.Width(24))) gridLevel = Mathf.Max(0, gridLevel - 1);
                if (GUILayout.Button(">", GUILayout.Width(24))) gridLevel = Mathf.Min(GridSizes.Length - 1, gridLevel + 1);
            }

            EditorGUILayout.LabelField("Rects", Count.ToString());

            if (Layout != null && selected >= 0 && selected < Count)
                DrawPatchOptions();

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Frame"))
                    FrameTexture();
                using (new EditorGUI.DisabledScope(Count == 0)) {
                    if (GUILayout.Button("Clear All") &&
                        EditorUtility.DisplayDialog("Clear Hotspot Rects", "Remove all rects?", "Clear", "Cancel")) {
                        Undo.RecordObject(UndoTarget, "Clear Hotspot Rects");
                        if (Layout != null) Layout.patches.Clear();
                        else editingHotspotTexture.rects.Clear();
                        selected = -1;
                        EditorUtility.SetDirty(UndoTarget);
                    }
                }
            }

            EditorGUILayout.LabelField("LMB drag: create · LMB click: select · RMB: delete · MMB: pan · Wheel: zoom", EditorStyles.miniLabel);
        }

        private void DrawPatchOptions() {
            var patch = Layout.patches[selected];
            var size = GetPixelRect(selected).size;
            EditorGUILayout.LabelField($"Patch {selected}", $"{size.x:0}×{size.y:0} px", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            patch.allowRotation = EditorGUILayout.Toggle(new GUIContent("Allow Rotation", "Allow rotating the patch 90° to fit a face."), patch.allowRotation);
            patch.allowMirrorX = EditorGUILayout.Toggle(new GUIContent("Allow Mirror X", "Allow randomly flipping the patch horizontally."), patch.allowMirrorX);
            patch.allowMirrorY = EditorGUILayout.Toggle(new GUIContent("Allow Mirror Y", "Allow randomly flipping the patch vertically."), patch.allowMirrorY);
            patch.allowTiling = EditorGUILayout.Toggle(new GUIContent("Allow Tiling", "Repeat instead of stretching. The patch must span the full width or height."), patch.allowTiling);
            patch.inset = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Inset (px)", "Pixels to shrink the patch by on each side when mapping."), patch.inset));
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(Layout, "Edit Hotspot Patch");
                Layout.patches[selected] = patch;
                EditorUtility.SetDirty(Layout);
            }

            if (patch.allowTiling && Layout.GetTilingAxis(selected) == TilingAxis.None)
                EditorGUILayout.HelpBox("Tiling only works on patches spanning the full width or height.", MessageType.Warning);
        }
    }

    // Thanks to http://martinecker.com/martincodes/unity-editor-window-zooming/
    // Helper Rect extension methods
    public class EditorZoomArea {
        private const float kEditorWindowTabHeight = 21.0f;
        private static Matrix4x4 _prevGuiMatrix;

        public static Rect Begin(float zoomScale, Rect screenCoordsArea) {
            GUI.EndGroup();

            Rect clippedArea = screenCoordsArea.ScaleSizeBy(1.0f / zoomScale, screenCoordsArea.TopLeft());
            clippedArea.y += kEditorWindowTabHeight;
            GUI.BeginGroup(clippedArea);

            _prevGuiMatrix = GUI.matrix;
            Matrix4x4 translation = Matrix4x4.TRS(clippedArea.TopLeft(), Quaternion.identity, Vector3.one);
            Matrix4x4 scale = Matrix4x4.Scale(new Vector3(zoomScale, zoomScale, 1.0f));
            GUI.matrix = translation * scale * translation.inverse * GUI.matrix;

            return clippedArea;
        }

        public static void End() {
            GUI.matrix = _prevGuiMatrix;
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(0.0f, kEditorWindowTabHeight, Screen.width, Screen.height));
        }
    }
}
