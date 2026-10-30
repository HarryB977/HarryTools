using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;
using UnityManagedReferenceUtility = UnityEngine.Serialization.ManagedReferenceUtility;

namespace HarryTools.SerializeReferences.Editor {
    /// <summary> Creación, clonado y búsqueda de instancias de 'SerializeReference' </summary>
    public static class ManagedReferenceTools {
        // ==================== METHODS ====================
        #region Instances
        /// <summary> Crea una instancia aunque el tipo no tenga constructor público sin parámetros </summary>
        public static object CreateInstance(Type _type) {
            if (_type == null) return null;

            try {
                return Activator.CreateInstance(_type, true);
            } catch (MissingMethodException) {
                return FormatterServices.GetUninitializedObject(_type);
            }
        }

        /// <summary>
        /// Copia profunda de una instancia. Usa 'EditorJsonUtility', por lo que conserva las referencias
        /// a objetos de Unity y crea instancias nuevas para las 'SerializeReference' anidadas.
        /// </summary>
        public static object Clone(object _source) {
            if (_source == null) return null;

            object _clone = CreateInstance(_source.GetType());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(_source), _clone);

            return _clone;
        }

        /// <summary> Copia los campos con el mismo nombre de una instancia a otra, aunque sean de tipos distintos </summary>
        public static void CopyValues(object _source, object _destination) {
            if (_source == null || _destination == null) return;
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(_source), _destination);
        }

        /// <summary> Asigna una instancia nueva del tipo indicado, conservando los valores compatibles de la anterior </summary>
        public static object SetManagedReference(this SerializedProperty _property, Type _type) {
            object _result = CreateInstance(_type);
            if (_result != null) CopyValues(_property.managedReferenceValue, _result);

            _property.managedReferenceValue = _result;
            return _result;
        }
        #endregion

        // =================================================

        #region Properties
        public static bool HasValidId(this SerializedProperty _property) {
            long _id = _property.managedReferenceId;
            return _id != UnityManagedReferenceUtility.RefIdNull && _id != UnityManagedReferenceUtility.RefIdUnknown;
        }

        /// <summary> Recorre todas las propiedades 'ManagedReference' de un objeto, incluidas las anidadas </summary>
        public static IEnumerable<SerializedProperty> EnumerateManagedReferences(SerializedObject _serializedObject) {
            SerializedProperty _iterator = _serializedObject.GetIterator();
            bool _enterChildren = true;

            while (_iterator.Next(_enterChildren)) {
                // Los strings son arrays de caracteres, no merece la pena entrar en ellos
                _enterChildren = _iterator.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference;

                if (_iterator.propertyType == SerializedPropertyType.ManagedReference) yield return _iterator.Copy();
            }
        }

        /// <summary> Indica si el objeto tiene alguna 'SerializeReference' (asignada o con tipo perdido) </summary>
        public static bool HasManagedReferences(Object _target) {
            if (_target == null) return false;
            return UnityEngine.Serialization.ManagedReferenceUtility.GetManagedReferenceIds(_target).Length > 0 || SerializationUtility.HasManagedReferencesWithMissingTypes(_target);
        }
        #endregion
    }
}
