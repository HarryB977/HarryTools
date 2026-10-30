using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace HarryTools.SerializeReferences.Editor {
    /// <summary> Resolución de tipos de 'SerializeReference' y búsqueda de tipos candidatos para el desplegable </summary>
    public static class TypeUtility {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static readonly Dictionary<string, Type> ManagedTypesCache = new();
        private static readonly Dictionary<string, Assembly> AssembliesCache = new();
        private static readonly Dictionary<Type, Type[]> CandidatesCache = new();
        #endregion

        // ==================== METHODS ====================
        #region Managed Reference Types
        /// <summary>
        /// Convierte el formato de 'managedReferenceFullTypename' / 'managedReferenceFieldTypename' ("Assembly Namespace.Type") en un tipo.
        /// Devuelve null si está vacío o si el tipo ya no existe.
        /// </summary>
        public static Type GetManagedReferenceType(string _typename) {
            if (string.IsNullOrEmpty(_typename)) return null;
            if (ManagedTypesCache.TryGetValue(_typename, out Type _cached)) return _cached;

            Type _result = null;
            int _splitIndex = _typename.IndexOf(' ');

            if (_splitIndex > 0) {
                string _assemblyName = _typename[.._splitIndex];
                // Unity separa las clases anidadas con '/', reflection usa '+'
                string _typeName = _typename[(_splitIndex + 1)..].Replace('/', '+');

                Assembly _assembly = GetAssembly(_assemblyName);
                _result = _assembly?.GetType(_typeName, false);
            }

            ManagedTypesCache[_typename] = _result;
            return _result;
        }

        private static Assembly GetAssembly(string _name) {
            if (AssembliesCache.TryGetValue(_name, out Assembly _cached)) return _cached;

            Assembly _result = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(_assembly => _assembly.GetName().Name == _name);
            AssembliesCache[_name] = _result;

            return _result;
        }
        #endregion

        // =================================================

        #region Candidates
        /// <summary> Devuelve los tipos que pueden asignarse a un campo del tipo indicado </summary>
        public static IReadOnlyList<Type> GetCandidateTypes(Type _baseType) {
            if (_baseType == null) return Array.Empty<Type>();
            if (CandidatesCache.TryGetValue(_baseType, out Type[] _cached)) return _cached;

            IEnumerable<Type> _candidates = _baseType.IsGenericType ? EnumerateAllTypes() : TypeCache.GetTypesDerivedFrom(_baseType).Append(_baseType);

            Type[] _result = _candidates
                .Where(IsAllowed)
                .Where(_type => IsCompatible(_baseType, _type))
                .Distinct()
                .ToArray();

            CandidatesCache[_baseType] = _result;
            return _result;
        }

        /// <summary> Indica si un tipo puede instanciarse como 'SerializeReference' </summary>
        public static bool IsAllowed(Type _type) {
            return _type != null &&
                   _type.IsClass &&
                   !_type.IsAbstract &&
                   !_type.ContainsGenericParameters &&
                   !typeof(UnityEngine.Object).IsAssignableFrom(_type) &&
                   Attribute.IsDefined(_type, typeof(SerializableAttribute)) &&
                   !Attribute.IsDefined(_type, typeof(HideInTypeSelectorAttribute));
        }

        /// <summary> Compatibilidad teniendo en cuenta la varianza de los genéricos ('in' / 'out') </summary>
        public static bool IsCompatible(Type _baseType, Type _candidate) {
            if (!_baseType.IsGenericType) return _baseType.IsAssignableFrom(_candidate);
            if (_baseType.ContainsGenericParameters) return false;

            Type _definition = _baseType.GetGenericTypeDefinition();
            Type[] _targetArguments = _baseType.GetGenericArguments();
            Type[] _parameters = _definition.GetGenericArguments();

            foreach (Type _interface in _candidate.GetInterfaces()) {
                if (!_interface.IsGenericType || _interface.GetGenericTypeDefinition() != _definition) continue;
                if (AreArgumentsCompatible(_parameters, _targetArguments, _interface.GetGenericArguments())) return true;
            }

            for (Type _type = _candidate; _type != null && _type != typeof(object); _type = _type.BaseType) {
                if (!_type.IsGenericType || _type.GetGenericTypeDefinition() != _definition) continue;
                if (AreArgumentsCompatible(_parameters, _targetArguments, _type.GetGenericArguments())) return true;
            }

            return false;
        }

        private static bool AreArgumentsCompatible(Type[] _parameters, Type[] _targetArguments, Type[] _sourceArguments) {
            if (_parameters.Length != _targetArguments.Length || _sourceArguments.Length != _targetArguments.Length) return false;

            for (int i = 0; i < _parameters.Length; i++) {
                GenericParameterAttributes _variance = _parameters[i].GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

                bool _compatible = _variance switch {
                    GenericParameterAttributes.Contravariant => _sourceArguments[i].IsAssignableFrom(_targetArguments[i]),
                    GenericParameterAttributes.Covariant => _targetArguments[i].IsAssignableFrom(_sourceArguments[i]),
                    _ => _sourceArguments[i] == _targetArguments[i]
                };

                if (!_compatible) return false;
            }

            return true;
        }

        private static IEnumerable<Type> EnumerateAllTypes() {
            foreach (Assembly _assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                Type[] _types;

                try {
                    _types = _assembly.GetTypes();
                } catch (ReflectionTypeLoadException _exception) {
                    _types = _exception.Types;
                }

                if (_types == null) continue;

                foreach (Type _type in _types) {
                    if (_type != null) yield return _type;
                }
            }
        }
        #endregion
    }
}
