# FPS 3D Controller

Controlador de movimiento y cámara en primera persona con movimiento estilo Source: fricción, aceleración en suelo y en aire (air strafing y bunnyhop), saltos en el aire, dash, agacharse, deslizarse y escalones.

## Instalación

```sh
openupm add com.harryb97.tools.fps3dcontroller
```

## Montaje

```
Player        Rigidbody + CapsuleCollider + tu componente (IMovementHandler, ICameraHandler)
└── Head      Transform: recibe el yaw. Su altura local es la de los ojos
    └── Camera
```

- **Pivote en los pies**. La altura inicial del `CapsuleCollider` es la altura total del jugador. El controlador reescribe `height` y `center`: la cápsula flota `stepHeight` por encima de los pies y solo choca con paredes; el ground check sostiene los pies.
- **Rigidbody**: el controlador escribe `linearVelocity` en cada tick y aplica su propia gravedad (`gravity`). Congela la rotación en X y Z (como el prefab del Sample).
- **Capas**: `groundCheckMask` se usa para el suelo y para el techo. Pon el jugador en una capa que no esté en esa máscara (en el Sample, la capa 6).
- **Cabeza y cámara**: el yaw se aplica a `Head.localRotation`; el pitch, el roll y la inclinación al deslizarse, a la cámara. Al agacharse o deslizarse `Head` baja a `CameraController.Data.crouchHeight`.
- **Orientación**: la entrada de movimiento es relativa a `Orientation` (en el Sample, `Head`).
- **Cursor**: `CameraController` lo bloquea y lo oculta al crearse.

## Uso

```csharp
using UnityEngine;
using HarryTools.FPS3DController;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class Player : MonoBehaviour, IMovementHandler, ICameraHandler {
    [SerializeField] private MovementController.Data movementData;
    [SerializeField] private Transform head;
    [SerializeField] private Camera headCamera;
    [SerializeField] private CameraController.Data cameraData;

    private MovementController movementController;
    private CameraController cameraController;
    private Rigidbody rB;
    private CapsuleCollider col;

    private float lastJumpPressTime = float.NegativeInfinity;
    private float lastDashPressTime = float.NegativeInfinity;

    private void Awake() {
        TryGetComponent(out rB);
        TryGetComponent(out col);
    }

    private void Start() {
        movementController = new MovementController(this);
        cameraController = new CameraController(this);   // después del movimiento: lo lee por ICameraHandler.Movement
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) lastJumpPressTime = Time.time;
        if (Input.GetKeyDown(KeyCode.LeftShift)) lastDashPressTime = Time.time;

        cameraController.Ticks(Time.deltaTime);
    }

    private void FixedUpdate() => movementController.Ticks(Time.fixedDeltaTime);
    private void OnDrawGizmos() => movementController?.ShowGizmos();

    // IMovementHandler
    Vector2 IMovementHandler.MovementInput => new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    bool IMovementHandler.JumpHeld => Input.GetKey(KeyCode.Space);
    bool IMovementHandler.CrouchHeld => Input.GetKey(KeyCode.LeftControl);
    float IMovementHandler.LastJumpPressTime => lastJumpPressTime;
    float IMovementHandler.LastDashPressTime => lastDashPressTime;
    Transform IMovementHandler.Transform => transform;
    Transform IMovementHandler.Orientation => head;
    Rigidbody IMovementHandler.Rigidbody => rB;
    CapsuleCollider IMovementHandler.Collider => col;
    MovementController.Data IMovementHandler.Data => movementData;

    // ICameraHandler
    Vector2 ICameraHandler.MovementInput => new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    Vector2 ICameraHandler.LookInput => new(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
    Transform ICameraHandler.Head => head;
    Camera ICameraHandler.Camera => headCamera;
    MovementController ICameraHandler.Movement => movementController;
    CameraController.Data ICameraHandler.Data => cameraData;
}
```

El movimiento va en `FixedUpdate` con `Time.fixedDeltaTime` y la cámara en `Update` con `Time.deltaTime`.

### Handlers

