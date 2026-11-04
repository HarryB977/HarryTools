# Platformer 2D Controller

Controlador de movimiento 2D para plataformas basado en `Rigidbody2D`: andar sobre pendientes y escalones, salto con buffer y coyote time, saltos en el aire, dash, agacharse, paredes, escaleras y plataformas móviles. Depende de `com.harryb97.harryutils` y `com.harryb97.tools.logger`.

## Instalación

```sh
openupm add com.harryb97.tools.platformer2dcontroller
```

## Uso

`MovementController` es abstracto: se hereda, se le dan los datos y la entrada, y se llama a sus métodos desde el loop.

```csharp
using UnityEngine;
using HarryTools.Platformer2DController;

[System.Serializable]
public class PlayerData : ControllerData { }

public class PlayerMovement : MovementController {
    [SerializeField] private PlayerData data;

    protected override ControllerData Data => data;

    protected override Vector2 MoveInput => new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    protected override bool JumpDownInput => Input.GetKeyDown(KeyCode.Space);
    protected override bool JumpHeldInput => Input.GetKey(KeyCode.Space);
    protected override bool DashDownInput => Input.GetKeyDown(KeyCode.X);

    private void Start() => InitializeController();
    private void Update() => MovementTicks();             // Lee la entrada de salto y dash
    private void FixedUpdate() => MovementFixedTicks();   // Física y movimiento
}
```

- `ControllerData` también es abstracto y no es un `ScriptableObject`. En el sample se guarda dentro de uno (`PlayerConfig`) para compartirlo entre prefabs.
- `UpdateController()` vuelve a generar los colliders a partir de `ControllerSize`. `InitializeController()` ya lo llama; úsalo si cambias el tamaño en ejecución.
- La entrada no depende de ningún sistema concreto: cualquier fuente sirve para las propiedades abstractas.

## Componentes

`[RequireComponent]` añade `Rigidbody2D`, `BoxCollider2D`, `CapsuleCollider2D` y `ConstantForce2D`.

- Los dos colliders se dimensionan solos con `ControllerSize` y quedan como no editables en el inspector. El `BoxCollider2D` se usa en el suelo (de pie o agachado) y el `CapsuleCollider2D` en el aire.
- Ambos colliders usan el material físico asignado al `Rigidbody2D`.
- La gravedad es la de *Physics 2D*: el controlador pone `gravityScale` a 1 en el aire y a 0 en el suelo. En el aire suma `ExtraConstantGravity` mediante el `ConstantForce2D`.

## Capas

Las capas se escriben por nombre en el `ControllerData`:

- `collisionLayersMask`: lista de capas que cuentan como suelo y techo. Si una no existe se avisa por consola.
- `climbLayerMask`: capa de las paredes en las que se puede agarrar.
- `ladderLayerMask`: capa de las escaleras. Se detectan también si son triggers.
- `playerLayerMask`: se expone como `PlayerLayerMask`, el controlador no la usa.

## Configuración (`ControllerData`)

**Settings**

| Campo | Descripción |
|---|---|
| `PositionCorrection` | Cómo se ajusta la altura sobre el suelo: `Velocity` (con velocidad) o `Immediate` (con `MovePosition`). |
| `VerticalDeadZoneThreshold` | Entrada vertical mínima para agacharse y subir o bajar escaleras. |
| `HorizontalDeadZoneThreshold` | Entrada horizontal mínima para contar como empuje contra una pared. |
| `ControllerSize` | `Height`, `Width`, `StepHeight` (altura de escalón que se sube sin saltar), `CrouchHeight` y `RayInset` (margen de los rayos de suelo respecto al borde). En el Editor, `StepHeight` se limita a `Height - 0.05` y `CrouchHeight` a un mínimo de `StepHeight + 0.05`. |

**Movement**

| Campo | Descripción |
|---|---|
| `BaseSpeed` | Velocidad horizontal máxima. |
| `Acceleration` / `Friction` | Cambio de velocidad por segundo con entrada y sin ella. |
| `AirFrictionMultiplier` | Multiplica aceleración y fricción en el aire. |
| `DirectionCorrectionMultiplier` | Multiplica la aceleración al cambiar de sentido. |
| `MaxWalkableSlope` | Pendiente máxima en grados que cuenta como suelo. |

**Jump**

| Campo | Descripción |
|---|---|
| `ExtraConstantGravity` | Fuerza hacia abajo añadida en el aire. |
| `BufferedJumpTime` | Tiempo que se recuerda una pulsación de salto antes de tocar el suelo. |
| `CoyoteTime` | Tiempo para saltar después de dejar el suelo. |
| `JumpPower` | Impulso vertical del salto. |
| `EndJumpEarlyExtraForceMultiplier` | Multiplica `ExtraConstantGravity` si se suelta el salto mientras se sube. |
| `MaxAirJumps` | Saltos en el aire. Se recargan al tocar el suelo, al soltarse de una pared y al entrar o salir de una escalera. |

**Dash** (`AllowDash`)

