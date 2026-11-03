using System;
using UnityEngine;

namespace HarryTools.UI {
    /// <summary> Rol de color añadido por el usuario a una paleta. Se referencia por su id, que no cambia al renombrarlo </summary>
    [Serializable]
    public class CustomColorRole {
        [HideInInspector] public int id;
        public string name;
        public Color color = Color.white;
        [Tooltip("Color for text or content drawn on top of this color.")]
        public Color contrast = Color.black;
    }
}
