# Platformer 3D Controller

Controlador de movimiento 3D basado en físicas para juegos de plataformas, con el enfoque de cápsula flotante: un muelle mantiene el `Rigidbody` a una altura fija sobre el suelo, otro lo mantiene erguido y mirando hacia donde se mueve, y el movimiento horizontal es una aceleración limitada hacia una velocidad objetivo.

El Runtime no depende de Input System, Cinemachine ni URP. La entrada y la orientación de la cámara las da quien implementa `IMovementHandler`.

## Instalación

```sh
openupm add com.harryb97.tools.platformer3dcontroller
```

## Montaje

- **Rigidbody y collider** en el jugador (el Sample usa una `SphereCollider` de radio 0.5, con Interpolate y sin constraints). El controlador pone `useGravity = false` al crearse y aplica su propia gravedad: `Physics.gravity * gravityMultiplier`. No congeles la rotación: el muelle de erguirse es quien la controla.
- **Capas**: no hace falta una capa para el jugador. El ground check ignora los colliders de su propio `Rigidbody`. `groundCheckMask` decide qué cuenta como suelo.
- **Ground check**: se lanza desde la posición del `Rigidbody` hacia abajo. `groundCheckDistance` se mide desde ahí y tiene que ser mayor que `rideHeight`.
- **Orientación**: la entrada es relativa al yaw de `Orientation` (normalmente la cámara).
- **Plataformas móviles**: añade `MovingPlatform` a la raíz de la plataforma. Vale para cualquiera de sus colliders hijos y para cualquier forma de moverla (animación, script o rigidbody kinematic). El jugador sigue su posición y solo su giro en yaw.
- **Rigidbodies bajo los pies**: si no son kinematic reciben el peso del jugador y el impulso del salto (`pushGroundRigidbodies`).
- **Arriba** es `-Physics.gravity`. Si cambia la gravedad del mundo, cambia también la dirección en la que se mantiene erguido.

## Uso

`MovementController` es una clase C#. Se crea con un `IMovementHandler` y se llama a `Ticks` desde `FixedUpdate`:

```csharp
using UnityEngine;
using HarryTools.Platformer3DController;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour, IMovementHandler {
    [SerializeField] private MovementController.Data movementData;
    [SerializeField] private Transform orientation;   // normalmente la cámara

    private Rigidbody rB;
    private float lastJumpPressTime = float.NegativeInfinity;

    public MovementController Movement { get; private set; }

    private void Awake() {
        TryGetComponent(out rB);
        Movement = new MovementController(this);
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) lastJumpPressTime = Time.time;
    }

    private void FixedUpdate() => Movement.Ticks(Time.fixedDeltaTime);
    private void OnDrawGizmosSelected() => Movement?.ShowGizmos();

    // IMovementHandler
    Vector2 IMovementHandler.MovementInput => new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    bool IMovementHandler.SprintHeld => Input.GetKey(KeyCode.LeftShift);
    float IMovementHandler.LastJumpPressTime => lastJumpPressTime;
    Transform IMovementHandler.Orientation => orientation;
    Rigidbody IMovementHandler.Rigidbody => rB;
    MovementController.Data IMovementHandler.Data => movementData;
}
```

El ejemplo usa el Input Manager clásico para abreviar. Cualquier fuente de entrada vale.

### IMovementHandler

| Miembro | Qué es |
|---|---|
| `MovementInput` | Dirección de movimiento (se limita a magnitud 1), relativa a `Orientation` |
| `SprintHeld` | Mientras es `true` se usa `sprintSpeed` |
| `LastJumpPressTime` | `Time.time` de la última pulsación de salto, `float.NegativeInfinity` si nunca se ha pulsado |
| `Orientation` | Transform cuyo yaw orienta la entrada |
| `Rigidbody` | Rigidbody del jugador |
| `Data` | Configuración (`MovementController.Data`) |

### Estado y eventos

