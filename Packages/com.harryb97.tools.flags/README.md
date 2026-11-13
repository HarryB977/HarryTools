# HarryTools Flags

Flags booleanos de estado del mundo, para recordar lo que pasa en una escena y leerlo en otra ("el interruptor de la sala 3 abrió la puerta del pasillo"):

- Dos ámbitos: **globales** (de todo el juego) y **locales** (de una escena), para que cinco escenas puedan tener cada una su `Door01`.
- Se crean, renombran y borran desde la **ventana Flags**, que muestra también el valor en vivo en Play, quién usa cada flag y un historial de cambios y lecturas.
- Los campos `FlagRef` eligen el flag de un desplegable: no hay nombres escritos a mano.
- `Flag Listener`: un objeto reacciona a un flag, colocándose al instante al cargar y animándose al cambiar en vivo.
- Condición y acciones listas para `conditions` y `actions`.
- Guardado dentro del sistema de partida del juego (`Capture`/`Restore`) o con un ayudante basado en `localdata`.
- Renombrar un flag o una escena no rompe referencias ni partidas guardadas.

> **Beta:** la API aún puede cambiar antes de la 1.0.0.

## Instalación

Instala el paquete desde [OpenUPM](https://openupm.com/packages/com.harryb97.tools.flags/). La CLI añade también los paquetes `com.harryb97.*` de los que depende:

```sh
openupm add com.harryb97.tools.flags@1.0.0-beta.1
```

Para instalarlo por URL de git hay que añadir también sus dependencias, porque Unity no las resuelve en ese caso:

```json
"com.harryb97.tools.flags": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.flags#flags/1.0.0-beta.1",
"com.harryb97.tools.conditions": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.conditions#conditions/2.0.0-beta.1",
"com.harryb97.tools.actions": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.actions#actions/2.0.0-beta.1",
"com.harryb97.tools.scenerefs": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.scenerefs#scenerefs/1.0.0-beta.1",
"com.harryb97.tools.serializereferences": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.serializereferences#serializereferences/1.0.3",
"com.harryb97.tools.localdata": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.localdata#localdata/2.0.3",
"com.harryb97.tools.logger": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.logger#logger/1.0.3",
"com.harryb97.tools.domainreload": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.domainreload#domainreload/1.0.3",
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10"
```

Requiere Unity 6000.0 o superior.

## Cómo funciona

Cada flag tiene:

| Dato | Qué es |
|---|---|
| **guid** | Identificador interno (32 caracteres hexadecimales). Lo genera la herramienta al crear el flag, nunca cambia y es lo que guardan las referencias y las partidas. |
| **Nombre** | Texto visible (`Door01`): letras, dígitos y `_`, sin espacios. Único dentro de su ámbito, sin distinguir mayúsculas. Se puede renombrar sin romper nada. |
| **Ámbito** | `Global`, o `Local` de una escena (identificada por el GUID de su asset, así que renombrar o mover la escena no rompe sus flags). |
| **Valor inicial** | Lo que vale el flag hasta que alguien lo cambia (por defecto `false`). |
| **Descripción** | Una línea opcional para recordar para qué sirve. |

Un flag global y uno local pueden llamarse igual. Para que no haya confusión el nombre nunca se muestra solo: siempre con su ámbito (`Global / Door01`, `Hospital_Planta1 / Door01`).

Las definiciones viven en un asset `FlagDatabase` (`Assets/HarryTools/Flags/Resources/FlagDatabase.asset`, se crea solo la primera vez). Va en git y en la build, porque hace falta en ejecución para los valores iniciales. Su inspector es de solo lectura: los flags se editan desde la ventana Flags.

**Globales y locales:** lo que cruza escenas es global. Un flag local solo se puede usar desde su propia escena, y los assets (ScriptableObjects, prefabs de Assets) solo ven globales.

## La ventana Flags

**Window > HarryTools > Flags.**

- A la izquierda, la lista: grupo Global y un grupo por escena con flags locales (incluidas las escenas cerradas), más un grupo "Missing scene" con los flags de escenas que ya no existen. Buscador y filtro Todas / Globales / Locales. Cada fila muestra el checkbox, el nombre, el número de usos, el último cambio (en Play) y un aviso si lo hay.
- **Fuera de Play, el checkbox es el valor inicial. En Play, el valor actual**, y cambiarlo cuenta como un cambio en vivo, así que se ve la reacción de los Flag Listener. La fila que cambia se ilumina un momento.
- Botones `+ Global` y `+ Local` (con selector de escena, por defecto la activa). Renombrar en la fila o en el detalle. Borrar un flag con usos muestra la lista y pide confirmación. No se permiten nombres repetidos en el mismo ámbito.
- A la derecha, el detalle del flag: ámbito, nombre, valor inicial, descripción y sus **usos**: quién lo activa (Set, Clear, Toggle) y quién lo lee (Flag Is Set, Flag Listener). Un clic selecciona el objeto si su escena está abierta, o el asset; si la escena está cerrada, ofrece abrirla.
- **Historial de la sesión de Play**: cambios y lecturas con hora, valor y origen. Se vacía cada vez que se entra en Play. Las lecturas repetidas seguidas del mismo origen (por ejemplo un `Wait Until` que comprueba cada frame) se agrupan en una entrada con contador.
- **Avisos** (no bloquean): se lee pero nadie lo activa, se activa pero nadie lo lee, sin ningún uso, mismo nombre como global y como local, flags de una escena que ya no existe y referencias rotas.

## El campo `FlagRef`

```csharp
using HarryTools.Flags;

[SerializeField] private FlagRef doorOpen;
```

En el inspector es una sola línea con el ámbito y el nombre actuales (con icono o color distinto para global y local):

```
[ Global / DoorB               ▾ ]
[ Hospital_Planta1 / Door01    ▾ ]
[ ⚠ Door01 (deleted)           ▾ ]   en rojo: el flag ya no existe
[ ⚠ Hospital_Sotano / LightsOn ▾ ]   en rojo: local de otra escena
```

- El desplegable tiene buscador y ofrece los flags **globales** y, si el objeto vive en una escena, **los locales de esa escena**. Nada más. En ScriptableObjects, prefabs de Assets y modo prefab solo ve globales.
- Al final del desplegable, **Create new…** abre la ventana Flags con la creación preparada en el ámbito que toca; al crearlo, el campo queda asignado.
- No se puede escribir texto en el campo. Undo, multiedición y overrides de prefab funcionan como en cualquier propiedad.
- Opcionalmente, `[FlagUsage(FlagUsageKind.Read)]` (o `Write`) sobre un campo `FlagRef` de tu código le dice al índice de usos si ese campo lee o escribe el flag. Sin el atributo cuenta como uso "otro".

### Desde código

```csharp
if (doorOpen.IsSet) { ... }
doorOpen.Set(this);        // el Object opcional es el origen que verá el historial
doorOpen.Clear(this);
doorOpen.Toggle(this);
doorOpen.SetValue(true, this);

doorOpen.Subscribe(OnDoorChanged);     // solo los cambios de este flag
doorOpen.Unsubscribe(OnDoorChanged);

private void OnDoorChanged(FlagChange _change)
{
    // _change.OldValue, _change.NewValue, _change.Reason (Live, Restore o Reset), _change.Source
}
```

La clase estática `Flags` ofrece lo mismo (`Flags.IsSet(ref)`, `Flags.Set(ref, source)`, …) y dos eventos globales: `Flags.Changed` (por cada flag cuyo valor cambia) y `Flags.Restored` (una vez al terminar un `Restore` o `ResetAll`).

- Si el valor nuevo es igual al actual, no pasa nada y no se emite evento.
- Un `FlagRef` vacío, o que apunta a un flag que ya no existe: leer devuelve `false`, escribir no hace nada, y sale un aviso en consola una vez por referencia.
- Un flag vale su valor inicial hasta que alguien lo cambia en esta partida.

## Flag Listener

Componente (`HarryTools > Flags > Flag Listener`) para que un objeto reaccione a un flag, por ejemplo una puerta. Tiene un `FlagRef` y cuatro `ActionList` de `actions`:

| Lista | Cuándo corre |
|---|---|
| **On Apply Set / On Apply Cleared** | Al activarse el objeto (`OnEnable`: carga de escena o activación) y tras un `Restore` o `ResetAll`, según el valor del flag. Para colocar el objeto **sin animación**. |
| **On Change Set / On Change Cleared** | Cuando el flag cambia en vivo (motivo `Live`) con el componente activo. Para **animar**. |

- Si una lista *On Change* está vacía, se usa la *On Apply* del mismo valor.
- Si el flag cambia mientras el objeto está desactivado o su escena descargada, no pasa nada; al activarse se vuelve a aplicar.
- Si llega un cambio mientras aún corre la lista anterior, se cancela la anterior. Al desactivarse se cancela lo que esté en marcha.
- El `Self` del contexto de las acciones es el GameObject del listener.

El interruptor solo cambia el flag; la puerta decide cómo reaccionar. Así el resultado no depende del orden de `Start`: el valor se aplica al activarse y otra vez si se carga una partida después.

## Condición y acciones

Aparecen en el menú de tipos bajo `Flags/`:

- Condición **Flag Is Set** (con `Invert`, como el resto de condiciones). Con la acción `Wait Until` de `actions` sirve para esperar a que un flag cambie.
- Acciones **Set Flag**, **Clear Flag** y **Toggle Flag**. Pasan el `Self` del contexto como origen para el historial.

## Guardado

Los flags solo persisten si el juego guarda la partida. No hay reinicio por escena: reintentar un nivel es cargar la última partida guardada.

### `Capture` y `Restore`

```csharp
FlagSnapshot snapshot = Flags.Capture();   // [Serializable], compatible con JsonUtility
// ... el sistema de partida del juego lo mete en su archivo, una ranura con sus flags ...
Flags.Restore(snapshot);                   // sustituye el estado entero, no lo mezcla
Flags.ResetAll();                          // partida nueva: todo vuelve a su valor inicial
```

- Solo se guardan los flags que se han cambiado. Los no tocados siguen tomando su valor inicial, así que cambiar un valor inicial durante el desarrollo afecta también a partidas viejas en los flags que no se tocaron.
- `Restore` y `ResetAll` emiten `Flags.Changed` una vez por cada flag cuyo valor efectivo cambia (motivo `Restore` o `Reset`) y, al terminar, `Flags.Restored`. Los Flag Listener cuentan `Restore` como "aplicar".
- Una entrada con un `guid` que ya no existe en la base de datos se ignora con un aviso y la carga continúa.
- El volcado lleva un número de versión y un campo `type` por entrada (hoy siempre `bool`), para poder añadir otros tipos sin romper partidas viejas.

### `FlagsLocalSave`

Ayudante opcional para juegos que no tienen sistema de partida. Usa `LocalDataSystem` de `localdata`, y solo guarda o carga cuando el juego lo llama:

```csharp
await FlagsLocalSave.SaveAsync("Saves", "slot1.json");

bool loaded = await FlagsLocalSave.LoadAsync("Saves", "slot1.json");   // false si falta el archivo o no se puede leer
```

## Índice de usos

El índice (`Assets/HarryTools/Flags/FlagUsageIndex.asset`, se crea solo) guarda dónde se usa cada flag: escena o asset, ruta en la jerarquía y componente, y el tipo de uso (escribe, lee, otro). Es **una caché** solo de editor, no entra en la build y se puede reconstruir entera.

- Encuentra los `FlagRef` dentro de `[SerializeReference]`, así que ve las acciones y condiciones de cualquier `ActionList` o `ConditionList`.
- Se actualiza al guardar escenas y assets, y al cambiar archivos fuera del editor (git pull). Las escenas cerradas cambiadas fuera del editor se leen de su YAML, de forma aproximada.
- **Tools > HarryTools > Flags > Rebuild Usage Index** lo reconstruye abriendo cada escena no cargada en modo aditivo, y revisa también los assets.
- Sube el índice al repositorio. Se reescribe al guardar escenas, así que puede dar conflictos de merge: quédate con cualquiera de las dos versiones y reconstruye.

## Validación al entrar en Play y en la build

**Bloquean** Play y la build, con un informe en consola:

- Dos flags con el mismo `guid` en la base de datos.
- Dos flags con el mismo nombre en el mismo ámbito (puede pasar tras un merge de git; la ventana lo impide).

**Avisan sin bloquear**, con un mensaje por caso:

- `FlagRef` a un flag borrado.
- `FlagRef` local que apunta a una escena distinta de la suya (por ejemplo tras duplicar una escena o mover un objeto entre escenas).
- Flags locales de escenas que ya no existen.

Si el proyecto no tiene base de datos, la validación no hace nada.

## Limitaciones

- **Solo booleanos** en la v1. Las comprobaciones numéricas se hacen con `conditions`. El formato de guardado ya está preparado para otros tipos.
- **Los assets solo referencian flags globales.** No hay flags locales "relativos" (un asset que dice `Door01` y se resuelve contra la escena en la que corre).
- **Un flag local solo se puede usar desde su propia escena.** El editor lo avisa; en ejecución no se valida.
- **Los usos desde código C# sin un campo `FlagRef` no aparecen** en el índice ni en la ventana.
- **Los usos de escenas cambiadas fuera del editor son aproximados** hasta que se guarda la escena o se pulsa Rebuild.
- **`FromAsset` indexa el ScriptableObject o prefab principal del asset**: los sub-assets de otros tipos no se escanean.
- **Las constantes para código** (`GlobalFlags.GiftLeft`) son trabajo futuro: en la v1 se usa un campo `FlagRef`.
- Borrar una escena deja sus flags locales en el grupo "Missing scene" hasta que los borres o los reasignes.
- Los avisos de ejecución pasan por el paquete `logger`, que los elimina en las builds sin `DEBUG`.
