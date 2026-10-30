# Change Log:

## 1.0.1

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.0.0

    - Primera versión
    - Sustituye a 'Processes': una sola interfaz 'IAction' con contexto y 'CancellationToken' estándar
    - Acciones de flujo: Wait, Wait Until, If, Parallel, Sequence, Repeat, Run In Background
    - 'Invoke Unity Event' sustituye al 'UnityEvent' suelto de los grupos
    - 'ActionHandle' para controlar cada ejecución y componente 'ActionRunner'
