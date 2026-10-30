using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using HarryUtils;

namespace HarryTools.GameRules {
    [Serializable]
    public class GameRuleList {
        // TODO: Añadir "GameRuleComposite", un struct que sirve como conjunto de reglas para así agruparlas mejor
        // TODO: Hacer posible que las reglas "estáticas" que no vayan a mutar durante el juego, se puedan guardar en caché para mejorar rendimiento
        // TODO: Usar carga asíncrona opcional para largas reglas
        
        // ==================== VARIABLES ===================
        #region Public Variables
        [field:SerializeField] public GameRuleValidation Validation { get; private set; }
        [SerializeReference, SubclassSelector] public IGameRule[] Rules;
        #endregion
        
        #region Private Variables

        private List<IGameRule> currentRules;
        private bool hasDefaultRules = false;
        #endregion

        // ==================== START ====================
        public GameRuleList() {
            Validation = GameRuleValidation.AllTrue;
            Rules = new IGameRule[0];

            currentRules = new();
            hasDefaultRules = false;
        }

        public GameRuleList(GameRuleList _list) {
            Validation = _list.Validation;
            Rules = _list.Rules.ToArray();

            currentRules = _list.currentRules.ToList();
            hasDefaultRules = _list.hasDefaultRules;
        }
            
        // ==================== METHODS ====================

        /// <summary> Intenta añadir las reglas que se encuentran serializadas en el inspector </summary>
        private void TryAddDefaultRules() {
            if (Rules.IsNullOrEmpty()) return;
            if (hasDefaultRules && !currentRules.IsNullOrEmpty()) return;

            hasDefaultRules = true;
            currentRules ??= new();

            for (int i = 0; i < Rules.Length; i++) {
                currentRules.Add(Rules[i]);
            }
        }
        
        // =================================================

        /// <summary> Añade una regla </summary>
        public void AddRule(IGameRule _rule) {
            currentRules ??= new();
            if (!currentRules.Contains(_rule)) currentRules.Add(_rule);
        }

        /// <summary> Añade un conjunto de reglas </summary>
        public void AddRules(IGameRule[] _rules) {
            currentRules ??= new();

            for (int i = 0; i < _rules.Length; i++) {
                if (!currentRules.Contains(_rules[i])) currentRules.Add(_rules[i]);
            }
        }

        /// <summary> Elimina una regla </summary>
        public void RemoveRule(IGameRule _rule) {
            if (currentRules != null && currentRules.Contains(_rule)) currentRules.Remove(_rule);
        }

        /// <summary> Actualiza los parámetros de todas las reglas </summary>
        public void UpdateParameters(GameRuleParameters _parameters) {
            TryAddDefaultRules();

            if (currentRules.IsNullOrEmpty()) return;

            for (int i = 0; i < currentRules.Count; i++) {
                currentRules[i].UpdateParameters(_parameters);
            }
        }

        // =================================================

        public bool ValidateRules { get {
            // Intentamos añadir las reglas por defecto si no estan añadidas aun
            TryAddDefaultRules();

            // Comprobamos si hay alguna regla, si no hay ninguna, validamos correctamente
            if (currentRules.IsNullOrEmpty()) return true;
            
            switch (Validation) {
                // Devuelve true si todas las reglas se pueden validar
                case GameRuleValidation.AllTrue: {
                    for (int i = 0; i < currentRules.Count; i++) {
                        if (!currentRules[i].Validate()) return false;
                    }

                    return true;
                }

                // Devuelve true si al menos una regla se puede validar
                case GameRuleValidation.OneTrue: {
                    for (int i = 0; i < currentRules.Count; i++) {
                        if (currentRules[i].Validate()) return true;
                    }

                    return false;
                }

                // Devuelve true si ninguna regla se puede validar
                case GameRuleValidation.NoneTrue: {
                    for (int i = 0; i < currentRules.Count; i++) {
                        if (currentRules[i].Validate()) return false;
                    }

                    return true;
                }

                // Devuelve true si todas las reglas no se pueden validar
                case GameRuleValidation.AllFalse: {
                    for (int i = 0; i < currentRules.Count; i++) {
                        if (currentRules[i].Validate()) return false;
                    }

                    return true;
                }
                
                // Devuelve true si al menos una regla no se puede validar
                case GameRuleValidation.OneFalse: {
                    for (int i = 0; i < currentRules.Count; i++) {
                        if (!currentRules[i].Validate()) return true;
                    }

                    return false;
                }

                default: return false;
            }
        }}
    }
}