- `IsGrounded`, `IsJumping`, `IsSprinting`, `IsLevitating` (en el suelo y sin saltar).
- `Velocity`, `HorizontalSpeed`, `MaxSpeed`, `FacingDirection`, `Up`.
- `Ground` (el `GroundCheck` usado) y `Platform` (el `MovingPlatform` que pisa, o `null`).
- Eventos `OnJump`, `OnGrounded` y `OnUngrounded`.

```csharp
Movement.OnJump += OnJump;
Movement.OnGrounded += OnGrounded;

// En OnDestroy
Movement.OnJump -= OnJump;
Movement.OnGrounded -= OnGrounded;
```

### GroundCheck

Sphere cast hacia el suelo que ignora los colliders de un `Rigidbody`. Se puede usar por separado, por ejemplo para una sombra:

```csharp
private readonly GroundCheck ground = new();

if (ground.Check(body.position, -Movement.Up, 0.25f, 12f, ~0, body)) {
    shadow.position = ground.Point;   // también Distance, Hit, IsGrounded
}
```

`Check(origin, direction, radius, maxDistance, mask, ignored = null, maxGroundAngle = 90f)`. `maxDistance` se mide desde el origen, no desde el borde de la esfera. Las superficies con más inclinación que `maxGroundAngle` no cuentan como suelo.

## Configuración (`MovementController.Data`)

| Grupo | Campos | Notas |
|---|---|---|
| Ground Check | `groundCheckRadius`, `groundCheckDistance`, `groundCheckMask`, `maxGroundAngle` | `groundCheckDistance` > `rideHeight` |
| Levitation | `rideHeight`, `rideStiffness`, `rideDamper` | Muelle de altura. El amortiguador usa la velocidad relativa al suelo |
| Movement | `speed`, `sprintSpeed`, `acceleration`, `maxAcceleration`, `accelerationCurve`, `lean`, `airControl`, `keepAirMomentum` | `accelerationCurve` multiplica la aceleración según el producto escalar entre la entrada y la velocidad actual (por defecto, girar en contra acelera el doble). `lean` es la altura sobre el centro donde se aplica la fuerza e inclina el cuerpo. Con `keepAirMomentum`, en el aire sin entrada se conserva la velocidad horizontal |
| Upright | `uprightStiffness`, `uprightDamper` | Muelle de rotación |
| Gravity | `gravityMultiplier`, `fallGravityMultiplier` | El segundo se aplica además al caer |
| Jump | `jumpHeight`, `jumpBufferTime`, `coyoteTime`, `failedJumpCooldown` | La altura no depende de la masa. El salto conserva la velocidad horizontal. Si un salto no despega, la levitación vuelve tras `failedJumpCooldown` |
| Platforms | `inheritPlatformVelocity`, `pushGroundRigidbodies` | Al dejar una plataforma se hereda su velocidad |

Levitación, movimiento y torque son aceleraciones: los valores no dependen de la masa ni del tamaño del collider. Mientras levita no se aplica gravedad, el muelle sostiene al jugador.

## Sample

**Player Movement Sample**, importable desde Package Manager > Samples. Incluye:

- Escena con escalones, rampas, bloques, plataforma móvil, ascensor, plataforma giratoria, cajas con físicas y balancín.
- `Player`: implementación de `IMovementHandler`.
- `SampleInput`: teclado y ratón con Input System o con el Input Manager clásico. WASD o flechas para moverse, Espacio para saltar, Shift izquierdo para correr.
- `OrbitCamera`: cámara orbital con el ratón, zoom con la rueda y colisión con paredes. Esc libera el cursor y el clic izquierdo lo vuelve a bloquear.
- `BlobShadow`, `PlayerDust` (polvo al saltar, aterrizar y caminar), `SquashAndStretch` y `PlatformMover`.

Los materiales del Sample son de URP.

## Créditos

Adaptación del [stylised-character-controller](https://github.com/joebinns/stylised-character-controller) de Joe Binns (MIT), basado en la cápsula flotante de Toyful Games. Texturas de prototipo del Sample de Kenney (CC0).
