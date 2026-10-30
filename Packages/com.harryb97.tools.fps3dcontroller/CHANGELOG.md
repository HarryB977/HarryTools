# Change Log:

## 2.0.0

### Cambios incompatibles

    - 'MovementController.Ticks' y 'CameraController.Ticks' reciben ahora el deltaTime. El movimiento debe llamarse desde 'FixedUpdate' con 'Time.fixedDeltaTime'
    - 'IMovementHandler': 'JumpInput', 'CrouchInput' y 'SprintInput' se sustituyen por 'JumpHeld', 'CrouchHeld', 'LastJumpPressTime', 'LastDashPressTime' y 'Orientation'
    - 'ICameraHandler': se eliminan 'Transform' y 'Rigidbody'. 'LookInput' es el delta del ratón en píxeles de ese frame
    - La sensibilidad del ratón ya no se escala por deltaTime (valor por defecto 0.1 en vez de 25). Se elimina 'snap'
    - La rotación horizontal se aplica a 'Head' en vez de al Transform del jugador
    - Se elimina el sprint ('canSprint', 'sprintSpeed', 'sprintFov', 'sprintBobMultiplier') y los flags 'canJump', 'canCrouch' y 'canSlide'
    - El ground check pasa de 'groundCheckRadius' y 'groundCheckOffset' a 'groundCheckDistance'

### Movimiento

    - Feat: Movimiento estilo Source (fricción, aceleración en suelo y en aire con air strafing) en la nueva clase estática 'SourceMovement'
    - Feat: Saltos en el aire ('airJumps'), buffer de salto ('jumpBufferTime'), bunnyhop automático opcional y salto por altura ('jumpHeight') en vez de fuerza
    - Feat: Dash con cargas ('dashes', 'dashSpeed')
    - Feat: Escalones ('stepHeight') y ángulo máximo de suelo ('maxGroundAngle')
    - Feat: El deslizamiento depende de la velocidad ('slideStartSpeed', 'slideFriction', 'slideStopSpeed') en vez de una duración fija
    - Feat: Agacharse en el aire recoge los pies en vez de bajar la cabeza
    - Fix: El ground check en bordes de plataformas ya no da falsos positivos ni negativos

### Cámara

    - Feat: El FOV aumenta con la velocidad por encima de la máxima ('maxSpeedFovBoost')
    - Feat: Inclinación lateral al moverse de lado ('strafeRollAngle', 'rollReturnSpeed')
    - Feat: El balanceo de cabeza sigue la velocidad y aparece y desaparece suavemente ('bobFadeSpeed')
    - Feat: Transición suave de altura al agacharse ('heightChangeSpeed')
    - Feat: Nuevo método 'AddLookOffset' para desplazar la vista, por ejemplo con el retroceso de un arma

## 1.0.0

    - Primera versión