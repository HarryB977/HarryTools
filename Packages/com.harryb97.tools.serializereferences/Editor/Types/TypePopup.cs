using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.SerializeReferences.Editor {
    public class TypePopupItem : AdvancedDropdownItem {
        public Type Type { get; }

        public TypePopupItem(Type _type, string _name) : base(_name) {
            Type = _type;
        }
    }

    /// <summary> Desplegable de tipos con buscador, agrupado por la ruta de [TypeMenu] o por namespace </summary>
    public class TypePopup : AdvancedDropdown {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Se sustituye en cada apertura para apuntar a la propiedad que abrió el desplegable </summary>
        public Action<TypePopupItem> OnItemSelected { get; set; }
        #endregion

        #region Private Variables
        private const int MaxNamespaceNestCount = 16;
        private static readonly float HeaderHeight = EditorGUIUtility.singleLineHeight * 2f;

        private readonly Type[] types;
        private readonly bool allowNull;
        #endregion

        // ==================== START ====================
        public TypePopup(IEnumerable<Type> _types, bool _allowNull, int _maxLineCount, AdvancedDropdownState _state) : base(_state) {
            types = _types.ToArray();
            allowNull = _allowNull;
            minimumSize = new Vector2(minimumSize.x, EditorGUIUtility.singleLineHeight * _maxLineCount + HeaderHeight);
        }

        // ==================== METHODS ====================
        protected override AdvancedDropdownItem BuildRoot() {
            AdvancedDropdownItem _root = new("Select Type");
            int _id = 0;

            if (allowNull) _root.AddChild(new TypePopupItem(null, TypeMenuUtility.NullDisplayName) { id = _id++ });

            Type[] _ordered = types.OrderByTypeMenu().ToArray();
            bool _isSingleNamespace = IsSingleNamespace(_ordered);

            foreach (Type _type in _ordered) {
                string[] _path = TypeMenuUtility.GetSplitTypePath(_type);
                if (_path.Length == 0) continue;

                AdvancedDropdownItem _parent = _root;

                // Si todos comparten el mismo namespace, no tiene sentido crear carpetas
                if (!_isSingleNamespace) {
                    for (int k = 0; k < _path.Length - 1; k++) {
                        AdvancedDropdownItem _found = _parent.children.FirstOrDefault(_item => _item.name == _path[k]);

                        if (_found == null) {
                            _found = new AdvancedDropdownItem(_path[k]) { id = _id++ };
                            _parent.AddChild(_found);
                        }

                        _parent = _found;
                    }
                }

                _parent.AddChild(new TypePopupItem(_type, ObjectNames.NicifyVariableName(_path[^1])) { id = _id++ });
            }

            return _root;
        }

        protected override void ItemSelected(AdvancedDropdownItem _item) {
            base.ItemSelected(_item);
            if (_item is TypePopupItem _typeItem) OnItemSelected?.Invoke(_typeItem);
        }

        // =================================================

        private static bool IsSingleNamespace(Type[] _types) {
            string[] _namespaces = new string[MaxNamespaceNestCount];

            foreach (Type _type in _types) {
                string[] _path = TypeMenuUtility.GetSplitTypePath(_type);
                if (_path.Length <= 1) continue;

                // Si se ha pedido una categoría explícitamente, se respeta
                if (TypeMenuUtility.GetAttribute(_type) != null) return false;

                for (int k = 0; k < _path.Length - 1 && k < MaxNamespaceNestCount; k++) {
                    if (_namespaces[k] == null) _namespaces[k] = _path[k];
                    else if (_namespaces[k] != _path[k]) return false;
                }
            }

            return true;
        }
    }
}
