using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HarryTools.UI.Editor
{
    /// <summary> Crea sets de iconos a partir de una fuente TTF y su fichero de metadatos </summary>
    public static class IconSetImporter
    {
        public enum MetadataFormat
        {
            /// <summary> meta.json de @mdi/svg (Material Design Icons): nombre, codepoint, alias y etiquetas </summary>
            MdiMetaJson,
            /// <summary> Fichero "codepoints" de Google (Material Symbols): "nombre hex" por línea </summary>
            GoogleCodepoints
        }

        [Serializable]
        private class MdiEntry
        {
            public string name;
            public string codepoint;
            public string[] aliases;
            public string[] tags;
        }

        [Serializable]
        private class MdiEntries
        {
            public MdiEntry[] items;
        }

        // ==================== METHODS ====================
        public static List<IconGlyph> Parse(string text, MetadataFormat format) =>
            format == MetadataFormat.MdiMetaJson ? ParseMdiMeta(text) : ParseCodepoints(text);

        public static List<IconGlyph> ParseMdiMeta(string json)
        {
            var entries = JsonUtility.FromJson<MdiEntries>("{\"items\":" + json + "}");
            var glyphs = new List<IconGlyph>(entries.items?.Length ?? 0);

            foreach (var entry in entries.items ?? Array.Empty<MdiEntry>())
            {
                if (string.IsNullOrEmpty(entry.name) || !TryParseHex(entry.codepoint, out uint codepoint))
                    continue;

                glyphs.Add(new IconGlyph
                {
                    name = entry.name,
                    codepoint = codepoint,
                    aliases = entry.aliases ?? Array.Empty<string>(),
                    tags = entry.tags ?? Array.Empty<string>()
                });
            }

            return glyphs;
        }

        public static List<IconGlyph> ParseCodepoints(string text)
        {
            var glyphs = new List<IconGlyph>();

            foreach (var rawLine in text.Split('\n'))
            {
                var parts = rawLine.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !TryParseHex(parts[1], out uint codepoint))
                    continue;

                glyphs.Add(new IconGlyph { name = parts[0], codepoint = codepoint, aliases = Array.Empty<string>(), tags = Array.Empty<string>() });
            }

            return glyphs;
        }

        /// <summary>
        /// Crea en la carpeta un TMP_FontAsset dinámico (los glifos se rasterizan al usarse) y el asset IconSet.
        /// Si ya existen, los sustituye.
        /// </summary>
        public static IconSet CreateIconSet(string id, Font font, List<IconGlyph> glyphs, string folder, string fontAssetFolder = null)
        {
            if (!font) throw new ArgumentNullException(nameof(font));
            fontAssetFolder ??= folder;
            EnsureFolder(folder);
            EnsureFolder(fontAssetFolder);

            string fontPath = $"{fontAssetFolder}/{font.name} SDF.asset";
            AssetDatabase.DeleteAsset(fontPath);

            var fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fontAsset.name = $"{font.name} SDF";
            AssetDatabase.CreateAsset(fontAsset, fontPath);

            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            foreach (var atlas in fontAsset.atlasTextures)
            {
                atlas.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            string setPath = $"{folder}/{id}.asset";
            var set = AssetDatabase.LoadAssetAtPath<IconSet>(setPath);
            if (!set)
            {
                set = ScriptableObject.CreateInstance<IconSet>();
                AssetDatabase.CreateAsset(set, setPath);
            }

            set.SetData(id, fontAsset, font, glyphs);
            EditorUtility.SetDirty(set);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary> Añade el set a la biblioteca (o lo sustituye si ya hay uno con su id) </summary>
        public static void AddToLibrary(IconLibrary library, IconSet set, bool makeDefault)
        {
            var sets = new List<IconSet>();
            foreach (var existing in library.Sets)
                if (existing && existing != set && !string.Equals(existing.Id, set.Id, StringComparison.OrdinalIgnoreCase))
                    sets.Add(existing);
            sets.Add(set);

            var defaultSet = makeDefault || !library.DefaultSet || library.DefaultSet.Id == set.Id ? set : library.DefaultSet;
            library.SetSets(defaultSet, sets);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        private static bool TryParseHex(string value, out uint codepoint) =>
            uint.TryParse(value?.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codepoint);

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    /// <summary> Ventana para importar un set de iconos nuevo </summary>
    public class IconSetImporterWindow : EditorWindow
    {
        Font _font;
        TextAsset _metadata;
        IconSetImporter.MetadataFormat _format;
        string _id = "icons";
        DefaultAsset _folder;
        bool _addToLibrary = true;

        [MenuItem("Window/HarryTools/UI/Icon Set Importer")]
        static void Open() => GetWindow<IconSetImporterWindow>("Import Icons");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Creates an icon set from an icon font (TTF) and its metadata file. " +
                                    "Only use openly licensed fonts (for example Material Symbols, Tabler or Phosphor).", MessageType.Info);

            _font = (Font)EditorGUILayout.ObjectField("Font (TTF)", _font, typeof(Font), false);
            _metadata = (TextAsset)EditorGUILayout.ObjectField("Metadata", _metadata, typeof(TextAsset), false);
            _format = (IconSetImporter.MetadataFormat)EditorGUILayout.EnumPopup("Format", _format);
            _id = EditorGUILayout.TextField(new GUIContent("Id", "Tag prefix: <icon=id:name>"), _id);
            _folder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _folder, typeof(DefaultAsset), false);
            _addToLibrary = EditorGUILayout.Toggle("Add to Library", _addToLibrary);

            string folderPath = _folder ? AssetDatabase.GetAssetPath(_folder) : null;
            bool valid = _font && _metadata && !string.IsNullOrWhiteSpace(_id) && AssetDatabase.IsValidFolder(folderPath);

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (!GUILayout.Button("Import"))
                    return;
            }

            var glyphs = IconSetImporter.Parse(_metadata.text, _format);
            var set = IconSetImporter.CreateIconSet(_id.Trim(), _font, glyphs, folderPath);

            var library = IconLibrary.Instance;
            if (_addToLibrary && library)
                IconSetImporter.AddToLibrary(library, set, false);

            EditorGUIUtility.PingObject(set);
            ShowNotification(new GUIContent($"{glyphs.Count} icons imported"));
        }
    }
}
