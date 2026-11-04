# Signals

Eventos identificados por un ID y registrados en una clase central. Quien emite y quien escucha solo comparten el ID, no una referencia entre ellos.

## Instalación

```sh
openupm add com.harryb97.tools.signals
```

## Uso

### Señal sin valor

```csharp
using HarryTools.Signals;

Signal _gameOver = Signal.New("GameOver")
    .WithListener(() => Debug.Log("Game Over"))
    .Build();

_gameOver.Dispatch();
```

### Señal con valor

Avisa a los listeners al asignar `Value`. `WithValue` asigna el valor inicial sin avisar.

```csharp
Signal<int> _health = Signal<int>.New("PlayerHealth")
    .WithValue(100)
    .DispatchOnSameValue(false)   // no avisa si el valor no cambia (por defecto sí avisa)
    .Build();

_health.Value -= 10;
```

### Obtener una señal

```csharp
Signal<int> _health = Signal<int>.Get("PlayerHealth");             // excepción si no existe o es de otro tipo
Signal<int> _health = await Signal<int>.GetAsync("PlayerHealth");  // espera a que se registre

IReadOnlySignal<int> _health = await Signal<int>.GetReadOnlyAsync("PlayerHealth");
_health.AddListener(OnHealthChanged);
_health.RemoveListener(OnHealthChanged);
```

`IReadOnlySignal` e `IReadOnlySignal<T>` permiten suscribirse (y en la genérica leer `Value`), pero no emitir. En `Signal` sin valor los listeners se añaden con `WithListener` o desde su `IReadOnlySignal`.

## Notas

- `Build` lanza `ArgumentException` si el ID es nulo o está vacío.
- Los IDs son únicos. Si ya existe una señal con ese ID se muestra un aviso y la nueva no se registra: `Get` sigue devolviendo la primera.
- `Signal` y `Signal<T>` comparten registro. Pedir un ID con otro tipo lanza `InvalidCastException`.
- `GetAsync` y `GetReadOnlyAsync` devuelven `Task` (no `UniTask`) y no terminan nunca si el ID no llega a registrarse.
- No hay forma de quitar una señal del registro. En el Editor se vacía al entrar y salir del Modo Play.

## Sample

**Signals Sample**, desde *Package Manager > Signals > Samples*: `Player` registra la señal `PlayerHealth`, `Enemy` le quita vida al pulsar Espacio y `HUD` la muestra en un texto.
