using System;
using System.IO;
using UnityEngine;

using Cysharp.Threading.Tasks;

using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.LocalData {
    // Stores objects as JSON through 'JsonUtility': the type must be '[Serializable]' with serializable fields.
    // Fields missing from an older file keep the default value of the class.
    public static class LocalDataSystem {
        // ==================== METHODS ====================

        /// <summary> Guarda un objeto en un archivo, pero no sobreescribe datos </summary>
        public static async UniTask<bool> SaveData<T>(T _object, string _directoryName, string _fileName) {
            if (CheckData(_directoryName, _fileName)) return false;

            return await WriteData(_object, _directoryName, _fileName);
        }

        /// <summary> Sobreescribe un archivo con los datos proporcionados </summary>
        public static async UniTask<bool> OverwriteData<T>(T _object, string _directoryName, string _fileName) {
            return await WriteData(_object, _directoryName, _fileName);
        }

        /// <summary> Carga un objeto de un archivo. Devuelve 'default' si no existe o no se puede leer </summary>
        public static async UniTask<T> LoadData<T>(string _directoryName, string _fileName) {
            if (!CheckData(_directoryName, _fileName)) {
                return default;
            }

            try {
                string _json = await File.ReadAllTextAsync(GetFilePath(_directoryName, _fileName));
                return JsonUtility.FromJson<T>(_json);

            } catch (Exception _exception) {
                Logger.LogWarning("LocalDataSystem", $"No se han podido leer los datos '{_fileName}': {_exception.Message}");
                return default;
            }
        }

        /// <summary> Elimina un archivo </summary>
        public static async UniTask<bool> RemoveData(string _directoryName, string _fileName) {
            if (!CheckData(_directoryName, _fileName)) {
                return false;
            }

            try {
                File.Delete(GetFilePath(_directoryName, _fileName));
                await UniTask.Yield();

                return true;

            } catch (Exception _exception) {
                Logger.LogError("LocalDataSystem", $"Ha ocurrido un error al intentar eliminar los datos '{_fileName}': {_exception.Message}");
                return false;
            }
        }

        // =================================================

        /// <summary> Devuelve 'true' si existe la carpeta que se pide </summary>
        public static bool CheckDirectory(string _directoryName) {
            return Directory.Exists(GetDirectoryPath(_directoryName));
        }

        /// <summary> Devuelve 'true' si existe el archivo de guardado que se pide </summary>
        public static bool CheckData(string _directoryName, string _fileName) {
            return CheckDirectory(_directoryName) && File.Exists(GetFilePath(_directoryName, _fileName));
        }

        // =================================================

        // Creates or truncates the file, so shorter data never leaves old bytes behind
        private static async UniTask<bool> WriteData<T>(T _object, string _directoryName, string _fileName) {
            try {
                if (!CheckDirectory(_directoryName)) Directory.CreateDirectory(GetDirectoryPath(_directoryName));

                await File.WriteAllTextAsync(GetFilePath(_directoryName, _fileName), JsonUtility.ToJson(_object, true));
                return true;

            } catch (Exception _exception) {
                Logger.LogError("LocalDataSystem", $"Ha ocurrido un error al intentar guardar los datos '{_fileName}': {_exception.Message}");
                return false;
            }
        }

        private static string GetDirectoryPath(string _directoryName) {
            return $"{Application.persistentDataPath}/{_directoryName}";
        }

        private static string GetFilePath(string _directoryName, string _fileName) {
            return $"{Application.persistentDataPath}/{_directoryName}/{_fileName}.json";
        }
    }
}
