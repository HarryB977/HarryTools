using System;
using System.Collections.Generic;
using UnityEditor;

namespace HarryTools.Audio.Editor {
    /// <summary> Keeps the catalog in sync with the audio assets of the project. </summary>
    internal class AudioCatalogMaintainer : AssetPostprocessor {
        // ==================== VARIABLES ===================
        private static bool pending;

        // ==================== EVENTS ===================
        public static event Action Rebuilt;

        // ==================== PUBLIC ===================
        public static void Rebuild(AudioCatalog _catalog) {
            if (_catalog == null) return;
            string[] _guids = AssetDatabase.FindAssets("t:AudioCatalogItem");
            var _paths = new List<string>(_guids.Length);
            for (int _i = 0; _i < _guids.Length; _i++) _paths.Add(AssetDatabase.GUIDToAssetPath(_guids[_i]));
            _paths.Sort(StringComparer.Ordinal);

            var _items = new List<AudioCatalogItem>(_paths.Count);
            var _seen = new Dictionary<string, string>();
            for (int _i = 0; _i < _paths.Count; _i++) {
                var _item = AssetDatabase.LoadAssetAtPath<AudioCatalogItem>(_paths[_i]);
                if (_item == null) continue;
                AudioIdAssigner.EnsureId(_item);
                _items.Add(_item);
                if (_seen.TryGetValue(_item.Id, out string _first)) {
                    HarryTools.LoggerTool.Logger.LogWarning("Audio", $"Duplicate audio id {_item.Id}: {_first} and {_paths[_i]}");
                } else _seen.Add(_item.Id, _paths[_i]);
            }

            if (!SameItems(_catalog.Items, _items)) {
                _catalog.SetItems(_items);
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_catalog))) {
                    EditorUtility.SetDirty(_catalog);
                    AssetDatabase.SaveAssetIfDirty(_catalog);
                }
            }
            Rebuilt?.Invoke();
        }

        // ==================== INTERNAL ===================
        /// <summary> The catalog the maintainer updates. Task 4 redirects this to the project settings. </summary>
        internal static AudioCatalog FindTargetCatalog() {
            string[] _guids = AssetDatabase.FindAssets("t:AudioCatalog");
            if (_guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<AudioCatalog>(AssetDatabase.GUIDToAssetPath(_guids[0]));
        }

        // ==================== POSTPROCESSOR ===================
        private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom) {
            if (pending || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AnyItem(_imported) && !AnyItem(_moved) && !AnyAsset(_deleted) && !AnyAsset(_movedFrom)) return;
            pending = true;
            EditorApplication.delayCall += RunPending;
        }

        // ==================== PRIVATE ===================
        private static void RunPending() {
            pending = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Rebuild(FindTargetCatalog());
        }

        private static bool AnyItem(string[] _paths) {
            for (int _i = 0; _i < _paths.Length; _i++) {
                if (!_paths[_i].EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
                Type _type = AssetDatabase.GetMainAssetTypeAtPath(_paths[_i]);
                if (_type != null && typeof(AudioCatalogItem).IsAssignableFrom(_type)) return true;
            }
            return false;
        }

        // The type of a deleted asset is gone, so any .asset counts.
        private static bool AnyAsset(string[] _paths) {
            for (int _i = 0; _i < _paths.Length; _i++) {
                if (_paths[_i].EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool SameItems(IReadOnlyList<AudioCatalogItem> _a, List<AudioCatalogItem> _b) {
            if (_a.Count != _b.Count) return false;
            for (int _i = 0; _i < _a.Count; _i++) if (_a[_i] != _b[_i]) return false;
            return true;
        }
    }
}
