# HarryTools Dialogue

Diálogos con decisiones, ramas, memoria persistente y presentación configurable sobre UGUI.

- Diálogos como assets (`DialogueAsset`) con nodos de inicio, línea, decisión, rama, acciones, salto a otro diálogo y fin.
- Ventana de graph para escribirlos y depurarlos: texto directamente en el lienzo, grupos con color, notas, buscador, avisos de validación y seguimiento del nodo activo en Play.
- Un servicio estático (`Dialogue`) que ejecuta conversaciones **principales** (una a la vez, con decisiones) y **ambientales** (sin límite).
- Decisiones con temporizador opcional, salida "sin respuesta", etiquetas, condiciones de visibilidad y disponibilidad, y estado "ya elegida" que se guarda en la partida.
- Reglas de presentación comunes (`LineContainer`): cuántas líneas caben, cuánto duran y cómo avanzan. Las vistas (`LineView`, `ChoiceView`) son componentes base que cada proyecto configura o hereda; el paquete no trae una interfaz montada.
- Texto con las marcas de `texteffects` (pausas, efectos, eventos, voces) y eventos de texto que ejecutan `ActionList`.
- Saltar una conversación ejecutando igualmente sus acciones y cambios de flags.
- Historial opcional de lo mostrado y lo elegido.
- Texto localizado automáticamente: cada diálogo tiene su propia String Table Collection, con comentarios para traductores, orden de lectura, avisos de traducciones sin hacer o desactualizadas y sincronización opcional con Google Sheets.

> **Beta:** la API aún puede cambiar antes de la 1.0.0.

## Instalación

1. Instala **PrimeTween** antes que el paquete. OpenUPM solo tiene la 1.3.3 y la dependencia pide 1.5.0 o superior, así que usa una de estas dos formas:
   - Desde el Asset Store: tras importarlo, selecciona `Assets/Plugins/PrimeTween/PrimeTweenInstaller.asset` y pulsa **Install**. Eso añade `"com.kyrylokuzyk.primetween": "file:../Assets/Plugins/PrimeTween/internal/com.kyrylokuzyk.primetween-<versión>.tgz"` al `manifest.json`. Solo importar el asset no basta.
   - Desde el registro npm del autor: scope `com.kyrylokuzyk`, URL `https://registry.npmjs.org`. En ese caso quita `com.kyrylokuzyk` de los scopes del registro de OpenUPM si la CLI lo añade.
