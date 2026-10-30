using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HarryTools.MultiScene.Editor {
    /// <summary>
    /// Mantiene la ruta y la dirección de Addressables de cada 'SceneReference' sincronizadas con su GUID.
    /// Se actualiza al dibujarse en el inspector, al mover o borrar escenas, al cambiar Addressables y antes de cada build.
    /// </summary>
    internal class SceneReferenceUtility : AssetPostprocessor, IPreprocessBuildWithReport {
        public const string GuidField = "guid";
        public const string PathField = "path";
        public const string AddressField = "address";

        // ==================== VARIABLES ===================
        #region Public Variables
        public int callbackOrder => 0;
        #endregion

        #region Private Variables
        private static bool refreshScheduled;
        #endregion

        // ==================== START ====================
        [InitializeOnLoadMethod]
        private static void Initialize() {
            AddressableAssetSettings.OnModificationGlobal -= Event_OnAddressablesModified;
            AddressableAssetSettings.OnModificationGlobal += Event_OnAddressablesModified;
        }

        // ==================== EVENTS ====================
        private static void Event_OnAddressablesModified(AddressableAssetSettings _settings, AddressableAssetSettings.ModificationEvent _event, object _data) {
            switch (_event) {
                case AddressableAssetSettings.ModificationEvent.EntryAdded:
                case AddressableAssetSettings.ModificationEvent.EntryCreated:
                case AddressableAssetSettings.ModificationEvent.EntryModified:
                case AddressableAssetSettings.ModificationEvent.EntryMoved:
                case AddressableAssetSettings.ModificationEvent.EntryRemoved:
                    ScheduleRefresh();
                    break;
            }
        }

        private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom) {
            if (ContainsScene(_deleted) || ContainsScene(_moved)) ScheduleRefresh();
        }

        // ==================== METHODS ====================
        /// <summary> Actualiza la ruta y la dirección a partir del GUID. Devuelve si ha cambiado algo </summary>
        public static bool Sync(SerializedProperty _property) {
            SerializedProperty _guid = _property.FindPropertyRelative(GuidField);
            SerializedProperty _path = _property.FindPropertyRelative(PathField);
            SerializedProperty _address = _property.FindPropertyRelative(AddressField);

            string _newPath = string.IsNullOrEmpty(_guid.stringValue) ? string.Empty : AssetDatabase.GUIDToAssetPath(_guid.stringValue);
            string _newAddress = GetAddress(_guid.stringValue);

            if (_path.stringValue == _newPath && _address.stringValue == _newAddress) return false;

            _path.stringValue = _newPath;
            _address.stringValue = _newAddress;
            return true;
        }

        /// <summary> Recorre los ScriptableObjects del proyecto y sincroniza todas sus 'SceneReference' </summary>
        public static void RefreshAll() {
            bool _anyChanged = false;

            foreach (string _assetGuid in AssetDatabase.FindAssets("t:ScriptableObject")) {
                string _assetPath = AssetDatabase.GUIDToAssetPath(_assetGuid);

                // Los paquetes son de solo lectura
                if (!_assetPath.StartsWith("Assets/")) continue;

                foreach (Object _asset in AssetDatabase.LoadAllAssetsAtPath(_assetPath)) {
                    if (_asset is ScriptableObject && RefreshObject(_asset)) _anyChanged = true;
                }
            }

            if (_anyChanged) AssetDatabase.SaveAssets();
        }

        // =================================================

        private static bool RefreshObject(Object _target) {
            using SerializedObject _serializedObject = new(_target);
            SerializedProperty _iterator = _serializedObject.GetIterator();
            bool _changed = false;

            // Solo se entra en propiedades genéricas: los strings y primitivos no pueden contener una 'SceneReference'
            bool _enterChildren = true;
            while (_iterator.Next(_enterChildren)) {
                _enterChildren = _iterator.propertyType == SerializedPropertyType.Generic;

                if (_iterator.type == nameof(SceneReference) && Sync(_iterator)) _changed = true;
            }

            if (_changed) {
                _serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_target);
            }

            return _changed;
        }

        private static void ScheduleRefresh() {
            if (refreshScheduled) return;
            refreshScheduled = true;

            EditorApplication.delayCall += () => {
                refreshScheduled = false;
                RefreshAll();
            };
        }

        private static string GetAddress(string _guid) {
            if (string.IsNullOrEmpty(_guid)) return string.Empty;

            AddressableAssetSettings _settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetEntry _entry = _settings != null ? _settings.FindAssetEntry(_guid) : null;

            return _entry != null ? _entry.address : string.Empty;
        }

        private static bool ContainsScene(string[] _paths) {
            foreach (string _path in _paths) {
                if (_path.EndsWith(".unity")) return true;
            }

            return false;
        }

        // ==================== INTERFACES ===================
        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport _report) {
            RefreshAll();
        }
    }
}
