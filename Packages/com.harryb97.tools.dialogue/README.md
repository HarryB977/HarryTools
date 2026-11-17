# HarryTools Dialogue

Diálogos con decisiones, ramas, memoria persistente y presentación configurable sobre UGUI.

- Diálogos como assets (`DialogueAsset`) con nodos de inicio, línea, decisión, rama, acciones, salto a otro diálogo y fin.
- Un servicio estático (`Dialogue`) que ejecuta conversaciones **principales** (una a la vez, con decisiones) y **ambientales** (sin límite).
- Decisiones con temporizador opcional, salida "sin respuesta", etiquetas, condiciones de visibilidad y disponibilidad, y estado "ya elegida" que se guarda en la partida.
- Reglas de presentación comunes (`LineContainer`): cuántas líneas caben, cuánto duran y cómo avanzan. Las vistas (`LineView`, `ChoiceView`) son componentes base que cada proyecto configura o hereda; el paquete no trae una interfaz montada.
- Texto con las marcas de `texteffects` (pausas, efectos, eventos, voces) y eventos de texto que ejecutan `ActionList`.
- Saltar una conversación ejecutando igualmente sus acciones y cambios de flags.
- Historial opcional de lo mostrado y lo elegido.
- Texto localizado automáticamente: cada diálogo tiene su propia String Table Collection y sus claves se generan solas.

> **Beta:** la API aún puede cambiar antes de la 1.0.0.

## Instalación

1. Instala **PrimeTween** antes que el paquete. OpenUPM solo tiene la 1.3.3 y la dependencia pide 1.5.0 o superior, así que usa una de estas dos formas:
   - Desde el Asset Store: tras importarlo, selecciona `Assets/Plugins/PrimeTween/PrimeTweenInstaller.asset` y pulsa **Install**. Eso añade `"com.kyrylokuzyk.primetween": "file:../Assets/Plugins/PrimeTween/internal/com.kyrylokuzyk.primetween-<versión>.tgz"` al `manifest.json`. Solo importar el asset no basta.
   - Desde el registro npm del autor: scope `com.kyrylokuzyk`, URL `https://registry.npmjs.org`. En ese caso quita `com.kyrylokuzyk` de los scopes del registro de OpenUPM si la CLI lo añade.