| `IMovementHandler` | Qué es |
|---|---|
| `MovementInput` | Dirección de movimiento, relativa a `Orientation` |
| `JumpHeld` | Solo se usa con `autoBunnyhop`: salta al tocar el suelo mientras está pulsado |
| `CrouchHeld` | Agacharse, y deslizarse si se va lo bastante rápido |
| `LastJumpPressTime`, `LastDashPressTime` | `Time.time` de la última pulsación, `float.NegativeInfinity` si nunca se ha pulsado |
| `Transform`, `Orientation`, `Rigidbody`, `Collider` | Referencias |
| `Data` | `MovementController.Data` |

| `ICameraHandler` | Qué es |
|---|---|
| `MovementInput` | Para el roll al moverse de lado |
| `LookInput` | Delta del ratón de ese frame. No se escala por deltaTime |
| `Head`, `Camera` | Referencias |
| `Movement` | El `MovementController` (para FOV, balanceo y altura) |
| `Data` | `CameraController.Data` |

### Estado

`MovementController` expone `IsGrounded`, `IsCrouching`, `IsSliding`, `BelowCeiling`, `HorizontalSpeed`, `MaxSpeed`, `AirJumpsLeft` y `DashesLeft`. No tiene eventos.

Para desplazar la vista sin que vuelva (por ejemplo, el retroceso de un arma):

```csharp
cameraController.AddLookOffset(_pitchUp: 1.5f, _yaw: 0f);   // pitch positivo mira hacia arriba
```

`SourceMovement` es estática y se puede usar por separado: `ApplyFriction`, `Accelerate` y `AirAccelerate`.

## Configuración

### `MovementController.Data`

| Grupo | Campos | Notas |
|---|---|---|
| General | `gravity` | |
| Ground | `maxSpeed`, `groundAcceleration`, `friction`, `stopSpeed`, `maxGroundAngle` | Por debajo de `stopSpeed` la fricción se calcula como si se fuera a `stopSpeed`, así se para rápido |
| Air | `airAcceleration`, `airWishSpeedCap` | El límite solo se aplica en la dirección deseada, lo que permite ganar velocidad con air strafing |
| Jump | `jumpHeight`, `airJumps`, `jumpBufferTime`, `autoBunnyhop`, `coyoteTimeDuration` | Los saltos en el aire se recargan al tocar el suelo |
| Dash | `dashes`, `dashSpeed` | Solo en el aire. Horizontal, corta la caída, va hacia la entrada o, sin entrada, hacia delante. Usa `jumpBufferTime` como buffer. Las cargas se recargan en el suelo |
| Crouch | `crouchHeight`, `crouchSpeed`, `crouchTransitionSpeed` | Altura total agachado. Bajo un techo sigue agachado. En el aire recoge los pies en vez de bajar la cabeza |
| Slide | `slideStartSpeed`, `slideFriction`, `slideStopSpeed` | Empieza al agacharse en el suelo a `slideStartSpeed` o más. Termina al bajar de `slideStopSpeed`, soltar o dejar el suelo |
| Step | `stepHeight` | Altura de escalón que se sube sin saltar |
| Ground Check | `groundCheckDistance`, `groundCheckMask` | Margen bajo los pies para detectar el suelo en el aire |

### `CameraController.Data`

| Grupo | Campos | Notas |
|---|---|---|
| Look | `mouseSensitivity`, `verticalMaxAngle`, `crouchHeight` | `crouchHeight` es la altura local de `Head` agachado |
| FOV | `defaultFov`, `maxSpeedFovBoost`, `fovChangeSpeed` | El FOV sube al pasar de `MaxSpeed`, hasta `maxSpeedFovBoost` al doble de esa velocidad |
| Camera Bob | `bobbingAmount`, `walkBobSpeed`, `bobFadeSpeed` | Sigue la velocidad y aparece y desaparece suavemente. No hay balanceo agachado ni deslizándose |
| Height | `heightChangeSpeed` | Transición de altura al agacharse |
| Roll | `strafeRollAngle`, `rollReturnSpeed` | Inclinación al moverse de lado en el suelo |
| Slide | `slideTiltAngle` | Inclinación hacia atrás al deslizarse |

## Sample

**Player Movement Sample**, importable desde Package Manager > Samples: escena, prefab `Player` y el script del ejemplo de arriba.

Controles: WASD para moverse, ratón para mirar, Espacio para saltar, Shift izquierdo para el dash y Control izquierdo para agacharse. Usa el Input Manager clásico (`Input.GetAxisRaw`, `Input.GetKey`), así que necesita *Active Input Handling* en *Input Manager (Old)* o *Both*.
