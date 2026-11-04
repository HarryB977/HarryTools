# Change Log:

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
