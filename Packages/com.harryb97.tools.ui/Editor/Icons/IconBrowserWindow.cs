using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HarryTools.UI.Editor
{
    /// <summary> Buscador de iconos con previsualización: clic copia la etiqueta, doble clic la añade al texto seleccionado </summary>
    public class IconBrowserWindow : EditorWindow
    {
        const float CellSize = 64f;
        const float GlyphSize = 32f;
        const int MaxResults = 3000;

        string _query = "";
        int _setIndex;
        Vector2 _scroll;
        List<(IconSet set, IconGlyph glyph)> _results = new();
        (IconSet set, IconGlyph glyph)? _selected;
        GUIStyle _glyphStyle, _largeGlyphStyle, _nameStyle;
        double _lastClickTime;

        [MenuItem("Window/HarryTools/UI/Icon Browser")]
        public static void Open() => GetWindow<IconBrowserWindow>("Icons");

        void OnEnable()
        {
            minSize = new Vector2(360, 300);
            Search();
        }

        void OnGUI()
        {
            var library = IconLibrary.Instance;
            if (!library || library.Sets.Count == 0)
            {
                EditorGUILayout.HelpBox("No icon library found (Resources/HarryTools.UI/IconLibrary).", MessageType.Warning);
                return;
            }

            DrawToolbar(library);
            DrawGrid();
            DrawDetails(library);
        }

        void DrawToolbar(IconLibrary library)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _query = EditorGUILayout.TextField(_query, EditorStyles.toolbarSearchField);

                var options = new[] { "All" }.Concat(library.Sets.Where(s => s).Select(s => s.Id)).ToArray();
                _setIndex = EditorGUILayout.Popup(Mathf.Clamp(_setIndex, 0, options.Length - 1), options, EditorStyles.toolbarPopup, GUILayout.Width(110));

                if (EditorGUI.EndChangeCheck())
                    Search();
            }

            EditorGUILayout.LabelField($"{_results.Count}{(_results.Count >= MaxResults ? "+" : "")} iconos", EditorStyles.miniLabel);
        }

        void DrawGrid()
        {
            EnsureStyles();

            float width = position.width - 16f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(width / CellSize));
            int rows = Mathf.CeilToInt(_results.Count / (float)columns);
            float detailsHeight = _selected.HasValue ? 110f : 0f;
            float viewHeight = position.height - 60f - detailsHeight;

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(viewHeight));
            var content = GUILayoutUtility.GetRect(width, rows * CellSize);

            // Solo se dibujan las filas visibles
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / CellSize));
            int lastRow = Mathf.Min(rows - 1, Mathf.CeilToInt((_scroll.y + viewHeight) / CellSize));

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int i = row * columns + col;
                    if (i >= _results.Count) break;

                    var cell = new Rect(content.x + col * CellSize, content.y + row * CellSize, CellSize - 2f, CellSize - 2f);
                    DrawCell(cell, _results[i]);
                }
            }

            GUILayout.EndScrollView();
        }

        void DrawCell(Rect cell, (IconSet set, IconGlyph glyph) item)
        {
            bool isSelected = _selected.HasValue && _selected.Value.set == item.set && _selected.Value.glyph.name == item.glyph.name;
            EditorGUI.DrawRect(cell, isSelected ? new Color(0.24f, 0.48f, 0.9f, 0.45f) : new Color(0f, 0f, 0f, 0.12f));

            _glyphStyle.font = item.set.SourceFont;
            GUI.Label(new Rect(cell.x, cell.y + 4f, cell.width, GlyphSize + 4f), new GUIContent(item.glyph.Character, Tooltip(item)), _glyphStyle);
            GUI.Label(new Rect(cell.x + 2f, cell.yMax - 16f, cell.width - 4f, 14f), item.glyph.name, _nameStyle);

            var e = Event.current;
            if (e.type != EventType.MouseDown || !cell.Contains(e.mousePosition))
                return;

            bool doubleClick = EditorApplication.timeSinceStartup - _lastClickTime < 0.3 && isSelected;
            _lastClickTime = EditorApplication.timeSinceStartup;
            _selected = item;

            string tag = TagFor(item);
            EditorGUIUtility.systemCopyBuffer = tag;
            ShowNotification(new GUIContent($"Copied {tag}"), 0.6f);

            if (doubleClick)
                AppendToSelectedText(tag);

            e.Use();
        }

        void DrawDetails(IconLibrary library)
        {
            if (!_selected.HasValue) return;

            var (set, glyph) = _selected.Value;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(100)))
            {
                _largeGlyphStyle.font = set.SourceFont;
                GUILayout.Label(glyph.Character, _largeGlyphStyle, GUILayout.Width(90), GUILayout.Height(90));

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.SelectableLabel(TagFor(_selected.Value), EditorStyles.boldLabel, GUILayout.Height(18));
                    EditorGUILayout.LabelField("Set", set.Id);
                    EditorGUILayout.LabelField("Codepoint", $"U+{glyph.codepoint:X}");
                    if (glyph.aliases is { Length: > 0 }) EditorGUILayout.LabelField("Alias", string.Join(", ", glyph.aliases));
                    if (glyph.tags is { Length: > 0 }) EditorGUILayout.LabelField("Tags", string.Join(", ", glyph.tags));
                }
            }
        }

        void Search()
        {
            var library = IconLibrary.Instance;
            if (!library) { _results.Clear(); return; }

            string setId = _setIndex > 0 && _setIndex - 1 < library.Sets.Count ? library.Sets[_setIndex - 1]?.Id : null;
            _results = IconSearch.Find(library.Sets, _query, setId, MaxResults);
            _scroll = Vector2.zero;
        }

        static string TagFor((IconSet set, IconGlyph glyph) item)
        {
            var library = IconLibrary.Instance;
            bool isDefault = library && library.DefaultSet == item.set;
            return isDefault ? $"<icon={item.glyph.name}>" : $"<icon={item.set.Id}:{item.glyph.name}>";
        }

        static string Tooltip((IconSet set, IconGlyph glyph) item)
        {
            var parts = new List<string> { item.glyph.name };
            if (item.glyph.aliases is { Length: > 0 }) parts.Add("Alias: " + string.Join(", ", item.glyph.aliases));
            if (item.glyph.tags is { Length: > 0 }) parts.Add("Tags: " + string.Join(", ", item.glyph.tags));
            return string.Join("\n", parts);
        }

        static void AppendToSelectedText(string tag)
        {
            var text = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<TMP_Text>() : null;
            if (!text) return;

            Undo.RecordObject(text, "Add Icon");
            text.text += tag;
            EditorUtility.SetDirty(text);
        }

        void EnsureStyles()
        {
            _glyphStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = (int)GlyphSize };
            _largeGlyphStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 72 };
            _nameStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
        }
    }
}
