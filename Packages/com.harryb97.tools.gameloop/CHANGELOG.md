# Change Log:

## 1.1.0

    - `LoopSystem.Initialize()` ya no es obligatorio: `AddLoop`, `RemoveLoop`, `HasLoop` y `TryGetLoop` crean el "Loop Manager" si hace falta
    - `Initialize()` es idempotente: con un manager vivo no hace nada y no vacía los loops. Si el manager se destruyó desde fuera, se vuelve a crear conservando los loops
    - `AddLoop` y `RemoveLoop` llamados desde un listener se aplican al terminar el recorrido, en vez de modificar el diccionario mientras se recorre
    - Añadidos `IsInitialized`, `HasLoop` y `TryGetLoop`
    - Con la recarga de dominio desactivada, al entrar en Play ya no quedan loops ni managers de la sesión anterior
    - Añadidos tests de PlayMode

## 1.0.3

    - `displayName` de `Game Loop` a `HarryTools Game Loop`, para distinguirlo en la carpeta Packages y en el Package Manager. Los samples nuevos se importan en `Assets/Samples/HarryTools Game Loop/`

## 1.0.2

    - Añadido README con la instalación, el uso y la configuración

## 1.0.1

    - Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.gameloop` y el Package Manager avisa de las actualizaciones
    - `package.json`: añadido `repository` y el autor ya no incluye email

## 1.0.0

    - Primera versión