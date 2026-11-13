# Changelog

## [1.0.0-beta.1] - 2026-11-13
### Añadido
- Base de datos de flags (`FlagDatabase`) con flags booleanos globales y locales por escena, identificados por un guid interno. Los nombres son únicos dentro de su ámbito (sin distinguir mayúsculas) y un global y un local pueden llamarse igual. Los locales se ligan al GUID del asset de escena, así que renombrar o mover la escena no los rompe.
- Ventana **Window > HarryTools > Flags**: lista de globales y de locales por escena (escenas cerradas y grupo "Missing scene" incluidos), valor en vivo en Play editable a mano, crear, renombrar y borrar, detalle con usos, avisos e historial de la sesión de Play.
- `FlagRef`: campo serializable con drawer de una línea, desplegable con buscador (globales y locales de la escena del objeto) y "Create new…".
- API estática `Flags` (`IsSet`, `Set`, `Clear`, `Toggle`, `SetValue`, `Subscribe`) con los eventos `Changed` (con motivo `Live`, `Restore` o `Reset`) y `Restored`, y los mismos miembros en `FlagRef`.
- `FlagSnapshot` con `Flags.Capture`, `Flags.Restore` y `Flags.ResetAll`, compatible con `JsonUtility` y con versión de formato. Los guids huérfanos se ignoran con aviso.
- `FlagsLocalSave`: ayudante opcional para guardar y cargar el estado con `localdata`.
- Condición **Flag Is Set** y acciones **Set Flag**, **Clear Flag** y **Toggle Flag**.
- `Flag Listener`: listas *On Apply* (al activarse y tras `Restore` o `ResetAll`) y *On Change* (cambio en vivo), con cancelación de la lista anterior.
- Índice de usos (`FlagUsageIndex`) que encuentra `FlagRef` dentro de `[SerializeReference]`, se mantiene al guardar y al cambiar archivos fuera del editor, y se reconstruye con **Tools > HarryTools > Flags > Rebuild Usage Index**. Atributo opcional `[FlagUsage]`.
- Validación que bloquea Play y la build con guids o nombres repetidos en un ámbito, y avisos para referencias rotas, locales de otra escena y flags de escenas que ya no existen.
- Historial de depuración de cambios y lecturas durante Play, agrupando lecturas repetidas.
