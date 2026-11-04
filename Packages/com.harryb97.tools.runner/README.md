# Runner

Un único `MonoBehaviour` que reparte `Update`, `LateUpdate` y `FixedUpdate` a clases normales, sin que tengan que ser `MonoBehaviour`.

## Instalación

```sh
openupm add com.harryb97.tools.runner
```

## Uso

```csharp
using HarryTools.Runner;

public class Cooldown : IRunnable {           // ILateRunnable -> LateTicks(), IFixedRunnable -> FixedTicks()
    public Cooldown() => Runner.Get.AddTicks(this);

    void IRunnable.Ticks() {
        // Se llama en cada Update
    }

    public void Stop() => Runner.Get.RemoveTicks(this);
}
```

`Runner.Get` crea el GameObject "Runner" (DontDestroyOnLoad) la primera vez que se usa.

```csharp
Runner.Get.AddLateTicks(this);
Runner.Get.AddFixedTicks(this);

bool registered = Runner.Get.CheckTicks(this);   // También CheckLateTicks y CheckFixedTicks

Runner.Get.StopTicks();      // Pausa todos los IRunnable. También StopLateTicks y StopFixedTicks
Runner.Get.ResumeTicks();    // También ResumeLateTicks y ResumeFixedTicks
```

Añadir dos veces el mismo objeto no lo duplica.

## Limitaciones

- Quitar un objeto desde su propio `Ticks()` hace que el siguiente de la lista se salte ese frame.

## Sample

*Runner Sample*, importable desde Package Manager > Samples: una escena con `ExampleEntity`, que crea un `ExampleClass` que se registra en el `Runner` y se quita solo pasado un tiempo.
