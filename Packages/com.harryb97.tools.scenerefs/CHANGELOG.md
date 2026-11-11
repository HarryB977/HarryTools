# Changelog

## [1.0.0-beta.1] - 2026-11-11
### Añadido
- `SceneRef<T>`: campo serializable que guarda la referencia directa y el guid del objeto (y, para componentes, su tipo concreto y su posición entre componentes de ese tipo). `Value` devuelve el tipo pedido; resuelve por referencia directa o, si no, por guid entre las escenas cargadas, y avisa una vez por referencia y carga de escena si no lo encuentra.
- Componente oculto `SceneId` con el guid interno y el ID visible, y fila de ID en la cabecera del GameObject (`Add ID`, edición, ⟳ para regenerar el guid y ✕ para quitarlo), con validación al escribir, multiselección y Undo.
- `SceneRefs`: registro de ejecución de las escenas cargadas, objetos inactivos incluidos, con `Find`, `TryFind` y `Assign` para objetos creados en ejecución. Compatible con varias escenas aditivas y sin recarga de dominio.
- Drawer de `SceneRef<T>` con arrastre desde cualquier escena, y selector `▾` con buscador de todos los objetos del índice, incluidas las escenas cerradas.
- Índice de IDs del proyecto (`SceneIdIndex`), mantenido al guardar escenas y al cambiarlas fuera del editor, con inspector de solo lectura y **Tools > HarryTools > SceneRefs > Rebuild Id Index**.
- Validación que bloquea Play y la build con IDs en conflicto (mismo guid, mismo ID sin distinguir mayúsculas, `SceneId` en un prefab de Assets), con un informe en consola por conflicto.
- Corrección automática de duplicados al hacer Ctrl+D o pegar, y botón **Regenerate IDs in scene** para escenas duplicadas.
- Los prefabs de Assets pierden sus `SceneId`; la instancia de escena que los tenía los conserva como componente añadido.
