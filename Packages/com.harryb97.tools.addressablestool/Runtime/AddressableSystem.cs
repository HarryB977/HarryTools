using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

using Cysharp.Threading.Tasks;
using HarryUtils;

namespace HarryTools.AddressablesTool {
    public static class AddressableSystem {
        // ==================== METHODS ====================
        public static async UniTask Initialize() {
            await Addressables.InitializeAsync();
        }

        /// <summary>
        /// Busca y actualiza catálogos en remoto
        /// (No funciona si la opción de "actualizar los catálogos manualmente" está deshabilitada)
        /// </summary>
        public static async UniTask UpdateCatalogs() {
            List<string> _outdatedCatalogs = await Addressables.CheckForCatalogUpdates();

            if (_outdatedCatalogs.Count > 0) {
                List<IResourceLocator> _catalog = await Addressables.UpdateCatalogs(_outdatedCatalogs);

                if (_catalog.Count > 0) {
                    for (int i = 0; i < _catalog.Count; i++) {
                        Addressables.AddResourceLocator(_catalog[i]);
                    }
                }
            }
        }

        // =================================================
        
        #region Locations
        /// <summary> Devuelve una lista con las localizaciones de los Assets que poseen los labels que se pasen como parámetro </summary>
        public static async UniTask<IList<IResourceLocation>> GetAssetsLocations(params string[] _labels) {
            AsyncOperationHandle<IList<IResourceLocation>> _handle = Addressables.LoadResourceLocationsAsync(_labels, Addressables.MergeMode.Union);
            await _handle;

            IList<IResourceLocation> _locations = _handle.Result;
            Addressables.Release(_handle);
            
            return _locations.IsNullOrEmpty() ? null : _locations;
        }

        /// <summary> Devuelve una lista con las localizaciones de los Assets que poseen el label que se pase como parámetro </summary>
        public static async UniTask<IList<IResourceLocation>> GetAssetsLocations(string _label) {
            AsyncOperationHandle<IList<IResourceLocation>> _handle = Addressables.LoadResourceLocationsAsync(_label);
            await _handle;

            IList<IResourceLocation> _locations = _handle.Result;
            Addressables.Release(_handle);

            return _locations.IsNullOrEmpty() ? null : _locations;
        }
        #endregion

        // =================================================
        
        #region Groups
        /// <summary> Crea un grupo de assets que compartan los mismos labels </summary>
        public static async UniTask<AddressableGroup<T>> CreateAddressableGroup<T>(params string[] _labels) {
            if (_labels.IsNullOrEmpty()) return default;
            
            IList<IResourceLocation> _locations = await GetAssetsLocations(_labels);
            AddressableGroup<T> _group = new(_locations);
            
            return _group;
        }

        /// <summary> Crea un grupo de assets que compartan el mismo label </summary>
        public static async UniTask<AddressableGroup<T>> CreateAddressableGroup<T>(string _label) {
            if (_label.IsNullOrEmpty()) return default;

            IList<IResourceLocation> _locations = await GetAssetsLocations(_label);
            AddressableGroup<T> _group = new(_locations);

            return _group;
        }
        #endregion

        // =================================================
        
        #region Load Asset
        public static async UniTask<(T, AsyncOperationHandle<T>)> LoadAsset<T>(IResourceLocation _location) {
            AsyncOperationHandle<T> _handle = Addressables.LoadAssetAsync<T>(_location);
            await _handle;

            return _handle.Status == AsyncOperationStatus.Succeeded ? (_handle.Result, _handle) : (default, _handle);
        }

        public static async UniTask<(IList<T>, AsyncOperationHandle<IList<T>>)> LoadAssets<T>(ICollection<IResourceLocation> _locations, Action<T> _callback = null) {
            AsyncOperationHandle<IList<T>> _handle = Addressables.LoadAssetsAsync<T>(_locations, _callback);
            await _handle;

            return _handle.Status == AsyncOperationStatus.Succeeded ? (_handle.Result, _handle) : (default, _handle);
        }

        public static async UniTask<(T, AsyncOperationHandle<T>)> LoadAsset<T>(AssetReference _reference) {
            AsyncOperationHandle<T> _handle = Addressables.LoadAssetAsync<T>(_reference);
            await _handle;

            return _handle.Status == AsyncOperationStatus.Succeeded ? (_handle.Result, _handle) : (default, _handle);
        }

        public static async UniTask<(IList<T>, AsyncOperationHandle<IList<T>>)> LoadAssets<T>(ICollection<AssetReference> _references, Action<T> _callback = null) {
            AsyncOperationHandle<IList<T>> _handle = Addressables.LoadAssetsAsync<T>(_references, _callback);
            await _handle;

            return _handle.Status == AsyncOperationStatus.Succeeded ? (_handle.Result, _handle) : (default, _handle);
        }

        public static async UniTask<(T, AsyncOperationHandle<T>)> LoadAsset<T>(string _address) {
            AsyncOperationHandle<T> _handle = Addressables.LoadAssetAsync<T>(_address);
            await _handle;

            return _handle.Status == AsyncOperationStatus.Succeeded ? (_handle.Result, _handle) : (default, _handle);
        }

        public static async UniTask<(IList<T>, AsyncOperationHandle<IList<T>>)> LoadAssets<T>(ICollection<string> _addresses, Action<T> _callback = null) {
            AsyncOperationHandle<IList<T>> _handle = Addressables.LoadAssetsAsync<T>(_addresses, _callback);
            await _handle;

            return _handle.Status == AsyncOperationStatus.Succeeded ? (_handle.Result, _handle) : (default, _handle);
        }
        #endregion

        #region Instantiate Asset
        public static async UniTask<(GameObject, AsyncOperationHandle<GameObject>)> InstantiateAsset(IResourceLocation _location, Vector3 _position = default, Transform _parent = null, Quaternion _rotation = default) {
            AsyncOperationHandle<GameObject> _handle = Addressables.InstantiateAsync(
                _location, new InstantiationParameters(_position, _rotation, _parent)
            );

            GameObject _instance = await _handle;
            return _handle.Status == AsyncOperationStatus.Succeeded ? (_instance, _handle) : (null, _handle);
        }

        public static async UniTask<(GameObject, AsyncOperationHandle<GameObject>)> InstantiateAsset(AssetReference _reference, Vector3 _position = default, Transform _parent = null, Quaternion _rotation = default) {
            AsyncOperationHandle<GameObject> _handle = Addressables.InstantiateAsync(
                _reference, new InstantiationParameters(_position, _rotation, _parent)
            );

            GameObject _instance = await _handle;
            return _handle.Status == AsyncOperationStatus.Succeeded ? (_instance, _handle) : (null, _handle);
        }

        public static async UniTask<(GameObject, AsyncOperationHandle<GameObject>)> InstantiateAsset(string _address, Vector3 _position = default, Transform _parent = null, Quaternion _rotation = default) {
            AsyncOperationHandle<GameObject> _handle = Addressables.InstantiateAsync(
                _address, new InstantiationParameters(_position, _rotation, _parent)
            );

            GameObject _instance = await _handle;
            return _handle.Status == AsyncOperationStatus.Succeeded ? (_instance, _handle) : (null, _handle);
        }
        #endregion
    }
}