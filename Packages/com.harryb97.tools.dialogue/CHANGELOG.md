# Changelog

## [1.0.0-beta.2] - Sin publicar
### Añadido
- Ventana del graph (**Window > HarryTools > Dialogue Graph**, doble clic en el asset o **Open Graph** en el Inspector) para crear, conectar y editar todos los nodos, con deshacer y sin botón de guardar.
- Creación rápida de nodos con Espacio o clic derecho, y al soltar una conexión en un hueco vacío. Texto de líneas y respuestas editable directamente en el lienzo, que se guarda al perder el foco, y panel lateral con las propiedades del nodo o los ajustes del diálogo.
- Grupos con título y color (Ctrl+G, sacar con Shift o **Remove from Group**) y notas, guardados en el asset. Ajuste a cuadrícula, minimapa, alinear y distribuir, **Auto Layout** (se ejecuta solo una vez en los diálogos cuyos nodos están todos en el origen), copiar, pegar y duplicar con IDs nuevos.
- Buscador de nodos (Ctrl+F) por texto, personaje, nota de guion y tipo.
- Validación en la ventana con lista de avisos y notas informativas, marcas en los nodos y **Revalidate all**, que también detecta los diálogos lanzados como ambientales desde `DialogueLauncher` de escenas abiertas y prefabs.
- Depuración en Play: nodo activo iluminado, conexiones recorridas marcadas, selector de conversación y **Follow**, que cambia de diálogo con los saltos. El lienzo y el panel son de solo lectura en Play.
- Eventos `Dialogue.NodeEntered` y `DialogueRun.NodeEntered`, lanzados al entrar en cada nodo.
- Aviso en consola, una vez por diálogo y sesión, cuando una conversación ambiental llega a una decisión.

### Cambiado
- El Inspector del `DialogueAsset` deja de ser el editor de nodos: muestra los ajustes del diálogo, un resumen y **Open Graph**.

## [1.0.0-beta.1] - Sin publicar
### Añadido
- `DialogueAsset` con nodos de inicio, línea, decisión, rama, acciones, salto a otro diálogo (continuar o entrar y volver) y fin, conectados por IDs estables. Ajustes por diálogo: tiempo propio, política de ambientales propia y permitir saltar.
- `DialogueCharacter` (nombre localizado y voz de tecleo de `texteffects`) y `ChoiceTag`.
- Servicio `Dialogue` y `DialogueRun`: conversaciones principales (una a la vez) y ambientales, `Advance`, `Skip`, `Pause`, `Resume`, `Stop`, `PauseAll`, `ResumeAll`, `StopAll`, y eventos `Started`, `Ended`, `LineStarted`, `LineEnded`, `ChoicePresented` y `ChoiceMade`.
- Líneas con delays antes y después, acciones antes, al empezar y al terminar, tabla de eventos del texto y vida y avance propios opcionales.
- Decisiones con temporizador opcional (elegir la seleccionada o tomar "sin respuesta"), etiquetas, condiciones de visibilidad y disponibilidad con motivo de bloqueo, respuesta por defecto, selección inicial configurable, estado tras elegir (marcar como vista, desactivar u ocultar) y acciones al elegir. Control con `MoveSelection`, `Select`, `Confirm` y `Choose`.
- Políticas de ambientales al empezar una principal (`Continue`, `Pause`, `Silence`), por llamada, por diálogo o en Settings.
- Saltar conversaciones hasta la siguiente decisión o el final, ejecutando en orden sus acciones y eventos salvo los marcados para no ejecutarse al saltar.
- Tiempo escalado o no escalado por conversación (por defecto no escalado).
- `DialogueActor` y resolución de actores (asignados, el más cercano al origen o el primero registrado), como `Self` de las acciones y eventos de sus líneas.
- `LineContainer` (líneas máximas, reutilizar la vista con una sola línea, salida de líneas por antigüedad, por tiempo o ambas, vida, avance, contenedor principal y ambiental por defecto, enrutado) y vistas base `LineView` y `ChoiceView` sobre UGUI y `EffectText`.
- `DialogueMemory`: veces reproducido y respuestas elegidas por diálogo, con `Capture` / `Restore` y el ayudante `DialogueMemoryLocalSave` sobre `localdata`.
- `DialogueHistory` opcional con máximo de entradas y borrado al terminar cada conversación principal o manual.
- `DialogueLauncher` (manual, en `Start` o al activarse).
- Condiciones **Choice Was Chosen**, **Play Count** e **Is Main Dialogue Running** y acciones **Start Dialogue**, **Stop Dialogue**, **Skip Dialogue**, **Advance Dialogue** y **Forget Dialogue**.
- Localización automática: una String Table Collection por diálogo, claves derivadas de los IDs y texto escrito en el idioma de origen desde el inspector, con las demás traducciones visibles de solo lectura. Duplicar un diálogo crea una copia independiente con su propia colección.
- Inspector provisional del `DialogueAsset` para añadir, editar y quitar nodos, y `DialogueSettings` en **Project Settings > HarryTools > Dialogue**.
