using System;

namespace HarryTools.ServiceLocator {
    /// <summary> Marca un campo de un servicio como destino de un 'ServiceBinding'. El campo debe ser de un tipo derivado de 'UnityEngine.Object' </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ServiceBindableAttribute : Attribute { }
}
