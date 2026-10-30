namespace HarryTools.Conditions {
    /// <summary>
    /// Comprobación síncrona y sin efectos secundarios. Para esperar a que algo ocurra, usa la acción 'Wait Until'.
    /// Las implementaciones deben ser [Serializable] para mostrarse en el desplegable de [TypeSelector].
    /// </summary>
    public interface ICondition {
        bool Check(LogicContext _context);
    }
}
