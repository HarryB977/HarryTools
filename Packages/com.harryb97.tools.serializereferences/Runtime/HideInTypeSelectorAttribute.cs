using System;

namespace HarryTools.SerializeReferences {
    /// <summary> Oculta el tipo en el desplegable de [TypeSelector] </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HideInTypeSelectorAttribute : Attribute { }
}
