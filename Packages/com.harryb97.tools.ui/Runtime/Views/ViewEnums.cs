namespace HarryTools.UI {
    /// <summary> Cuándo se crea y se destruye la instancia de una vista </summary>
    public enum ViewLifetime {
        /// <summary> Se instancia la primera vez que se muestra y se conserva al ocultarse </summary>
        Cached,
        /// <summary> Se instancia al inicializar el UIRoot (o con PreloadAsync) y no se destruye nunca </summary>
        Preloaded,
        /// <summary> Se instancia al mostrarse y se destruye al ocultarse </summary>
        Transient
    }

    public enum ViewState { Hidden, Entering, Visible, Exiting }

    public enum UILayerKind { Views, Popups, Overlays }
}
