using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

using Cysharp.Threading.Tasks;

using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.LocalData {
    public static class LocalDataSystem {
        // ==================== METHODS ====================

        /// <summary> Guarda un objeto en un archivo, pero no sobreescribe datos </summary>
        public static async UniTask<bool> SaveData<T>(T _object, string _directoryName, string _fileName) {
            // Directory
            if (!CheckDirectory(_directoryName)) {
                Directory.CreateDirectory($"{Application.persistentDataPath}/{_directoryName}");
            }

            // File
            if (CheckData(_directoryName, _fileName)) return false;
            try {
                BinaryFormatter _binaryFormatter = new();
                FileStream _file = File.Create(GetFilePath(_directoryName, _fileName));
                _binaryFormatter.Serialize(_file, _object);

                await UniTask.Yield();

                _file.Close();
                return true;

            } catch (Exception _exception) {
                Logger.LogError("LocalDataSystem", $"Ha ocurrido un error al intentar guardar los datos '{_fileName}': {_exception.Message}");
                return false;
            }
        }

        /// <summary> Sobreescribe un archivo con los datos proporcionados </summary>
        public static async UniTask<bool> OverwriteData<T>(T _object, string _directoryName, string _fileName) {
            if (!CheckData(_directoryName, _fileName)) return await SaveData(_object, _directoryName, _fileName);
            else {
                try {
                    BinaryFormatter _binaryFormatter = new();
                    FileStream _file = File.Open(GetFilePath(_directoryName, _fileName), FileMode.Open);
                    _binaryFormatter.Serialize(_file, _object);

                    await UniTask.Yield();

                    _file.Close();
                    return true;

                } catch (Exception _exception) {
                    Logger.LogError("LocalDataSystem", $"Ha ocurrido un error al intentar sobreescribir los datos '{_fileName}': {_exception.Message}");
                    return false;
                }
            }
        }

        /// <summary> Carga un objeto de un archivo </summary>
        public static async UniTask<T> LoadData<T>(string _directoryName, string _fileName) {
            if (!CheckData(_directoryName, _fileName)) {
                return default;
            }

            try {
                BinaryFormatter _binaryFormatter = new();
                FileStream _file = File.Open(GetFilePath(_directoryName, _fileName), FileMode.Open);
                T _data = (T)_binaryFormatter.Deserialize(_file);

                await UniTask.Yield();

                _file.Close();
                return _data;

            } catch (Exception _exception) {
                Logger.LogError("LocalDataSystem", $"Ha ocurrido un error al intentar cargar los datos '{_fileName}': {_exception.Message}");
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

        private static string GetDirectoryPath(string _directoryName) {
            return $"{Application.persistentDataPath}/{_directoryName}";
        }

        private static string GetFilePath(string _directoryName, string _fileName) {
            return $"{Application.persistentDataPath}/{_directoryName}/{_fileName}.dat";
        }
    }
}