# Changelog

## [1.0.0-beta.1] - Sin publicar
### Añadido
- `DialogueAsset` con nodos de inicio, línea, decisión, rama, acciones, salto a otro diálogo (continuar o entrar y volver) y fin, conectados por IDs estables. Ajustes por diálogo: tiempo propio, política de ambientales propia y permitir saltar.
- `DialogueCharacter` (nombre localizado y voz de tecleo de `texteffects`) y `ChoiceTag`.
- Servicio `Dialogue` y `DialogueRun`: conversaciones principales (una a la vez) y ambientales, `Advance`, `Skip`, `Pause`, `Resume`, `Stop`, `PauseAll`, `ResumeAll`, `StopAll`, y eventos `Started`, `Ended`, `LineStarted`, `LineEnded`, `ChoicePresented` y `ChoiceMade`.
- Líneas con delays antes y después, acciones antes, al empezar y al terminar, tabla de eventos del texto y vida y avance propios opcionales.
- Decisiones con temporizador opcional (elegir la seleccionada o tomar "sin respuesta"), etiquetas, condiciones de visibilidad y disponibilidad con motivo de bloqueo, respuesta por defecto, estado tras elegir (marcar como vista, desactivar u ocultar) y acciones al elegir. Control con `MoveSelection`, `Select`, `Confirm` y `Choose`.
- Políticas de ambientales al empezar una principal (`Continue`, `Pause`, `Silence`), por llamada, por diálogo o en Settings.
- Saltar conversaciones hasta la siguiente decisión o el final, ejecutando en orden sus acciones y eventos salvo los marcados para no ejecutarse al saltar.
- Tiempo escalado o no escalado por conversación (por defecto no escalado).
- `DialogueActor` y resolución de actores (asignados, el más cercano al origen o el primero registrado), como `Self` de las acciones y eventos de sus líneas.
- `LineContainer` (líneas máximas, vida, avance, contenedor principal y ambiental por defecto, enrutado) y vistas base `LineView` y `ChoiceView` sobre UGUI y `EffectText`.
- `DialogueMemory`: veces reproducido y respuestas elegidas por diálogo, con `Capture` / `Restore` y el ayudante `DialogueMemoryLocalSave` sobre `localdata`.
- `DialogueHistory` opcional con máximo de entradas y borrado al terminar cada conversación principal o manual.
- `DialogueLauncher` (manual, en `Start` o al activarse).
- Condiciones **Choice Was Chosen**, **Play Count** e **Is Main Dialogue Running** y acciones **Start Dialogue**, **Stop Dialogue**, **Skip Dialogue**, **Advance Dialogue** y **Forget Dialogue**.
- Localización automática: una String Table Collection por diálogo, claves derivadas de los IDs y texto escrito en el idioma de origen desde el inspector, con las demás traducciones visibles de solo lectura. Duplicar un diálogo crea una copia independiente con su propia colección.
- Inspector provisional del `DialogueAsset` para añadir, editar y quitar nodos, y `DialogueSettings` en **Project Settings > HarryTools > Dialogue**.
