using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

using Cysharp.Threading.Tasks;

namespace HarryTools.AddressablesTool {
    public class AddressableGroup<T> {
        // ==================== VARIABLES ===================
        #region Properties
        public int Count => Locations == null ? 0 : Locations.Count;
        #endregion

        #region Public Variables
        public IList<IResourceLocation> Locations { get; private set; }
        public IList<T> Assets { get; private set; }

        public AsyncOperationHandle<IList<T>> OperationHandle { get; private set; }
        #endregion

        // ==================== START ====================
        public AddressableGroup() { }
        public AddressableGroup(IList<IResourceLocation> _locations) : base() {
            Locations = _locations;
        }

        // ==================== METHODS ====================
        public async UniTask LoadAllAssets(Action<T> _callback = null) {
            (Assets, OperationHandle) = await AddressableSystem.LoadAssets<T>(Locations, _callback);
        }
        
        public void ReleaseAssets() {
            if (OperationHandle.IsValid()) {
                Addressables.Release(OperationHandle);
            }
        }
    }
}