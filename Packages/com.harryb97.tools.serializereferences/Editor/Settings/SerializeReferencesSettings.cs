using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.SerializeReferences.Editor {
    /// <summary> Preferencias del paquete (Edit > Preferences > HarryTools > Serialize References) </summary>
    public static class SerializeReferencesSettings {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string AutoFixDuplicatesKey = "HarryTools.SerializeReferences.AutoFixDuplicates";
        private const string LogFixesKey = "HarryTools.SerializeReferences.LogFixes";
        #endregion

        #region Public Variables
        /// <summary> Convierte en instancias independientes las referencias compartidas de los campos con [TypeSelector] </summary>
        public static bool AutoFixDuplicates {
            get => EditorPrefs.GetBool(AutoFixDuplicatesKey, true);
            set => EditorPrefs.SetBool(AutoFixDuplicatesKey, value);
        }

        /// <summary> Muestra un mensaje en consola cada vez que se corrige una referencia compartida </summary>
        public static bool LogFixes {
            get => EditorPrefs.GetBool(LogFixesKey, false);
            set => EditorPrefs.SetBool(LogFixesKey, value);
        }
        #endregion

        // ==================== METHODS ====================
        [SettingsProvider]
        private static SettingsProvider CreateProvider() {
            return new SettingsProvider("Preferences/HarryTools/Serialize References", SettingsScope.User) {
                guiHandler = _ => {
                    EditorGUIUtility.labelWidth = 260;

                    AutoFixDuplicates = EditorGUILayout.Toggle(new GUIContent("Corregir referencias duplicadas",
                        "Al añadir un elemento a un array, Unity copia la referencia del anterior y ambos comparten instancia. " +
                        "Con esta opción, se crea una copia independiente automáticamente (solo en campos con [TypeSelector])."), AutoFixDuplicates);

                    using (new EditorGUI.DisabledScope(!AutoFixDuplicates)) {
                        LogFixes = EditorGUILayout.Toggle("Avisar en consola de cada corrección", LogFixes);
                    }

                    EditorGUILayout.Space();
                    if (GUILayout.Button("Abrir SerializeReference Explorer", GUILayout.Width(260))) SerializeReferenceExplorerWindow.Open();
                },
                keywords = new HashSet<string> { "SerializeReference", "TypeSelector", "Duplicate", "Referencias" }
            };
        }
    }
}