| Campo | Descripción |
|---|---|
| `DashVelocity` / `DashDuration` / `DashCooldown` | Velocidad, duración y tiempo de espera. |
| `DashEndHorizontalMultiplier` | Multiplica la velocidad horizontal al terminar. |

La dirección sale de `MoveInput` (nunca hacia abajo). No hay dash sin entrada ni estando agachado. Se recarga al tocar el suelo.

**Crouch** (`AllowCrouching`)

| Campo | Descripción |
|---|---|
| `CrouchSlowDownTime` | Tiempo hasta llegar a la velocidad de agachado. |
| `CrouchSpeedModifier` | Multiplicador de velocidad agachado. |

Solo se agacha en el suelo. No se levanta (ni salta) si no hay hueco para estar de pie.

**Walls** (`AllowWallJump`)

| Campo | Descripción |
|---|---|
| `RequireInputPush` | Hay que empujar hacia la pared para quedarse agarrado. |
| `WallJumpPower` | Impulso del salto si se está agarrado o empujando contra la pared. |
| `WallPushPower` | Impulso del salto en los demás casos. |
| `WallJumpTotalInputLossTime` / `WallJumpInputLossReturnTime` | Tras saltar desde la pared, tiempo sin control hacia ella y tiempo hasta recuperarlo del todo. |
| `WallClimbSpeed` | Velocidad vertical con entrada vertical, y velocidad máxima de caída sin ella. |
| `WallFallAcceleration` | Aceleración de caída agarrado. |
| `WallPopForce` | Impulso vertical al soltarse mientras se sube. |
| `WallCoyoteTime` | Tiempo para saltar después de soltarse. |
| `WallDetectorRange` | Distancia de detección de la pared. |

Solo se agarra a la pared cayendo.

**Ladders** (`AllowLadders`)

| Campo | Descripción |
|---|---|
| `AutoAttachToLadders` | Se sube a la escalera al tocarla. Si no, hace falta entrada vertical. |
| `SnapToLadders` | Sin entrada horizontal, se centra en la X de la escalera. |
| `LadderCooldownTime` | Tiempo antes de poder volver a subirse. |
| `LadderSnapTime` | Tiempo de suavizado del centrado. |
| `LadderPopForce` | Impulso vertical al salir de la escalera subiendo. |
| `LadderClimbSpeed` / `LadderSlideSpeed` | Velocidad al subir y al bajar. |
| `LadderShimmySpeedMultiplier` | Multiplica `Acceleration` para el movimiento lateral en la escalera. |

**Moving Platforms**

| Campo | Descripción |
|---|---|
| `NegativeYVelocityNegation` | Multiplica la velocidad vertical negativa heredada al dejar una plataforma. |
| `ExternalVelocityDecayRate` | Ritmo al que se pierde la velocidad heredada. |

## API

```csharp
movement.OnJump += type => { };                      // JumpType: Jump, Coyote, AirJump, WallJump
movement.OnGroundedChanged += (grounded, lastY) => { };
movement.OnDashChange += (dashing, direction) => { };
movement.OnWallGrabChanged += grabbed => { };
movement.OnRepositioned += position => { };
movement.OnToggledPlayer += active => { };

movement.AddFrameForce(new Vector2(0, 15f), _resetVelocity: true);   // Impulso que se aplica en el siguiente MovementFixedTicks
movement.RepositionImmediately(spawnPoint, _resetVelocity: true);
movement.TogglePlayer(false);                                       // Rigidbody2D Kinematic y sin movimiento

MovementController.MovementState state = movement.CurrentState;    // Posición, rotación, velocidad, en suelo
movement.LoadState(state);
```

Propiedades de solo lectura: `IsActive`, `IsCrouching`, `IsClimbingLadder`, `Velocity`, `GroundNormal`, `WallDirection`, `UpVector`, `RightVector`.

## Plataformas y modificadores

- **`IPhysicsObjectMove`**: objetos que mueven al personaje. Se aplican al estar encima (el componente debe estar en el collider que golpean los rayos de suelo) o al entrar en su trigger si `RequireGrounding` es `false`. Su `FramePositionDelta` se suma al movimiento y su `TakeOffVelocity` se hereda al dejar la plataforma, si `UsesBounding` es `false`.
- **`ISpeedModifier`**: triggers que suman `Modifier` a un multiplicador de velocidad (base `Vector2.one`), en el suelo (`OnGround`), en el aire (`InAir`) o en ambos.

`OnTriggerEnter2D` y `OnTriggerExit2D` son `protected virtual`. Si se sobrescriben, hay que llamar a `base`.

## Sample

*Player Movement Sample*, importable desde Package Manager > Samples: escena de prueba, prefab de jugador, plataforma que oscila (`SinPlatform`) y un `Player Config`.

- La entrada usa el Input Manager antiguo (`Input.GetAxisRaw`, Espacio para saltar, X para el dash).
- El config usa las capas `Player`, `Climbable` y `Ladder`, que hay que crear en el proyecto.
- Añade la entrada **Assets > Create > HarryTools > Platformer 2D Controller > Sample > Player Config**.
