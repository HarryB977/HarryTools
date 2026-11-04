# Change Log:

## 1.0.1

    - Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.platformer3dcontroller` y el Package Manager avisa de las actualizaciones
    - `package.json`: añadido `repository` y el autor ya no incluye email
    - LICENSE: el titular pasa a ser HarryB97

## 1.0.0

    - Primera versión. Adaptación del 'stylised-character-controller' de Joe Binns (rama 'optimisation/refactor'), basado en la cápsula flotante de Toyful Games

### Estructura

    - Mismo esquema que 'fps3dcontroller': 'MovementController' es una clase C# con 'Data' serializable, se crea con un 'IMovementHandler' y se llama a 'Ticks(Time.fixedDeltaTime)' desde 'FixedUpdate'
    - El Runtime no depende de Input System, Cinemachine ni URP. La entrada y la orientación de la cámara las da el handler
    - Se eliminan los singletons ('PlayerController', 'InputManager') y los componentes repartidos (levitator, erector, gravity, jump, followers...)
    - 'GroundCheck' es una clase reutilizable (el Sample la usa también para la sombra)
    - 'MovingPlatform' marca las plataformas que arrastran al jugador
    - Eventos 'OnJump', 'OnGrounded' y 'OnUngrounded'

### Mejoras respecto al original

    - El ground check ignora los colliders del propio rigidbody, ya no hace falta una capa para el jugador
    - Se rechaza como suelo cualquier superficie con más inclinación que 'maxGroundAngle'
    - Salto: buffer de entrada ('jumpBufferTime') y coyote time ('coyoteTime')
    - Salto: la altura ya no depende de la masa (antes se usaba la fuerza de la gravedad en vez de su aceleración) y conserva la velocidad horizontal (antes se perdía)
    - Si un salto no llega a despegar, la levitación vuelve tras 'failedJumpCooldown'. Antes se quedaba desactivada y el jugador caía
    - El amortiguador de la levitación usa la velocidad relativa al suelo, también sobre rigidbodies en movimiento
    - Levitación, movimiento y torque de erguirse son aceleraciones: los valores no dependen de la masa ni del tamaño del collider
    - Las plataformas móviles funcionan con cualquier forma de moverlas (animación, script o rigidbody kinematic), no dependen del orden de ejecución y solo transmiten el giro en yaw
    - Al dejar una plataforma móvil se hereda su velocidad ('inheritPlatformVelocity') y en el aire sin input se conserva el impulso ('keepAirMomentum')
    - El suelo con rigidbody recibe también el peso del jugador ('pushGroundRigidbodies')
    - Multiplicador de gravedad al caer ('fallGravityMultiplier') y control aéreo ('airControl')
    - Se eliminan los eventos que nunca se desuscribían

### Sample

    - Escena con escalones, rampas, bloques, plataforma móvil, ascensor, plataforma giratoria, cajas con físicas y balancín
    - Cámara orbital con zoom y colisión, sombra blob, polvo al saltar, aterrizar y caminar, y squash & stretch
    - Funciona con Input System y con el Input Manager clásico. Los materiales son de URP
    - Texturas de prototipo de Kenney (CC0)
