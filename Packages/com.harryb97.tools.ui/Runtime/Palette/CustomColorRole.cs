using System;
using UnityEngine;

namespace HarryTools.UI {
    /// <summary> Rol de color añadido por el usuario a una paleta. Se referencia por su id, que no cambia al renombrarlo </summary>
    [Serializable]
    public class CustomColorRole {
        [HideInInspector] public int id;
        public string name;
        public Color color = Color.white;
        [Tooltip("Color para el texto o el contenido que va encima de este")]
        public Color contrast = Color.black;
    }
}
