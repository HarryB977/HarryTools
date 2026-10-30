using System.Threading;

using HarryTools.Conditions;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    /// <summary>
    /// Pieza de lógica ejecutable desde el inspector. Puede ser instantánea o durar varios frames.
    /// Las implementaciones deben ser [Serializable] para mostrarse en el desplegable de [TypeSelector].
    /// Si la acción espera, debe respetar el 'CancellationToken' (pasándolo a UniTask o comprobándolo en cada frame).
    /// </summary>
    public interface IAction {
        UniTask Run(LogicContext _context, CancellationToken _token);
    }
}
