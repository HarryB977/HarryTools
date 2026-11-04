# Disposable

Atributo `[StaticDispose]` para métodos estáticos que deben ejecutarse en el Editor al cambiar el estado del Modo Play, por ejemplo para limpiar estáticos al salir.

## Instalación

```sh
openupm add com.harryb97.tools.disposable
```

## Uso

```csharp
using HarryTools.Disposable;

private static List<Enemy> enemies = new();

[StaticDispose]
private static void Clear() => enemies.Clear();
```

## Limitaciones

- Solo métodos **estáticos** y sin parámetros. Los que tienen parámetros se ignoran sin aviso.
- Se ejecuta en todos los cambios de estado salvo al entrar en el Modo Play: al salir del modo edición, al salir del Modo Play y al volver al modo edición. Es decir, varias veces por cada sesión de Play.
- Solo actúa en el Editor. En las builds no hace nada.
