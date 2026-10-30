# Actions

Acciones modulares asíncronas configurables desde el inspector. Depende de `com.harryb97.tools.serializereferences`, `com.harryb97.tools.conditions` y **UniTask** (debe estar instalado en el proyecto).

## Uso

```csharp
using HarryTools.Actions;
using HarryTools.Conditions;

[SerializeField] private ActionList onOpen;           // Condiciones + lista de acciones

onOpen.Run(this);                                     // Sin esperar. Se cancela al destruir el componente
ActionHandle _handle = onOpen.Run(this, _otherObject); // Con 'Target'
_handle.Cancel();

bool _executed = await onOpen.RunAsync(LogicContext.From(this), destroyCancellationToken);
```

O sin código: componente **Action Runner** (*Add Component > HarryTools > Action Runner*), que se lanza en Awake/Start/OnEnable o desde un UnityEvent (`Run`, `RunWithTarget`, `Cancel`).

## Crear una acción

```csharp
// Instantánea
[Serializable, TypeMenu("Puzzles/Reset Puzzle")]
public class ResetPuzzle : InstantAction {
    [SerializeField] private Puzzle puzzle;
    protected override void Execute(LogicContext _context) => puzzle.ResetPuzzle();
}

// Con duración: respeta siempre el token
[Serializable, TypeMenu("Dialogues/Play Dialogue")]
public class PlayDialogue : IAction {
    [SerializeField] private Dialogue dialogue;

    public async UniTask Run(LogicContext _context, CancellationToken _token) {
        dialogue.Play();
        try {
            await UniTask.WaitWhile(() => dialogue.IsPlaying, cancellationToken: _token);
        } catch (OperationCanceledException) {
            dialogue.Stop();
            throw;
        }
    }
}
```

## Conceptos

- **Una sola interfaz**: `IAction.Run(LogicContext, CancellationToken)`. No hay interfaz aparte para las cancelables.
- **`CancellationToken` estándar**: se enlaza con `destroyCancellationToken` y funciona directamente con `UniTask.Delay`, `WaitUntil`, etc.
- **Errores aislados**: si una acción lanza una excepción, se muestra en consola (con el objeto como contexto) y la lista continúa.
- **`ActionHandle`**: cada ejecución tiene su propio control (`Cancel`, `Status`, `Task`, `Finished`), en lugar de un diccionario estático.
- **Eventos**: `Invoke Unity Event` es una acción más, así el evento respeta el orden, las condiciones y la cancelación.

## Incluidas

- **Flow**: Wait, Wait Frames, Wait Until (con timeout), If/Else, Sequence, Parallel (All/Any), Repeat, Run In Background, Run/Cancel Action Runner, Stop
- **Events**: Invoke Unity Event, Invoke GameObject Event
- **GameObject**: Set Active, Set Behaviour Enabled, Destroy, Instantiate
- **Transform**: Set Transform, Tween Transform (sin librerías de tweens)
- **Audio**: Play Audio Source, Stop Audio Source, Play Clip At Point
- **Animator**: Set Parameter, Play State
- **Application**: Set Time Scale, Load Scene, Quit
- **Debug**: Log

## Migración desde Processes / Requisites

| Antes                                        | Ahora                                                 |
|----------------------------------------------|-------------------------------------------------------|
| `IProcess` + `ICancellableProcess`           | `IAction` (o `InstantAction`)                         |
| `CanExecute()`                               | Comprobación dentro de `Run` / `Execute`              |
| `IRequisite` que comprueba                   | `ICondition` / `Condition`                            |
| `IRequisite` que espera (`Wait`, etc.)       | Acción (`Wait`, `Wait Until`, ...)                    |
| `ProcessCancellationToken` / `RequisiteCancellationToken` | `System.Threading.CancellationToken`    |
| `ProcessesGroup` (requisites + processes + events) | `ActionList` (conditions + actions, con `Invoke Unity Event`) |
| `ProcessesGroup[]` en orden                  | Una `ActionList` con acciones `If` / `Wait Until`     |
| `ProcessesUtils.Stop(groups)`                | `ActionHandle.Cancel()`                               |
| `SequenceInstance` + `Play Sequence`         | `ActionRunner` + `Run Action Runner` (con `Wait` entre pasos) |
