namespace HarryTools.ServiceLocator {
    /// <summary> Interfaz opcional para que un servicio sepa cuándo un 'ServiceBinding' le asigna o le retira una referencia </summary>
    public interface IServiceBindingCallbacks {
        void OnBind(string _fieldName);
        void OnUnbind(string _fieldName);
    }
}
