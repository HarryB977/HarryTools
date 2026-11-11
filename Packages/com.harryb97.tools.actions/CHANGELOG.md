# Change Log:

## [2.0.0-beta.1] - 2026-11-11

    Cambiado (incompatible):
    - `SetBehaviourEnabledAction.behaviours` pasa de `Behaviour[]` a `TargetComponent<Behaviour>[]`. Los elementos vacíos o que no se resuelven se saltan. Hay que volver a asignar las referencias
    - `RunActionRunnerAction.runner` y `CancelActionRunnerAction.runner` pasan de `ActionRunner` a `TargetComponent<ActionRunner>`. Hay que volver a asignar las referencias
    - `InstantiateAction.parent` pasa de `Transform` a `TargetComponent<Transform>`. Por defecto es `Custom` vacío, que equivale a la raíz de la escena
    - Dependencias: `com.harryb97.tools.conditions` 2.0.0-beta.1, nueva `com.harryb97.tools.scenerefs` 1.0.0-beta.1 y `serializereferences` 1.0.3

    Añadido:
    - Las acciones pueden apuntar por ID a objetos de escena desde assets y otras escenas
    - Pruebas de PlayMode en `Tests/`

    Otros:
    - Todos los comentarios, tooltips y mensajes del código pasan a inglés

## 1.0.4

    - `displayName` de `Actions` a `HarryTools Actions`, para distinguirlo en la carpeta Packages y en el Package Manager. Los samples nuevos se importan en `Assets/Samples/HarryTools Actions/`

## 1.0.3

    - Dependencia `com.harryb97.tools.serializereferences` de 1.0.0 a 1.0.2: la 1.0.0 no está publicada y no se podía resolver desde OpenUPM
    - Dependencia `com.harryb97.tools.conditions` de 1.0.0 a 1.0.2: la 1.0.0 no está publicada y no se podía resolver desde OpenUPM

## 1.0.2

    - Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.actions` y el Package Manager avisa de las actualizaciones
    - `package.json`: añadido `repository` y el autor ya no incluye email

## 1.0.1

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.0.0

    - Primera versión
    - Sustituye a 'Processes': una sola interfaz 'IAction' con contexto y 'CancellationToken' estándar
    - Acciones de flujo: Wait, Wait Until, If, Parallel, Sequence, Repeat, Run In Background
    - 'Invoke Unity Event' sustituye al 'UnityEvent' suelto de los grupos
    - 'ActionHandle' para controlar cada ejecución y componente 'ActionRunner'
