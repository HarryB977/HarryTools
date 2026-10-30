using System;

namespace HarryTools.DomainReload {
    /// <summary> Ejecuta un método estático sin parámetros al entrar en el Modo Play cuando la recarga de dominio está desactivada </summary>
    /// <example> [ExecuteOnReload] private static void ResetCache() { } </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ExecuteOnReloadAttribute : Attribute { }
}
