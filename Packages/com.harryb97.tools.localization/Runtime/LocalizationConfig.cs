using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using Cysharp.Threading.Tasks;
using HarryUtils.Singleton;

namespace HarryTools.Localization {
    [CreateAssetMenu(fileName = "Localization Config", menuName = "Tools/Localization/Localization Config")]
    public class LocalizationConfig : ScriptableSingleton<LocalizationConfig> {
        [Serializable]
        public struct FlagStruct {
            public Locale locale;
            public AssetReference flag;
        }
        
        // ==================== VARIABLES ===================
        #region Private Variables
        [Header("Flags")]
        [SerializeField] private AssetReference defaultFlag;
        [SerializeField] private FlagStruct[] flags;
        #endregion
        
        // ==================== METHODS ====================
        public async UniTask<Sprite> GetFlag(Locale _locale) {
            AsyncOperationHandle<Sprite> _operation = default;

            if (!flags.Any(f => f.locale.LocaleName == _locale.LocaleName)) _operation = Addressables.LoadAssetAsync<Sprite>(defaultFlag);
            else {
                FlagStruct _struct = flags.First(f => f.locale.LocaleName == _locale.LocaleName);
                _operation = Addressables.LoadAssetAsync<Sprite>(_struct.flag);
            }

            await _operation;
            return _operation.Result;
        }
    }
}