2. Instala el paquete desde [OpenUPM](https://openupm.com/packages/com.harryb97.tools.dialogue/). La CLI añade también UniTask y los paquetes `com.harryb97.*` de los que depende:

   ```sh
   openupm add com.harryb97.tools.dialogue@1.0.0-beta.1
   ```

Para instalarlo por URL de git hay que añadir también sus dependencias, porque Unity no las resuelve en ese caso. UniTask (`com.cysharp.unitask`) se instala desde OpenUPM:

```json
"com.harryb97.tools.dialogue": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.dialogue#dialogue/1.0.0-beta.1",
"com.harryb97.tools.texteffects": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.texteffects#texteffects/1.0.0-beta.2",
"com.harryb97.tools.actions": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.actions#actions/2.0.0-beta.1",
"com.harryb97.tools.conditions": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.conditions#conditions/2.0.0-beta.1",
"com.harryb97.tools.flags": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.flags#flags/1.0.0-beta.1",
"com.harryb97.tools.scenerefs": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.scenerefs#scenerefs/1.0.0-beta.1",
"com.harryb97.tools.serializereferences": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.serializereferences#serializereferences/1.0.3",
"com.harryb97.tools.localdata": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.localdata#localdata/2.0.3",
"com.harryb97.tools.logger": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.logger#logger/1.0.3",
"com.harryb97.tools.domainreload": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.domainreload#domainreload/1.0.3",
"com.cysharp.unitask": "2.5.10"
```

Requiere Unity 6000.0 o superior, `com.unity.ugui` 2.0.0 (TextMeshPro) y `com.unity.localization` 1.5.14, que Unity resuelve desde su registro.

## Puesta en marcha

1. Abre **Project Settings > HarryTools > Dialogue** (o **Tools > HarryTools > Dialogue > Open Settings**). La primera vez que se abre la página (fuera de Play) se crea el asset `DialogueSettings` en `Assets/HarryTools/Dialogue/Resources/`. Este asset va en git y en la build. Sin él se usan los valores por defecto sin avisar.
2. Comprueba que el proyecto tiene **Localization Settings** con al menos un idioma: el texto de los diálogos vive en String Tables.
3. Crea los personajes con **Create > HarryTools > Dialogue > Character** y los diálogos con **Create > HarryTools > Dialogue > Dialogue**.
4. Monta en la escena al menos un `LineContainer` principal con un prefab de `LineView` y, si hay decisiones, una `ChoiceView`.
5. Lanza el diálogo desde un `DialogueLauncher`, desde la acción **Start Dialogue** o desde código con `Dialogue.Start`.

## Piezas

| Pieza | Qué es |
|---|---|
| `DialogueAsset` | Un diálogo: sus nodos, conectados por ID, y sus ajustes (tiempo propio, política de ambientales propia, si se puede saltar). Siempre tiene un único nodo de inicio. |
| `DialogueCharacter` | Un personaje: nombre localizado (`LocalizedString`) y voz de tecleo (`TypingVoice` de `texteffects`). |
| `ChoiceTag` | Etiqueta sin lógica que se asigna a respuestas para que la vista les dé un aspecto. |
| `DialogueActor` | El personaje en la escena: qué `DialogueCharacter` es y, opcionalmente, su `LineContainer` para diálogos ambientales. Es el `Self` de las acciones y eventos de sus líneas. |
| `Dialogue` | Servicio estático: inicia diálogos, lista los activos, pausa, reanuda o detiene todos y emite los eventos globales. |
| `DialogueRun` | Una conversación en marcha: `Advance`, `Skip`, `Pause`, `Resume`, `Stop`, estado, nodo actual, decisión abierta y eventos propios. |
| `DialogueLauncher` | Componente que inicia un diálogo con su objeto como origen. |
| `LineContainer` | Reglas comunes de presentación de líneas y vista de decisiones del principal. |
| `LineView` / `ChoiceView` | Componentes base de UGUI para una línea y para una decisión. |
| `DialogueSettings` | Valores por defecto del proyecto. |
| `DialogueMemory` / `DialogueHistory` | Memoria persistente y historial en memoria. |

## Nodos

| Nodo | Contenido | Salidas |
|---|---|---|
| **Start** | Entrada del diálogo. No se puede borrar. | 1 |
| **Line** | Personaje, texto, nota para traducción, nota de guion, delay antes y después, acciones antes / al empezar / al terminar (cada lista con su opción *Run When Skipped*), tabla de eventos del texto, vida y avance propios opcionales. | 1 |
| **Choice** | Respuestas, temporizador opcional y regla al llegar a 0. | Una por respuesta + *No Answer* |
| **Branch** | Casos, cada uno con su `ConditionList`. Se toma el primero que se cumple. | Una por caso + *Otherwise* |
| **Actions** | `ActionList`, si el diálogo espera a que termine y si se ejecuta al saltar. | 1 |
| **Jump** | Otro `DialogueAsset`, en modo *Continue* (sigue allí) o *Call And Return* (entra y vuelve a la salida de este nodo al terminar). | 1 |
| **End** | Termina el diálogo. | 0 |

Una salida sin conectar termina el diálogo; dentro de un salto *Call And Return*, vuelve al diálogo que llamó. Los ciclos están permitidos.

Orden de una línea: delay antes → acciones *Before* (el diálogo las espera) → se muestra la línea y se lanzan las acciones *On Start* (sin esperarlas) → escritura con sus eventos → vida → acciones *On End* (el diálogo las espera) → delay después.

En este bloque los nodos se editan en el **Inspector** del `DialogueAsset`: ajustes del diálogo, un desplegable por nodo, **Add node** y **Remove**. Las salidas se conectan eligiendo el nodo de destino en una lista. Los IDs y las posiciones los gestiona la herramienta y no se muestran.

## Ejecución

`Dialogue.Start(asset, options)` devuelve un `DialogueRun`, o `null` si no puede empezar (asset nulo, sin nodo de inicio, o ya hay una conversación principal). El diálogo empieza en el siguiente frame, de modo que da tiempo a suscribirse a los eventos del run.

`DialogueStartOptions`:

| Opción | Qué hace |
|---|---|
| `IsMain` | Principal (por defecto) o ambiental. Solo puede haber una principal a la vez; las ambientales no tienen límite. |
| `AmbientPolicy` | Qué hacen las ambientales en marcha mientras dura esta principal. Sin valor, cada ambiental usa la de su asset y, si no la sobrescribe, la de Settings. |
| `Time` | Reloj de esta conversación. Sin valor, el del asset o el de Settings. |
| `Origin` | Objeto que lanzó el diálogo. Es el `Target` del `LogicContext` de todas las acciones y condiciones del diálogo, y la posición para elegir actor. |
| `Actors` | Pares personaje → `DialogueActor` fijos para esta conversación. |

**Actor de cada línea:** el asignado en `Actors`; si no hay, el `DialogueActor` activo de ese personaje más cercano al origen (o el primero registrado si no hay origen); si no hay ninguno, la línea va sin actor. El objeto del actor es el `Self` de las acciones y eventos de la línea; sin actor, `Self` va vacío. Los eventos del texto reciben el mismo contexto que las demás acciones de la línea, se ejecuten al escribirse, al saltarse o sin vista. Las condiciones de ramas y decisiones y las acciones de los nodos *Actions* se evalúan con `Self` vacío.

**Eventos:** `Started`, `Ended` (con `EndReason`: `Completed`, `Stopped` o `Skipped`), `LineStarted`, `LineEnded`, `ChoicePresented` y `ChoiceMade` en `Dialogue`, y los mismos (salvo `Started`) en cada `DialogueRun`. `ChoiceMade` no se lanza cuando se toma la salida *No Answer*.

**Control:** `DialogueRun.Advance()` es el input del jugador sobre la línea actual (ver contenedores). `Dialogue.Main` es la principal en marcha, `Dialogue.Active` todas (una copia: se pueden detener mientras se recorre), y `Dialogue.PauseAll`, `ResumeAll` y `StopAll` actúan sobre todas.

**Políticas de ambientales** (`AmbientPolicy`), que se aplican a las ambientales en marcha en el momento en que empieza una principal:

- `Continue`: siguen.
- `Pause`: se pausan con su línea hasta que acaba la principal. Esta pausa es aparte de la pausa a mano: `Resume` y `ResumeAll` no la levantan, y el final de la principal no levanta una pausa a mano. La ambiental sigue cuando no le queda ninguna de las dos.
- `Silence`: se detienen y no se retoman.

Una ambiental que empieza mientras ya hay una principal en marcha no recibe ninguna política: sigue con normalidad.

Una conversación cruza como mucho 1000 nodos en un mismo frame sin esperar a nada (una línea, un delay, una decisión o unas acciones). Si los supera, como en un ciclo de nodos que no esperan, se detiene (`Stopped`) con un aviso.

Si un run lanza una excepción, se detiene con error en consola y los demás siguen.

### DialogueLauncher

| Campo | Qué hace |
|---|---|
| **Dialogue** | Diálogo que lanza. |
| **Launch On** | `Manual` (solo con `Launch()`), `Start` o `Enable`. |
| **Is Main** | Principal o ambiental. |
| **Actors** | Actores fijos para la conversación. |

`Launch()` no inicia otra conversación mientras la suya sigue activa: devuelve la que está en marcha. `Stop()` detiene solo la suya. El objeto del launcher es el origen.

## Decisiones

Cada respuesta (`ChoiceOption`) tiene:

- Texto y nota para traducción.
- **Tags**: lista de `ChoiceTag`; la vista decide su aspecto.
- **Visible** (`ConditionList`): si no se cumple, la respuesta no aparece.
- **Available** (`ConditionList`): si no se cumple, aparece bloqueada, con un texto opcional del motivo (**Locked Reason**).
- **Is Default**: respuesta de respaldo del temporizador.
- **After Chosen**: `Nothing`, `MarkSeen` (aparece como vista), `Disable` (aparece bloqueada) o `Hide` (deja de aparecer). El estado sale de la memoria, así que se mantiene entre conversaciones y partidas.
- **On Chosen**: `ActionList` que se lanza al elegirla, sin esperarla.

**Temporizador** (opcional): duración en segundos del reloj de la conversación y regla al llegar a 0:

- `ChooseSelected`: la respuesta seleccionada; si no hay, la primera marcada por defecto; si no hay, la primera disponible.
- `NoAnswer`: se toma la salida *No Answer*.

Si todas las respuestas visibles están bloqueadas o no hay ninguna visible, se toma *No Answer*; si no está conectada, el diálogo termina ahí con un aviso.

La decisión abierta es `DialogueRun.CurrentChoice` (`ChoicePrompt`): `Options` (las visibles, con `Available`, `Seen` e `IsDefault`), `SelectedIndex`, `HasTimer`, `Remaining01` (de 1 a 0) y el evento `Changed` al cambiar la selección. Control: `MoveSelection(delta)` (solo por las disponibles, dando la vuelta), `Select(index)`, `Confirm()` y `Choose(index)`. Las respuestas bloqueadas no se pueden seleccionar ni elegir. El estado de las respuestas se calcula al abrir la decisión.

Las decisiones son de las conversaciones principales. En una ambiental se toma la respuesta de respaldo al momento y se avisa en consola.

## Memoria y guardado

`DialogueMemory` guarda, por diálogo (GUID del asset), cuántas veces se ha reproducido y qué respuestas se han elegido (por su ID estable, que no cambia al renombrar ni reordenar). No usa flags y no aparece en la ventana de Flags.

- Un diálogo cuenta como reproducido al empezar. Solo cuenta el diálogo que se inicia: los diálogos a los que se llega con un salto no suman.
- `PlayCount`, `WasChosen`, `MarkPlayed`, `MarkChosen`, `Forget` y `ResetAll`.
- `Capture()` devuelve un `DialogueMemorySnapshot` (compatible con `JsonUtility`, con versión) para la ranura de guardado del juego; `Restore(snapshot)` lo aplica y lanza `Restored`. Restaurar con una conversación en marcha afecta a la siguiente decisión que se abra.
- `DialogueMemoryLocalSave.SaveAsync(directorio, archivo)` / `LoadAsync(directorio, archivo)`: ayudante opcional que guarda y carga la memoria con `localdata`.

El ID de cada diálogo es el GUID de su asset: renombrar o mover el asset no cambia nada. **Duplicar** un diálogo crea una copia independiente: IDs nuevos de nodos, respuestas y salidas, su propia String Table Collection con el texto copiado en todos los idiomas, y su propia memoria.

## Historial

Opcional: se activa en Settings (**History Enabled**). `DialogueHistory.Entries` guarda las líneas mostradas y las respuestas elegidas de las conversaciones **principales** (las líneas que se cruzan al saltar no se muestran y no se registran); `Added` avisa de cada entrada nueva y `Clear()` lo vacía.

- `HistoryEntry`: `Kind` (`Line` o `Choice`), `Character` (solo líneas), `Text` (`LocalizedString`) y `Dialogue`. `GetText()` da el texto en el idioma actual sin las marcas de `texteffects`, conservando las de TextMeshPro.
- **History Max** limita las entradas (se descartan las más antiguas) y **History Clear** decide si se vacía al terminar cada conversación principal (`OnConversationEnd`) o solo por código (`Never`).
- Solo son datos: cada juego lo muestra a su manera. No se guarda en la partida.

## Contenedores y vistas

### LineContainer

| Campo | Qué hace |
|---|---|
| **Is Main** | Recibe las líneas de la conversación principal y muestra sus decisiones. |
| **Is Default Ambient** | Recibe las líneas ambientales cuyo actor no tiene contenedor. |
| **Max Visible** | Máximo de líneas en pantalla. Con 1, cada línea sustituye a la anterior; con más, al entrar una nueva con el contenedor lleno sale la más antigua. |
| **Lifetime** | Cuánto dura una línea tras escribirse: `Fixed` (segundos), `ByTextLength` (base + segundos por carácter), `ByVoice` (por ahora igual que `ByTextLength`) o `UntilNext` (no caduca). |
| **Advance** | `Input`, `Auto` o `Both`. |
| **Line Prefab** | Prefab con un `LineView`. Las vistas se reutilizan desde un pool. |
| **Line Parent** | Padre de las vistas. Vacío usa el propio objeto. |
| **Choice View** | La `ChoiceView` que muestra las decisiones mientras este es el contenedor principal. |

Si hay varios contenedores principales (o ambientales por defecto) activos, se usa el último activado y se avisa.

**Enrutado:** la conversación principal va al contenedor principal; una ambiental, al contenedor del actor de la línea, después al ambiental por defecto. Si no hay contenedor, la línea no se muestra pero el diálogo sigue con sus acciones y tiempos (la línea dura lo que diga su vida, con la vida y el avance de Settings) y se avisa una vez por conversación. Los eventos de su texto se ejecutan en orden al terminar la línea; si se salta, solo los que tienen *Run When Skipped*. Una línea cuyo contenedor o vista desaparece a mitad sigue igual sin vista, con los eventos que su vista no llegó a lanzar.

**Vida y avance:** la vida empieza a contar cuando termina la escritura. Cada línea usa la vida y el avance del contenedor, salvo que la línea los sobrescriba; los valores de Settings se aplican a las líneas que no tienen contenedor. Con input, la primera llamada a `Advance` completa la escritura, o continúa si el texto espera en `<waitinput>` (en cualquier modo); la siguiente termina la línea. Una línea automática con vida `UntilNext` también acepta input, para no quedarse para siempre.

**Cuándo sale una línea de la pantalla:** al terminar una línea el diálogo sigue, pero la vista se queda visible hasta que otra la sustituye o la empuja fuera, hasta que su conversación salta o hasta que su conversación termina. Las líneas de una misma conversación se acumulan mientras sigue y salen todas cuando acaba. Al salir, se llama a `Hide` del texto y la vista vuelve al pool cuando termina de ocultarse.

Eventos del contenedor: `LineAdded`, `LineRemoved` y `LinesMoved`; `Visible` lista las vistas en pantalla, de la más antigua a la más nueva. El contenedor solo ordena las vistas como hermanas; el layout y las animaciones son de las vistas (o de un `LayoutGroup`).

### LineView

Referencia a un `EffectText` (**Text**, con *Show On Enable* desactivado) y, opcionalmente, un `TMP_Text` para el nombre (**Speaker Name**). Al recibir una línea pone el nombre del personaje, su voz de tecleo como `DefaultVoice` (o la del prefab si el personaje no tiene), la tabla de eventos de la línea (con el actor, o nada, como `Self` y el `Origin` como `Target`), el reloj de la conversación, y el texto localizado, que empieza a escribirse cuando llega.

Métodos virtuales para las subclases: `Bind`, `OnEnter`, `OnExit` y `OnIndexChanged(index, count)` (0 es la más antigua). Los eventos de texto se ejecutan en una cola de la vista: volver al pool no los cancela.

### ChoiceView

El contenedor principal llama a `Show(prompt)` y `Hide()`, que activan y desactivan su objeto. Las subclases dibujan en `Refresh()` (al mostrarse y cada vez que cambia la selección), leen `Prompt` y responden con sus métodos (`Select`, `MoveSelection`, `Confirm`, `Choose`). `GetOptionText(index)` y `GetLockedReason(index)` dan los textos en el idioma actual. Los botones, la navegación, las etiquetas y la barra del temporizador (`Prompt.Remaining01`) son de la subclase.

El paquete no lee input: el juego llama a `Advance`, `Skip` y a los métodos de la decisión desde sus controles.

## Saltar

`DialogueRun.Skip()` avanza sin mostrar texto hasta la siguiente decisión o el final, ejecutando en orden lo que habría pasado: acciones *Before*, *On Start* y *On End*, eventos del texto y nodos *Actions*. Las listas con *Run When Skipped* desactivado no se ejecutan. La línea en pantalla se completa (sus eventos saltados se ejecutan según la regla de `texteffects`), y todas las líneas de esa conversación que siguen en pantalla salen por el camino normal de salida antes de abrir la decisión, sea cual sea el momento en que se salte. Las líneas de otras conversaciones no se tocan.

- El salto no espera a las acciones: se encolan y se ejecutan una tras otra mientras el diálogo ya ha avanzado. Las ramas se evalúan en el momento, así que una condición puede leer el estado antes de que la cola haya terminado de aplicarlo.
- Las acciones encoladas por un salto siguen ejecutándose aunque la conversación se detenga con `Stop`.
- El salto se para en las decisiones de la conversación principal; tras elegir, se puede volver a saltar.
- Un salto también está sujeto al límite de 1000 nodos por frame (ver Ejecución): un ciclo sin decisiones detiene la conversación con un aviso.
- Solo funciona si el diálogo lo permite (**Allow Skip**) y la conversación está en marcha: se ignora en pausa o con una decisión abierta.

## Tiempo y pausa

Cada conversación usa tiempo escalado o no escalado (`DialogueTime`). Por defecto, **no escalado** (Settings); cada diálogo puede sobrescribirlo y cada llamada a `Start` también. El reloj afecta a la escritura, la vida de las líneas, los delays y el temporizador de las decisiones.

`DialogueRun.Pause()` congela la conversación, sus tiempos y su línea en pantalla (también la escritura, con `EffectText.Pause`). Las acciones que ya están en marcha **no** se pausan. `Resume()` deshace `Pause()`; `Dialogue.PauseAll` y `ResumeAll` actúan sobre todas. `Resume` no levanta la pausa de la política `Pause` de una principal: esa ambiental sigue pausada hasta que la principal acaba.

## Lógica

Para `conditions` y `actions` (carpeta **Dialogue** del selector de tipos):

| Tipo | Nombre | Descripción |
|---|---|---|
| Condición | **Choice Was Chosen** | La respuesta se eligió alguna vez en el diálogo, según la memoria. La respuesta se elige de una lista del diálogo indicado. |
| Condición | **Play Count** | Compara las veces que se ha iniciado el diálogo con un valor. |
| Condición | **Is Main Dialogue Running** | Hay una conversación principal en marcha. |
| Acción | **Start Dialogue** | Inicia un diálogo, principal o ambiental, con el objetivo del contexto (o su `Self`) como origen. Con *Wait Until End* termina cuando acaba el diálogo; cancelar la acción deja de esperar pero no detiene el diálogo. |
| Acción | **Stop Dialogue** | Detiene la principal, todas, o todas las de un diálogo. |
| Acción | **Skip Dialogue** | Salta la principal hasta su siguiente decisión o su final. |
| Acción | **Advance Dialogue** | Da a la principal el input de avanzar. |
| Acción | **Forget Dialogue** | Borra la memoria de un diálogo. |

Las condiciones y acciones de `flags` (y cualquier otra) funcionan dentro de los nodos. Los diálogos son assets, así que solo ven **flags globales**.

## Localización

El texto de las líneas, las respuestas y los motivos de bloqueo se escribe en el inspector del diálogo en el **idioma de origen** (el de Settings; vacío usa el idioma del proyecto) y se guarda en String Tables de Unity Localization:

- Cada diálogo tiene **su propia String Table Collection**, creada al escribir el primer texto en la carpeta de Settings (**Tables Folder**, por defecto `Assets/Localization/Dialogue`), con el nombre `Dialogue_<nombre del asset>` y una tabla por idioma del proyecto. El diálogo la enlaza por GUID: renombrar el asset o la colección no rompe nada.
- Las **claves** se generan a partir del ID estable del nodo o la respuesta (el motivo de bloqueo añade `.locked`). Nunca se escriben a mano.
- Bajo el campo del idioma de origen se ven los demás idiomas, de solo lectura, con un aviso si falta la traducción. Las traducciones se editan en las tablas.
- El nombre de los personajes es un `LocalizedString` normal, en la tabla que se elija.

Sin ningún idioma en el proyecto, el campo de texto muestra un aviso y no escribe nada.

## Settings

| Campo | Qué hace |
|---|---|
| **Default Time** | Reloj de los diálogos que no tienen uno propio. Por defecto `Unscaled`. |
| **Default Ambient Policy** | Política de ambientales cuando ni la llamada ni el asset la indican. Por defecto `Continue`. |
| **Default Lifetime / Default Advance** | Vida y avance de las líneas sin contenedor que no los sobrescriben. |
| **History Enabled / History Max / History Clear** | Historial (ver arriba). |
| **Tables Folder** | Carpeta donde se crean las colecciones de los diálogos. |
| **Source Locale Code** | Idioma en el que se escriben los diálogos. Vacío usa el del proyecto. |

## Límites de este bloque

- Sin ventana de graph: los diálogos se editan en el Inspector.
- Sin validador de diálogos (marcas, eventos, traducciones, nodos inalcanzables).
- Localización mínima: sin comentarios de contexto para traductores, sin orden de las entradas por recorrido, sin borrado automático de entradas al quitar nodos y sin integración con Google Sheets. Las notas para traducción se guardan en el diálogo pero aún no salen a la tabla.
- Sin voz por línea con el paquete `audio`; la vida `ByVoice` se comporta como `ByTextLength`.
- Sin Sample con prefabs de interfaz.

## Notas

- Logs con `logger`, categoría `Dialogue`.
- El estado estático es compatible con Play sin recarga de dominio.
