using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.MultiScene {
    public enum SceneReferenceState {
        [Tooltip("No hay ninguna escena asignada")] Empty,
        [Tooltip("La escena está en la lista de escenas de la build")] Regular,
        [Tooltip("La escena es un Addressable")] Addressable,
        [Tooltip("La escena no está en la build ni es un Addressable: no se puede cargar")] Unsafe
    }

    /// <summary>
    /// Referencia serializable a una escena. Guarda el GUID del asset y, en el Editor, mantiene sincronizados
    /// su ruta y su dirección de Addressables para poder usarlos en la build.
    /// </summary>
    [Serializable]
    public class SceneReference {
        // ==================== VARIABLES ===================
        #region Properties
        public string Guid => guid;
        public string Path => path;
        public string Address => address;
        public string Name => string.IsNullOrEmpty(path) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(path);
        public bool HasValue => !string.IsNullOrEmpty(guid);

        /// <summary> Se calcula al consultarse: si la escena entra o sale de la build, el estado se actualiza solo </summary>
        public SceneReferenceState State {
            get {
                if (!HasValue) return SceneReferenceState.Empty;
                if (SceneUtility.GetBuildIndexByScenePath(path) >= 0) return SceneReferenceState.Regular;
                if (!string.IsNullOrEmpty(address)) return SceneReferenceState.Addressable;

                return SceneReferenceState.Unsafe;
            }
        }
        #endregion

        #region Private Variables
        [SerializeField] private string guid;
        [SerializeField] private string path;
        [SerializeField] private string address;
        #endregion

        // ==================== METHODS ====================
        public override string ToString() => HasValue ? $"{Name} ({State})" : "None";
    }
}
