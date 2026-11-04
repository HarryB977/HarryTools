# State Machine

Máquina de estados en C# con estados, transiciones con condición y transiciones desde cualquier estado.

## Instalación

```sh
openupm add com.harryb97.tools.statemachine
```

## Uso

```csharp
using HarryTools.StateMachineTool;

public class IdleState : BaseState {
    public override void OnEnter() { }
}

public class MoveState : BaseState {
    public override void Ticks() { }        // desde Update
    public override void FixedTicks() { }   // desde FixedUpdate
    public override void OnExit() { }
}
```

```csharp
private StateMachine stateMachine;

private void Start() {
    IdleState _idle = new();
    MoveState _move = new();

    stateMachine = new StateMachine()
        .AddTransition(_idle, _move, new FuncPredicate(() => IsMoving))
        .AddTransition(_move, _idle, new FuncPredicate(() => !IsMoving))
        .SetState(_idle);

    stateMachine.OnChangeState += _state => Debug.Log(_state.GetType().Name);
}

private void Update() => stateMachine.Ticks();
private void FixedUpdate() => stateMachine.FixedTicks();
```

`AddAnyTransition(_to, _condition)` añade una transición que se comprueba desde cualquier estado.

Para condiciones o estados propios se pueden implementar `IPredicate` (`bool Evaluate()`) e `IState` directamente.

## Funcionamiento

- `Ticks()` comprueba primero las transiciones desde cualquier estado y después las del estado actual. Aplica la primera cuya condición se cumpla y luego llama a `Ticks()` del estado actual.
- `FixedTicks()` solo llama a `FixedTicks()` del estado actual, no comprueba transiciones.
- Al cambiar de estado: `OnExit()` del anterior, `OnEnter()` del nuevo y el evento `OnChangeState`. Una transición al estado actual no hace nada.
- `SetState()` llama a `OnEnter()` y al evento `OnSetState`, sin llamar a `OnExit()` del estado anterior.

## Limitaciones

- Los estados se identifican por su tipo: solo puede haber una instancia de cada clase de estado.
- `SetState()` solo acepta estados ya añadidos con `AddTransition` o `AddAnyTransition`, y hay que llamarlo antes del primer `Ticks()`.
- Las transiciones se guardan en un `HashSet`: si varias se cumplen a la vez, el orden entre ellas no está garantizado.

## Sample

**State Machine Sample**, importable desde Package Manager > Samples: un `Player` con `Rigidbody` y los estados `IdleState` y `MovementState`. El GameObject se renombra con el estado actual. Usa el Input Manager clásico.
