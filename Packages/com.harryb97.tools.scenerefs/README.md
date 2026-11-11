# SceneRefs

Referencias a objetos de escena por un ID único y legible, desde cualquier escena o asset:

- `SceneRef<T>`: un solo campo para el inspector que acepta objetos de escena (de la misma escena, de otra o desde un asset como un ScriptableObject) y los recupera por su ID en ejecución.
- IDs legibles y editables (`Manzana01`, `Manzana02`), únicos en todo el proyecto, que se muestran como una fila en la cabecera del GameObject.
- Registro de ejecución que incluye los objetos inactivos, para poder activar por ID un objeto apagado.
- Índice de IDs del proyecto, con selector de objetos incluso de escenas cerradas.
- Un ID repetido bloquea Play y la build, con un informe en consola.
- Integrado en `conditions` y `actions`: la opción `Custom` de las referencias a objetos usa `SceneRef<T>`.

> **Beta:** la API aún puede cambiar antes de la 1.0.0.

## Instalación

Instala el paquete desde [OpenUPM](https://openupm.com/packages/com.harryb97.tools.scenerefs/). La CLI añade también los paquetes `com.harryb97.*` de los que depende:

```sh
openupm add com.harryb97.tools.scenerefs@1.0.0-beta.1
```

Para instalarlo por URL de git hay que añadir también sus dependencias, porque Unity no las resuelve en ese caso:

```json
"com.harryb97.tools.scenerefs": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.scenerefs#scenerefs/1.0.0-beta.1",
"com.harryb97.tools.logger": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.logger#logger/1.0.3",
"com.harryb97.tools.domainreload": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.domainreload#domainreload/1.0.3"
```

Los paquetes `conditions` y `actions` 2.0.0-beta.1 dependen de este, así que quien los use debe tenerlo también en su manifest.

## Cómo funciona

Cada objeto referenciable lleva un componente oculto `SceneId` con dos valores:

| Valor | Qué es |
|---|---|
| **guid** | Identificador interno (32 caracteres hexadecimales). Lo genera la herramienta, nunca cambia y es lo que guardan las referencias. |
| **ID visible** | Texto legible (`Manzana01`). Se edita en la cabecera del GameObject, sirve para identificar el objeto, para búsquedas desde código y para los mensajes. Único en todo el proyecto, sin distinguir mayúsculas de minúsculas. |

Como la referencia guarda el guid y no el nombre, puedes renombrar un ID visible sin romper ninguna referencia en escenas cerradas ni en assets.

## Uso

### El campo `SceneRef<T>`

```csharp
using HarryTools.SceneRefs;

[SerializeField] private SceneRef<Light> lamp;        // lamp.Value es un Light
[SerializeField] private SceneRef<GameObject> door;   // door.Value es un GameObject
```

`T` puede ser `GameObject` o cualquier `Component`. `Value` devuelve el tipo pedido, sin `GetComponent`. `IsAssigned` distingue un campo vacío de uno asignado cuyo objeto no está cargado.

El campo guarda siempre las dos cosas: la referencia directa de Unity (si Unity puede guardarla en ese sitio) y el guid. Al resolver, usa la referencia directa si sigue viva y, si no, busca el guid en el registro de ejecución. Si el campo apunta a un componente, guarda también cuál exactamente: su tipo concreto y su posición entre los componentes de ese tipo (el segundo `AudioSource` sigue siendo el segundo).

### Asignar un objeto

Arrastra un GameObject o un componente de una escena al campo.

- Si el objeto no tenía ID, se le añade uno automáticamente, a partir de su nombre.
- Si el campo pide `T` y arrastras un GameObject, toma su `T`. Si no la tiene, se rechaza. Los assets también se rechazan.
- Entre dos escenas distintas Unity no deja guardar la referencia directa, así que solo se guarda el guid. Esto es lo que permite usarlo desde un ScriptableObject.
- El botón `▾` abre un selector con buscador con todos los objetos del índice que tienen un `T`, agrupados por escena, **incluidas las escenas cerradas**. De las escenas abiertas lista los objetos tal como están, también los que aún no se han guardado.
- El campo muestra el objeto, el ID y la escena (`Manzana01 · Cocina`). Si el guid no está en el índice, se muestra en rojo con `missing`.

### La fila de ID en la cabecera

En el inspector de un GameObject, debajo de Tag y Layer:

```
ID   [Manzana01        ]  [⟳] [✕]      con ID
ID   [ Add ID ]                         sin ID
```

- **Add ID** añade un ID con el nombre del objeto como base. Si ya existe, se incrementa el número final (`Manzana01` pasa a `Manzana02`, `Puerta` a `Puerta1`) respetando los ceros a la izquierda. Los caracteres que no sean letras, números ni `-` se convierten en `_` (`Puerta (1)` pasa a `Puerta_1`).
- Al escribir un ID que ya usa otro objeto, el campo se pone en rojo y una línea dice `Used by <escena>/<ruta>`. Se guarda igualmente, pero Play y la build quedan bloqueados hasta que lo corrijas.
- **⟳** genera un guid nuevo y deja el ID visible igual. Pide confirmación, porque las referencias existentes a este objeto se rompen.
- **✕** quita el ID.
- Con varios objetos seleccionados, la fila muestra `Add ID` (añade uno a cada objeto) o `—` si los IDs son distintos. No se pueden editar varios IDs a la vez.
- La fila no aparece en el modo prefab ni al seleccionar un prefab en la ventana Project.
- Todo lo anterior admite Undo.

Al duplicar un objeto (Ctrl+D) o pegarlo, la copia recibe guid nuevo y el ID incrementado, y las `SceneRef` de la copia que apuntaban a objetos de la jerarquía original apuntan a la copia.

### Índice y Rebuild

El índice es un asset solo de editor (`Assets/HarryTools/SceneRefs/SceneIdIndex.asset`, se crea solo) con todos los IDs del proyecto: en qué escena están, su ruta en la jerarquía y los tipos de componente de cada objeto. Es **una caché**: todo su contenido se puede reconstruir a partir de las escenas, así que no hace falta editarlo a mano ni resolver sus conflictos de git (ver [Equipos y git](#equipos-y-git)).

- Se actualiza al guardar una escena. Las escenas que cambian fuera del editor (git pull, merge, duplicar el asset) se reindexan leyendo su YAML, sin abrirlas, siempre que estén guardadas como texto. Esa lectura ligera no conoce los tipos de componente; se completan en el siguiente guardado o Rebuild.
- Su inspector es de solo lectura: tabla con buscador, conflictos en rojo, el botón **Rebuild Index** y un botón **Regenerate IDs in scene** por cada escena en conflicto.
- **Tools > HarryTools > SceneRefs > Rebuild Id Index** (o el botón del inspector) abre cada escena no cargada en modo aditivo, la lee con exactitud y la cierra, y revisa también los prefabs de Assets. Pregunta antes si hay escenas con cambios sin guardar.

### Qué bloquea Play y la build

Estos conflictos impiden entrar en Play y hacer una build (la build comprueba todas las escenas del índice, no solo las de la build):

- Dos objetos con el mismo guid.
- Dos objetos con el mismo ID visible (sin distinguir mayúsculas).
- Un `SceneId` dentro de un prefab de Assets.

Cada conflicto se informa en consola; al hacer clic en el mensaje se selecciona el objeto si su escena está abierta. Antes de entrar en Play se tienen en cuenta los cambios sin guardar de las escenas abiertas. Si la propia validación falla con un error, Play y la build también se bloquean y el error sale en consola.

Los casos típicos son una escena duplicada desde la ventana Project o un merge de git que trae un guid repetido. No se elige automáticamente quién cambia: usa **Regenerate IDs in scene** en el inspector del índice para la escena que quieras corregir, o ⟳ en un objeto concreto. Regenerar una escena da a cada objeto un guid nuevo y un ID incrementado, y actualiza las `SceneRef` de esa escena que apuntaban a sus propios objetos.

### Prefabs

Los prefabs de la carpeta Assets no tienen ID.

- Al crear o guardar un prefab, la herramienta quita los `SceneId` del asset. Si lo creaste desde un objeto de escena con ID, la instancia que queda en la escena lo conserva (mismo guid y mismo ID) como componente añadido.
- Una instancia de prefab en una escena puede tener su propio ID. Ese es el caso normal.
- Una referencia entre objetos del mismo prefab se guarda solo como referencia directa, sin ID. Vale tanto en el asset como en cada instancia de la escena: una instancia cuyo componente apunta a un hijo suyo no recibe ID ni overrides.
- Un prefab de Assets que apunte a un objeto de escena (solo posible desde el selector `▾`) guarda solo el guid.

### Equipos y git

- **Sube el índice al repositorio** (`Assets/HarryTools/SceneRefs/SceneIdIndex.asset` y su `.meta`). Sin él, un clon nuevo o la build en CI no conocen las escenas cerradas: la validación no las comprueba y el selector `▾` no las muestra hasta hacer un Rebuild.
- El índice se reescribe al guardar escenas, así que es normal que tenga conflictos de merge. **Si hay un conflicto en el índice, o no se puede cargar, pulsa Rebuild Index** (en su inspector o en **Tools > HarryTools > SceneRefs > Rebuild Id Index**). No hace falta resolver el conflicto a mano: quédate con cualquiera de las dos versiones y reconstruye. Si el archivo no se puede cargar (por ejemplo, con marcadores de conflicto), se recrea vacío y un aviso en consola pide el Rebuild.
- **Save Scene As o duplicar una escena duplica sus IDs.** La copia tiene los mismos guid e IDs que el original, así que Play y la build quedan bloqueados hasta que pulses **Regenerate IDs in scene** en la escena que quieras cambiar.
- **Para mover un objeto a otra escena, arrástralo en la Hierarchy** de una escena a otra. Así conserva su guid. Copiar y pegar crea un objeto nuevo con guid nuevo: las referencias al original se rompen cuando borras el original.

### Desde código

```csharp
GameObject apple = SceneRefs.Find("Manzana01");                // null si no está cargado
if (SceneRefs.TryFind("Manzana01", out Light light)) { ... }   // componente del objeto

// Objetos creados en ejecución:
GameObject clone = Instantiate(prefab);
SceneRefs.Assign(clone, "Manzana99");   // false y error en consola si el ID ya existe
```

`Find` y `TryFind` buscan por ID visible, sin distinguir mayúsculas, entre los objetos de las escenas cargadas (inactivos incluidos). `Assign` añade un `SceneId` con guid nuevo y lo registra.

## Integración con `conditions` y `actions`

En `TargetGameObject` y `TargetComponent<T>`, la opción `Custom` es un `SceneRef`: las acciones y condiciones que apuntan a objetos aceptan objetos de otra escena desde un ScriptableObject sin hacer nada más. Los campos `BehaviourIsEnabled.behaviour`, `SetBehaviourEnabledAction.behaviours`, `RunActionRunnerAction.runner`, `CancelActionRunnerAction.runner` e `InstantiateAction.parent` pasan a `TargetComponent<T>`.

## Limitaciones

- **Recrear un objeto con el mismo ID no reconecta las referencias.** Si borras un objeto y creas otro con el mismo ID visible, las referencias al viejo siguen apuntando a su guid, que ya no existe.
- **El aviso usa la etiqueta del momento de asignar.** Una referencia que no se resuelve avisa con el ID y la escena que tenía cuando se asignó (`<guid> (last known as Manzana01 in Cocina)`). Renombrar un ID no reescribe escenas ni assets. El aviso sale una vez por referencia y carga de escena.
- **Las copias por `Instantiate` de objetos de escena con ID no se registran.** El registro solo se llena al cargar escenas y con `SceneRefs.Assign`. Si una copia necesita ID, asígnaselo por código justo después de instanciarla.
- **Awake y OnEnable.** Una `SceneRef` resuelta durante `Awake` u `OnEnable` puede devolver `null` y avisar si su escena de destino se carga en el mismo lote. Un acceso posterior ya la resuelve, porque no se guarda el `null` en caché.
- **Los avisos de ejecución pasan por el paquete `logger`**, que los elimina en las builds sin `DEBUG`.
- **`SceneRefs.Assign` no sanea los IDs.** El texto se usa tal cual; solo la fila de cabecera y la asignación automática limpian los caracteres problemáticos.
- **Conflicto de nombres.** La clase `SceneRefs` vive en el namespace `HarryTools.SceneRefs`. Dentro de código en otro namespace `HarryTools.*`, `SceneRefs` puede resolverse al namespace en lugar de a la clase. Usa `HarryTools.SceneRefs.SceneRefs` o un alias (`using SceneRegistry = HarryTools.SceneRefs.SceneRefs;`).
- **Referenciar un objeto de escena en modo Play** le añade un ID que se pierde al salir de Play, igual que cualquier cambio hecho en ese modo.
- No se espera a que una escena se cargue: si el objeto no está cargado, se devuelve `null`, se avisa y se sigue.
- Los IDs en prefabs de Assets no están soportados (ver Prefabs).
