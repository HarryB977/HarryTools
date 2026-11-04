# Game Loop

Loops con nombre (`Update`, `FixedUpdate` y `LateUpdate`) a los que se puede suscribir cualquier clase sin ser `MonoBehaviour`. Cada loop se puede pausar por separado.

## Instalación

```sh
openupm add com.harryb97.tools.gameloop
```

## Uso

```csharp
using HarryTools.GameLoop;

// Opcional: el "Loop Manager" (DontDestroyOnLoad) se crea solo con AddLoop; RemoveLoop, HasLoop y TryGetLoop nunca lo crean.
// Initialize() es idempotente: llamarlo otra vez no vacía los loops
LoopSystem.Initialize();

ILoopable gameplay = new Loop("Gameplay");
LoopSystem.AddLoop(gameplay);

gameplay.AddUpdateListener(OnUpdate);
gameplay.AddFixedUpdateListener(OnFixedUpdate);
gameplay.AddLateUpdateListener(OnLateUpdate);

gameplay.IsPaused = true;               // Deja de llamar a sus listeners

gameplay.RemoveUpdateListener(OnUpdate);
LoopSystem.RemoveLoop("Gameplay");

LoopSystem.HasLoop("Gameplay");                       // bool
LoopSystem.TryGetLoop("Gameplay", out ILoopable loop);
LoopSystem.IsInitialized;                             // true si el Loop Manager existe ahora mismo

private void OnUpdate() { }             // LoopSystem.LoopDelegate: void sin parámetros
```

`Loop` implementa los métodos `Add...Listener` y `Remove...Listener` de forma explícita, así que la variable tiene que ser de tipo `ILoopable`. Los loops registrados están en `LoopSystem.Loops`, indexados por `LoopID`.

## Limitaciones

- `AddLoop` ignora sin aviso un loop con un `LoopID` ya registrado.
- `AddLoop` y `RemoveLoop` llamados desde un listener se aplican al terminar el recorrido en curso (`Update`, `FixedUpdate` o `LateUpdate`), no al instante: `HasLoop` sigue devolviendo el valor anterior hasta entonces.
