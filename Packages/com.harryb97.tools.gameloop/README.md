# Game Loop

Loops con nombre (`Update`, `FixedUpdate` y `LateUpdate`) a los que se puede suscribir cualquier clase sin ser `MonoBehaviour`. Cada loop se puede pausar por separado.

## Instalación

```sh
openupm add com.harryb97.tools.gameloop
```

## Uso

```csharp
using HarryTools.GameLoop;

// Una vez al arrancar: crea el GameObject "Loop Manager" (DontDestroyOnLoad)
LoopSystem.Initialize();

ILoopable gameplay = new Loop("Gameplay");
LoopSystem.AddLoop(gameplay);

gameplay.AddUpdateListener(OnUpdate);
gameplay.AddFixedUpdateListener(OnFixedUpdate);
gameplay.AddLateUpdateListener(OnLateUpdate);

gameplay.IsPaused = true;               // Deja de llamar a sus listeners

gameplay.RemoveUpdateListener(OnUpdate);
LoopSystem.RemoveLoop("Gameplay");

private void OnUpdate() { }             // LoopSystem.LoopDelegate: void sin parámetros
```

`Loop` implementa los métodos `Add...Listener` y `Remove...Listener` de forma explícita, así que la variable tiene que ser de tipo `ILoopable`. Los loops registrados están en `LoopSystem.Loops`, indexados por `LoopID`.

## Limitaciones

- Hay que llamar a `LoopSystem.Initialize()` antes de `AddLoop`. Llamarlo otra vez vacía los loops y crea otro "Loop Manager".
- `AddLoop` ignora sin aviso un loop con un `LoopID` ya registrado.
- No se pueden añadir ni quitar loops desde un listener: se modificaría el diccionario mientras se recorre.
