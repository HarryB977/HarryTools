namespace HarryTools.Interactions {
    /// <summary> Lo que ocurre al ejecutar una interacción </summary>
    public interface IInteractionResponse {
        /// <summary> La respuesta sigue en marcha (por ejemplo, una lista de acciones que espera) </summary>
        bool IsRunning { get; }

        void Respond(Interactable _source, IInteractor _interactor);
    }
}