2. Instala el paquete desde [OpenUPM](https://openupm.com/packages/com.harryb97.tools.dialogue/). La CLI añade también UniTask y los paquetes `com.harryb97.*` de los que depende:

   ```sh
   openupm add com.harryb97.tools.dialogue@1.0.0-beta.2
   ```

Para instalarlo por URL de git hay que añadir también sus dependencias, porque Unity no las resuelve en ese caso. UniTask (`com.cysharp.unitask`) se instala desde OpenUPM:

```json
"com.harryb97.tools.dialogue": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.dialogue#dialogue/1.0.0-beta.2",
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
3. Crea los personajes con **Create > HarryTools > Dialogue > Character** y los diálogos con **Create > HarryTools > Dialogue > Dialogue**. Un diálogo se edita en la ventana del graph (ver abajo): doble clic sobre el asset.
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

Los nodos se editan en la ventana del graph (ver **Ventana del graph**). Los IDs de nodos, salidas y respuestas los gestiona la herramienta y no se muestran.

## Ventana del graph

### Abrir

- **Doble clic** sobre un `DialogueAsset`.
- Botón **Open Graph** del Inspector del asset, que además muestra los ajustes del diálogo y un resumen (número de nodos y avisos).
- Menú **Window > HarryTools > Dialogue Graph**, y el campo de asset de la barra para cambiar de diálogo.

La ventana muestra un diálogo a la vez. Los cambios se escriben en el asset al momento, con deshacer de Unity; no hay botón de guardar. Posiciones, grupos y notas viajan con el asset y el runtime no los usa. La barra tiene el campo de asset, **Snap to Grid**, **Mini Map**, el número de avisos y el buscador. En Play aparecen además el selector de conversación y **Follow**.

### Crear y conectar

- **Espacio** o clic derecho sobre el lienzo abre el buscador de tipos de nodo (**Create Node**).
- Arrastrar una conexión desde una salida y soltarla en un hueco vacío abre ese mismo buscador: el nodo creado queda conectado a la salida.
- Una salida admite una sola conexión; una entrada admite varias. Los ciclos están permitidos.
- Borrar un nodo desconecta las salidas que lo apuntaban. El nodo Start no se puede borrar ni duplicar.
- La salida *No Answer* de una decisión y *Otherwise* de una rama están siempre visibles.
- **Auto Layout** (clic derecho) coloca los nodos por niveles de izquierda a derecha. Se ejecuta solo, una vez, al abrir un diálogo cuyos nodos están todos en el origen. Esa primera colocación escribe las posiciones en el asset, así que el asset queda modificado.
- Doble clic en un nodo Jump abre el diálogo destino.

### Nodos en el lienzo

- El lienzo no tiene campos editables: cada nodo muestra un resumen de solo lectura y todo se edita en el panel lateral. Las líneas muestran el personaje y hasta dos líneas de su texto en el idioma de origen; las decisiones, una línea por respuesta junto a su salida. El resumen quita las marcas de texto y se corta con "…" cuando no cabe, así que el texto nunca cambia el tamaño del nodo.
- Todos los nodos de contenido tienen el mismo ancho. Las entradas quedan pegadas al borde izquierdo y las salidas al derecho, y las filas de respuestas de una decisión tienen todas la misma altura.
- Los nodos muestran indicadores de lo que llevan (acciones, eventos, delays, vida y avance propios, temporizador, resumen de cada caso, destino y modo del salto).
- **Start** y **End** son nodos compactos: solo el título y su conexión. Start (verde) tiene una salida y End (rojo) una entrada. Una salida sin conectar también termina el diálogo.
- Las conexiones que vuelven atrás (el nodo destino está a la izquierda del origen o en la misma x, incluidas las de un nodo a sí mismo) se dibujan más tenues y finas. Significan lo mismo que las demás y se recalculan al mover nodos. En Play, una conexión recorrida se marca igual aunque vuelva atrás.

### Panel lateral

- Con un nodo seleccionado, la cabecera muestra el tipo de nodo con su color y un ID corto. Debajo hay secciones plegables, siempre en el mismo orden; la ventana recuerda durante la sesión cuáles están abiertas para cada tipo de nodo.
  - **Line:** *Text* (personaje, texto, nota para traducción y nota de guion), *Translations*, *Timing* (delays antes y después, vida y avance propios), *Actions* (antes, al empezar y al terminar) y *Text Events*.
  - **Choice:** *Timer* (usar temporizador, segundos y regla al agotarse) y *Options*, con un bloque plegable por respuesta titulado con su texto: texto y sus traducciones, motivo de bloqueo y sus traducciones, respuesta por defecto, estado tras elegir, etiquetas, condiciones de visibilidad y de disponibilidad, acciones al elegir y nota para traducción.
  - **Branch:** un bloque por caso con sus condiciones. *Otherwise* se toma cuando ningún caso se cumple.
  - **Actions** y **Jump:** sus campos, sin secciones. **Start** y **End:** una descripción.
- Las respuestas de una decisión y los casos de una rama se añaden, quitan y reordenan desde el panel; sus salidas se rehacen solas.
- El texto se escribe en el idioma de origen en el panel y se guarda en la tabla al escribir. Las traducciones se editan en *Translations* (ver **Localización**).
- Sin selección, el panel muestra los ajustes del diálogo (reloj, política de ambientales y permitir saltar).

### Grupos y notas

- **Ctrl+G** (o **Create Group** en el menú contextual) agrupa los nodos seleccionados. El grupo tiene título editable y color (menú **Color** del grupo y selector de color), y se mueve con sus nodos.
- Para sacar un nodo de un grupo, arrástralo fuera con **Shift** pulsado o usa **Remove from Group**. Un grupo que se queda sin nodos desaparece.
- **Create Note** añade una nota de texto libre con tamaño ajustable.

### Alinear, copiar y buscar

- Con varios nodos seleccionados, el menú contextual ofrece alinear y distribuir.
- **Copiar, pegar y duplicar** generan IDs nuevos para nodos, salidas y respuestas. Se conservan las conexiones entre los nodos copiados y se quitan las que salían de la selección. Los textos se copian a entradas nuevas de la tabla. Pegar funciona entre ventanas y entre diálogos a través del portapapeles del sistema.
- **Ctrl+F** o el campo de búsqueda de la barra busca por texto de línea o respuesta (idioma de origen), personaje, nota de guion y tipo de nodo. Elegir un resultado centra la vista en el nodo y lo selecciona.

### Validación

La lista de la parte inferior se actualiza al abrir el diálogo y tras cada cambio. Solo informa: no bloquea Play ni builds. Hacer clic en una entrada selecciona y centra su nodo, y los nodos con avisos llevan una marca con el mensaje como tooltip. Hay dos niveles:

- **Avisos:** diálogo sin Start o con Start sin conectar, nodos inalcanzables desde Start, salidas que apuntan a un nodo borrado, personaje o diálogo destino de un Jump ausente, línea o respuesta sin texto en el idioma de origen, entradas de tabla que faltan, marcas de texto desconocidas o eventos sin entrada en la tabla de eventos de la línea, temporizador con *ChooseSelected* sin respuesta por defecto, y decisión en un diálogo lanzado como ambiental.
- **Notas informativas:** salidas sin conectar (terminan el diálogo). *No Answer* solo se señala cuando el temporizador usa esa regla.

**Revalidate all** valida todos los diálogos del proyecto, vuelve a buscar los diálogos lanzados como ambientales y muestra un resumen. Esa detección solo mira los `DialogueLauncher` ambientales de las escenas abiertas y de los prefabs del proyecto que lanzan el diálogo directamente: no sigue Jumps, ni la acción **Start Dialogue**, ni el código. Para esos casos, el runtime escribe un aviso en consola, una vez por diálogo y sesión, cuando una conversación ambiental llega a una decisión (que se responde sola con la respuesta de respaldo).

### Depuración en Play

- En Play la ventana sigue las conversaciones en marcha: el nodo actual se ilumina y las conexiones recorridas en esa conversación quedan marcadas. Al terminar, la marca se mantiene hasta la siguiente conversación o hasta salir de Play.
- El selector de la barra lista las conversaciones activas (la principal primero) y permite elegir cuál se sigue. Por defecto se sigue la principal. Una ventana sin diálogo abierto muestra el de la conversación que empiece.
- Con **Follow** activado, la vista se centra sola en el nodo activo y cambia al diálogo al que salta la conversación (y vuelve al regresar de un Jump *Call And Return*). También cambia a una conversación principal de otro diálogo. Mover la vista a mano desactiva **Follow** hasta volver a pulsarlo.
- En Play el lienzo y el panel lateral, traducciones incluidas, son de solo lectura: no se crean, borran, conectan ni mueven nodos.
- Para integraciones propias, `Dialogue.NodeEntered` y `DialogueRun.NodeEntered` avisan de cada nodo en el que entra una conversación. El nodo Start se anuncia en la primera actualización de la conversación; si está en pausa, incluida la que aplica la política de ambientales de la principal, se anuncia cuando se reanuda.

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

**Eventos:** `Started`, `Ended` (con `EndReason`: `Completed`, `Stopped` o `Skipped`), `NodeEntered` (cada nodo en el que entra la conversación; el Start, en su primera actualización, o al reanudarse si una pausa o la política de ambientales de la principal la retiene), `LineStarted`, `LineEnded`, `ChoicePresented` y `ChoiceMade` en `Dialogue`, y los mismos (salvo `Started`) en cada `DialogueRun`. `ChoiceMade` no se lanza cuando se toma la salida *No Answer*.

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

Al abrirse, la decisión selecciona la primera respuesta disponible marcada por defecto o, si no hay, la primera disponible. **Initial Selection** en Settings lo cambia a `None` (sin selección).
- `NoAnswer`: se toma la salida *No Answer*.

Si todas las respuestas visibles están bloqueadas o no hay ninguna visible, se toma *No Answer*; si no está conectada, el diálogo termina ahí con un aviso.

La decisión abierta es `DialogueRun.CurrentChoice` (`ChoicePrompt`): `Options` (las visibles, con `Available`, `Seen` e `IsDefault`), `SelectedIndex`, `HasTimer`, `Remaining01` (de 1 a 0) y el evento `Changed` al cambiar la selección. Control: `MoveSelection(delta)` (solo por las disponibles, dando la vuelta), `Select(index)`, `Confirm()` y `Choose(index)`. Las respuestas bloqueadas no se pueden seleccionar ni elegir. El estado de las respuestas se calcula al abrir la decisión.

Las decisiones son de las conversaciones principales. En una ambiental se toma la respuesta de respaldo al momento y se avisa en consola, una vez por diálogo y sesión.

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
| **Max Visible** | Máximo de líneas en pantalla. Con 1, cada línea sustituye a la anterior; con más, al entrar una nueva con el contenedor lleno sale la más antigua. No se aplica con **Exit** `AfterTime`. |
| **Reuse View** | Con **Max Visible** 1, cada línea nueva se escribe en la vista que ya está en pantalla, sin ocultar antes el texto anterior. Activado por defecto. |
| **Exit** | Cuándo sale una línea de la pantalla: `PushedOut` (por antigüedad), `AfterTime` (por tiempo) o `Both` (lo primero que ocurra). Ver abajo. |
| **Stay Time** | Segundos que una línea sigue en pantalla después de que el diálogo pase a lo siguiente, con **Exit** `AfterTime` o `Both`. |
| **Lifetime** | Cuánto dura una línea tras escribirse: `Fixed` (segundos), `ByTextLength` (base + segundos por carácter), `ByVoice` (por ahora igual que `ByTextLength`) o `UntilNext` (no caduca). |
| **Advance** | `Input`, `Auto` o `Both`. |
| **Line Prefab** | Prefab con un `LineView`. Las vistas se reutilizan desde un pool. |
| **Line Parent** | Padre de las vistas. Vacío usa el propio objeto. |
| **Choice View** | La `ChoiceView` que muestra las decisiones mientras este es el contenedor principal. |

Si hay varios contenedores principales (o ambientales por defecto) activos, se usa el último activado y se avisa.

**Enrutado:** la conversación principal va al contenedor principal; una ambiental, al contenedor del actor de la línea, después al ambiental por defecto. Si no hay contenedor, la línea no se muestra pero el diálogo sigue con sus acciones y tiempos (la línea dura lo que diga su vida, con la vida y el avance de Settings) y se avisa una vez por conversación. Los eventos de su texto se ejecutan en orden al terminar la línea; si se salta, solo los que tienen *Run When Skipped*. Una línea cuyo contenedor o vista desaparece a mitad sigue igual sin vista, con los eventos que su vista no llegó a lanzar.

**Vida y avance:** la vida empieza a contar cuando termina la escritura. Cada línea usa la vida y el avance del contenedor, salvo que la línea los sobrescriba; los valores de Settings se aplican a las líneas que no tienen contenedor. Con input, la primera llamada a `Advance` completa la escritura, o continúa si el texto espera en `<waitinput>` (en cualquier modo); la siguiente termina la línea. Una línea automática con vida `UntilNext` también acepta input, para no quedarse para siempre.

**Cuándo sale una línea de la pantalla:** al terminar una línea el diálogo sigue, pero la vista se queda visible según **Exit**:

- `PushedOut`: hasta que otra la sustituye o la empuja fuera, o hasta que su conversación termina. Las líneas de una misma conversación se acumulan mientras sigue y salen todas cuando acaba.
- `AfterTime`: hasta que pasa su **Stay Time**, aunque su conversación ya haya terminado. No hay máximo de líneas.
- `Both`: lo primero de las dos cosas: la empuja otra línea o pasa su **Stay Time**. Al terminar la conversación, cada línea sigue hasta su **Stay Time**.

En todos los casos sale al saltar su conversación. Al salir, se llama a `Hide` del texto y la vista vuelve al pool cuando termina de ocultarse.

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

El texto de las líneas, las respuestas y los motivos de bloqueo se escribe en el panel lateral de la ventana del graph en el **idioma de origen** (el de Settings; vacío usa el idioma del proyecto) y se guarda en String Tables de Unity Localization.

### Tablas y claves

- Cada diálogo tiene **su propia String Table Collection**, creada al escribir el primer texto en la carpeta de Settings (**Tables Folder**, por defecto `Assets/Localization/Dialogue`), con el nombre `Dialogue_<nombre del asset>` y una tabla por idioma del proyecto. El diálogo la enlaza por GUID: renombrar el asset o la colección no rompe nada.
- Las **claves** se generan a partir del ID estable del nodo o la respuesta (el motivo de bloqueo añade `.locked`). Nunca se escriben a mano ni se renombran.
- La sección **Translations** del panel lista cada idioma del proyecto salvo el de origen, con un campo de texto por idioma para la línea, cada respuesta y cada motivo de bloqueo. Escribe en la tabla de ese idioma bajo la misma clave, crea la tabla o la entrada si faltan y se deshace con Ctrl+Z. Un idioma sin texto muestra *Not translated*.
- Sin ningún idioma en el proyecto, el campo de texto muestra un aviso y no escribe nada.

### Comentarios y orden

- Cada entrada lleva un comentario para quien traduce, en inglés, con el tipo de texto (`Line`, `Option` o `Locked reason`), el diálogo, el personaje de origen (solo en líneas), de dónde viene en el recorrido (tras el inicio, tras una respuesta, caso de una rama u "Otherwise") y la nota de guion si la hay. Si un texto se alcanza desde varios sitios, los orígenes se separan con `; `.
- Las entradas se ordenan por el recorrido del diálogo desde Start, siguiendo las salidas en orden, una decisión seguida de sus respuestas y sus motivos de bloqueo; lo que Start no alcanza va al final. Las claves y los IDs no cambian nunca.
- Comentarios y orden se actualizan al guardar el diálogo, antes de subir a la hoja y después de bajar (bajar cambia el orden). Se ven en la ventana **Localization Tables** de Unity.

### Borrado

- Borrar un nodo, una respuesta o un caso borra sus entradas en todas las tablas de la colección, en el mismo paso de Undo. Deshacer las recupera con sus textos. Pegar y duplicar crean entradas nuevas.
- **Clean orphan entries**, en el Inspector del `DialogueAsset`, quita de la colección las entradas cuyo dueño ya no existe. Sirve para restos anteriores a esta versión.

### Idiomas nuevos

- Al añadir un idioma al proyecto, cada colección de diálogo y la de personajes reciben su tabla vacía. En la hoja, las columnas de ese idioma aparecen en la siguiente subida.
- Al quitar un idioma no se borra nada: su tabla se queda en la colección y sus columnas en la hoja.

### Traducciones desactualizadas

- Cada traducción guarda el texto de origen que había cuando se escribió o se revisó. Si el original cambia, la traducción pasa a **Outdated**. "Sin traducir" es otra cosa: la entrada no existe o está vacía.
- Se ve en el campo del idioma del panel (la marca y el texto de origen anterior), en la validación y en la hoja.
- Una traducción deja de estar desactualizada cuando cambia su texto, cuando el original vuelve a como estaba, con **Mark as reviewed** (por idioma) o **Mark all as reviewed** (todos los idiomas de ese texto), o cuando quien traduce la da por revisada en la hoja. Nunca se vacía ni se borra una traducción por estar desactualizada.

### Nombres de personajes

- La herramienta mantiene una colección propia, `DialogueCharacters`, en la carpeta de tablas de Settings. Cada `DialogueCharacter` tiene un ID estable que es la clave de su entrada.
- El nombre se escribe en el Inspector del personaje, en el idioma de origen, con sus traducciones y las mismas marcas y botones de revisión que las líneas.
- Un personaje cuyo nombre apunta a otra tabla se muestra tal cual y no se cambia sin pedirlo: el botón **Move to dialogue characters table** lo pasa a la colección propia.
- Al duplicar un personaje, su nombre se copia a una entrada nueva, de modo que los dos no comparten texto por accidente.
- Los personajes se suben y se bajan como la pestaña `Characters`.

### Validación

Además de lo descrito en la ventana del graph, el validador informa de la localización:

- **Untranslated in:** nota informativa, una por línea, respuesta o motivo de bloqueo (el texto y el motivo de una respuesta cuentan juntos), con la lista de idiomas sin texto.
- **Outdated in:** aviso, con la lista de idiomas con la traducción desactualizada.
- **Translation (código):** aviso cuando una traducción difiere del original en marcas de texto o eventos, según las reglas de `texteffects`.
- **Speaker name untranslated in:** nota informativa, una por personaje en su primera línea, con los idiomas en los que su nombre no tiene texto.

## Google Sheets

La sincronización con una hoja de Google es opcional y usa la extensión de Google Sheets de Unity Localization. Las llamadas bloquean el editor y no se lanzan en Play.

### Guía desde cero

1. **Proyecto de Google Cloud.** En la consola de Google Cloud crea un proyecto nuevo o elige uno existente.
2. **Sheets API.** En ese proyecto, abre las APIs y servicios y habilita la Google Sheets API.
3. **Credenciales OAuth.**
   - Configura la pantalla de consentimiento y añade tu cuenta de Google como usuario de prueba.
   - Crea un ID de cliente OAuth de tipo "Aplicación de escritorio" y copia su ID y su secreto.
   - Una clave de API solo permite bajar de hojas accesibles con ella; subir y crear pestañas exigen OAuth.
4. **Asset de servicio.**
   - Crea un asset **Sheets Service Provider** fuera de cualquier carpeta `Resources`, para que las credenciales nunca entren en una build. Si está dentro, Settings lo avisa.
   - Elige la autenticación OAuth, introduce el ID y el secreto del cliente y autoriza el acceso desde el propio asset en el navegador.
5. **Hoja.** Crea una hoja en Google Drive y copia su ID, la parte de la URL entre `/d/` y `/edit`.
6. **Configuración.** En **Project Settings > HarryTools > Dialogue**, sección Google Sheets, asigna el asset de servicio y el ID. Se guardan en `ProjectSettings`, fuera de `Resources` y de las builds.
7. **Test connection.** Comprueba el acceso a la hoja y muestra el resultado. Con una clave de API avisa de que solo se puede bajar.
8. **Primera subida.** Con **Push all**, o **Push** en la barra de la ventana del graph para un solo diálogo. Cada diálogo crea su pestaña.

### Qué se sincroniza

- Cada diálogo tiene su pestaña, que se crea si no existe y se sigue usando aunque se renombre el diálogo. Se titula con el nombre del diálogo y los seis primeros caracteres de su ID, porque Google exige títulos únicos. Los personajes van en la pestaña `Characters`.
- Las columnas son la clave, su comentario y, por cada idioma, el texto y una columna `<código> Status`. Se rehacen al añadir idiomas.
- Se lanza desde **Window > HarryTools > Dialogue > Push all / Pull all** y los botones equivalentes de Settings (también para los personajes), y desde **Push** y **Pull** de la barra del graph para el diálogo abierto. Cada diálogo se sincroniza entero o no se toca. "Todos" sigue con el resto si uno falla y al final da un resumen.

### Reglas de la bajada y la subida

- **Subir baja siempre primero.** Si la bajada falla, no se sube nada. Después regenera comentarios y orden y sube clave, comentarios, el texto de todos los idiomas y las marcas.
- **Bajar** solo actualiza los idiomas que no son el de origen y nunca toca la columna de origen ni el texto escrito en el graph.
- Una traducción local solo se sustituye si la hoja cambió desde la última sincronización. **Si la misma traducción cambió en los dos sitios desde entonces, gana la hoja** y se cuenta y avisa como conflicto.
- **Una celda vacía nunca borra una traducción local.**
- Las filas desconocidas (de nodos borrados, añadidas a mano o con la clave editada) se ignoran y se cuentan. Bajar no crea ni renombra claves ni borra entradas locales que no estén en la hoja.
- Si hay filas desconocidas o conflictos, se avisa en consola y en el resumen.
- Con **clave de API** solo se puede bajar: subir y crear pestañas necesitan OAuth y fallan antes de enviar nada.
- Las columnas se localizan por el **título de su cabecera**, así que no hay que renombrarlas. Un idioma cuya cabecera falta se salta, y si falta la de la clave no se baja nada.
- Los errores (sin conexión, sin permisos, hoja inexistente) salen en un diálogo y en consola.

### Trabajar con quien traduce

- Quien traduce edita solo las columnas de texto de su idioma. La clave y los comentarios no se tocan, y la columna de origen no se baja nunca.
- La columna `<código> Status` marca con **Outdated** y el texto de origen anterior las traducciones desactualizadas. Para darla por revisada, quien traduce **borra el contenido de esa celda, pero conserva su nota de celda**, y se baja. Si el original cambió después de la última subida, la marca no se quita.
- Las notas para quien traduce o revisa van en **notas de celda** de Google Sheets, no en las columnas de comentario, que son de la herramienta y se reescriben al subir.

## Settings

| Campo | Qué hace |
|---|---|
| **Default Time** | Reloj de los diálogos que no tienen uno propio. Por defecto `Unscaled`. |
| **Default Ambient Policy** | Política de ambientales cuando ni la llamada ni el asset la indican. Por defecto `Continue`. |
| **Default Lifetime / Default Advance** | Vida y avance de las líneas sin contenedor que no los sobrescriben. |
| **Initial Selection** | Respuesta seleccionada al abrir una decisión: `DefaultOrFirst` (por defecto) o `None`. |
| **History Enabled / History Max / History Clear** | Historial (ver arriba). |
| **Tables Folder** | Carpeta donde se crean las colecciones de los diálogos y la de personajes. |
| **Source Locale Code** | Idioma en el que se escriben los diálogos. Vacío usa el del proyecto. |
| **Service Provider / Spreadsheet ID** | Credenciales de Google y hoja de destino (solo editor, ver Google Sheets). |
| **Test connection, Push all, Pull all, Push characters, Pull characters** | Comprobación y sincronización con la hoja. |

## Límites de este bloque

- Sin voz por línea con el paquete `audio`; la vida `ByVoice` se comporta como `ByTextLength` (bloque 4).
- Sin Sample con prefabs de interfaz (bloque 4).
- La estructura del graph no se edita en Play.

## Notas

- Logs con `logger`, categoría `Dialogue`.
- El estado estático es compatible con Play sin recarga de dominio.
