# Logger

Envoltorio de `Debug.Log` que colorea los mensajes por tipo y les añade una categoría. Los mensajes solo se emiten con el símbolo `DEBUG` (Editor y Development Builds).

## Instalación

```sh
openupm add com.harryb97.tools.logger
```

## Uso

```csharp
using Logger = HarryTools.LoggerTool.Logger;   // Evita el choque con UnityEngine.Logger

Logger.Initialize();   // Con DEBUG activa Debug.unityLogger; sin él desactiva todos los logs de Unity

Logger.Log("Save", "Partida guardada");
Logger.LogWarning("Save", "No hay espacio");
Logger.LogError("Save", "Archivo corrupto");
```

El resultado es `<b>[Save]</b> Partida guardada`, en blanco, amarillo o rojo según el tipo.

## Console

`Logger` usa por debajo la clase estática `Console`, que está en el namespace `UnityEngine` y ofrece más variantes. Todos los métodos tienen versión con y sin categoría (`Log`, `LogFormat`, `LogWarning`, `LogWarningFormat`, `LogError`, `LogErrorFormat`, `LogException`):

```csharp
Console.Log("Mensaje");
Console.Log("Save", data);
Console.LogFormat("Save", "Slot {0}", slot);
Console.LogWarning("Save", "No hay espacio");
Console.LogErrorFormat("Save", "Slot {0} corrupto", slot);
Console.LogException("Save", exception);
```

Todos sus métodos llevan `[Conditional("DEBUG")]`: sin `DEBUG` la llamada desaparece, incluidos sus argumentos.

## Limitaciones

- Si el archivo tiene `using System;`, `Console` es ambiguo con `System.Console`. Usa `UnityEngine.Console` o un alias.
- `LogException` solo registra `exception.Message`, sin la traza.

## Créditos

`Console` está basado en [UnityLoggerExtended](https://github.com/JoanStinson/UnityLoggerExtended) de JoanStinson (MIT).
