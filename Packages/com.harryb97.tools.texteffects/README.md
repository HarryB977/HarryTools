# HarryTools Text Effects

Typewriter y efectos de texto para TextMeshPro, controlados con marcas dentro del propio texto:

- Typewriter sobre cualquier `TMP_Text`, tanto UGUI (`TextMeshProUGUI`) como 3D (`TextMeshPro`).
- Marcas en el texto: pausas, cambios de velocidad, espera de confirmación, eventos, cambio de voz, bloques instantáneos y efectos.
- Efectos de **aparición**, **continuos**, **de un golpe** y de **desaparición**, definidos como presets (assets) y ampliables con clases propias. Los presets aceptan parámetros opcionales dentro de la marca.
- Sonido por carácter (voz de tecleo), con un sonido del paquete `audio` si está instalado.
- Eventos de texto que ejecutan `ActionList` del paquete `actions`, en una cola secuencial, más un evento C#.
- Texto localizado en el propio componente (`LocalizedString`) si está instalado `com.unity.localization`.
- Validación del marcado en el inspector y entre idiomas, y vista previa en el editor sin entrar en Play.
- Sin allocations por frame mientras el texto se escribe, se anima o se oculta.

> **Beta:** la API aún puede cambiar antes de la 1.0.0.

## Instalación

1. Instala **PrimeTween** antes que el paquete. OpenUPM solo tiene la 1.3.3 y la dependencia pide 1.5.0 o superior, así que usa una de estas dos formas:
   - Desde el Asset Store: tras importarlo, selecciona `Assets/Plugins/PrimeTween/PrimeTweenInstaller.asset` y pulsa **Install**. Eso añade `"com.kyrylokuzyk.primetween": "file:../Assets/Plugins/PrimeTween/internal/com.kyrylokuzyk.primetween-<versión>.tgz"` al `manifest.json`. Solo importar el asset no basta.
   - Desde el registro npm del autor: scope `com.kyrylokuzyk`, URL `https://registry.npmjs.org`. En ese caso quita `com.kyrylokuzyk` de los scopes del registro de OpenUPM si la CLI lo añade.
2. Instala el paquete desde [OpenUPM](https://openupm.com/packages/com.harryb97.tools.texteffects/). La CLI añade también UniTask y los paquetes `com.harryb97.*` de los que depende:

   ```sh
   openupm add com.harryb97.tools.texteffects@1.0.0-beta.1
   ```

Para instalarlo por URL de git hay que añadir también sus dependencias, porque Unity no las resuelve en ese caso:

```json
"com.harryb97.tools.texteffects": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.texteffects#texteffects/1.0.0-beta.1",
"com.harryb97.tools.actions": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.actions#actions/2.0.0-beta.1",
"com.harryb97.tools.conditions": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.conditions#conditions/2.0.0-beta.1",
"com.harryb97.tools.scenerefs": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.scenerefs#scenerefs/1.0.0-beta.1",
"com.harryb97.tools.serializereferences": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.serializereferences#serializereferences/1.0.3",
"com.harryb97.tools.logger": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.logger#logger/1.0.3",
"com.harryb97.tools.domainreload": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.domainreload#domainreload/1.0.3",
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10"
```

Requiere Unity 6000.0 o superior y `com.unity.ugui` 2.0.0 (TextMeshPro).

**Paquetes opcionales** (el paquete compila y funciona sin ellos; el código que los usa se activa solo al detectarlos):

| Paquete | Qué añade |
|---|---|
| `com.unity.localization` | Fuente de texto *Localized* en el componente y la validación de String Tables. |
| `com.harryb97.tools.audio` | El sonido de las voces de tecleo (`SoundRef` del catálogo de `audio`). Sin él, la voz solo filtra qué caracteres suenan y el evento por carácter informa de la voz activa. |

## Puesta en marcha

1. Abre **Project Settings > HarryTools > Text Effects**. La primera vez que se abre la página (fuera de Play) se crea el asset `TextEffectsSettings` en `Assets/HarryTools/TextEffects/Resources/`, ya con los 10 presets del paquete y los efectos por defecto: aparición `fade` y desaparición `fadeout`. Este asset va en git y en la build.
2. Añade un componente **EffectText** a un objeto con `TextMeshProUGUI` o `TextMeshPro` (se añade solo el requisito `TMP_Text`).
3. Escribe el texto con marcas en el inspector y pulsa **Play** en la vista previa del propio componente, o entra en Play.

Mientras el asset `TextEffectsSettings` existe, el registro se mantiene solo: cada preset (`TextEffectPreset`) o voz (`TypingVoice`) que se crea, mueve o borra en el proyecto se añade o se quita de su lista al importar. Si se quita una entrada a mano de la lista, se vuelve a añadir en la siguiente importación. Sin el asset, los presets y las voces no se registran y se avisa por consola.

## Componente EffectText

| Campo | Qué hace |
|---|---|
| **Source Mode** | `Plain` (campo de texto) o `Localized` (`LocalizedString`, solo con `com.unity.localization`). |
| **Characters Per Second** | Velocidad base del typewriter. Con 0 o menos se usa la de Settings (por defecto 40). |
| **Time Mode** | `Default` (el de Settings), `Scaled` o `Unscaled`. |
| **Show On Enable** | Empieza a escribir al activarse el componente. |
| **Appear Preset / Disappear Preset** | Efectos por defecto del componente. Vacíos usan los de Settings; si tampoco hay, las letras aparecen y desaparecen sin animación. |
| **Hide Mode / Hide Order / Hide Character Delay** | La desaparición va letra a letra (del principio al final o al revés, con un retardo entre letras) o todo a la vez. |
| **Default Voice** | Voz de tecleo activa hasta que una marca `<voice>` la cambie. |
| **Events** | Tabla de eventos de texto (ver más abajo). |

### API

| Miembro | Descripción |
|---|---|
| `SetText(string, bool show = true)` | Parsea y carga un texto. Empieza a escribirlo salvo que `show` sea `false`. No vacía la cola de eventos. |
| `Show()` | Empieza a escribir el texto actual desde el principio. |
| `Skip()` | Completa el texto hasta el siguiente `<waitinput>` o hasta el final. Ignora las pausas, aplica los cambios de voz sin sonar y ejecuta los eventos saltados en orden si su entrada lo permite. |
| `Continue()` | Sigue tras un `<waitinput>`. Si no se está esperando, no hace nada. |
| `Hide()` | Lanza la desaparición con efecto. Si se llama mientras escribe, desaparecen las letras ya visibles. |
| `HideImmediate()` | Oculta al instante. |
| `DefaultVoice` | Voz por defecto, también asignable por código. |
| `SetEventProvider(ITextEventProvider)` | Sustituye la tabla de eventos del componente por otra fuente. `null` vuelve a la tabla propia. |
| `State` | `Hidden`, `Typing`, `WaitingInput`, `Complete` o `Hiding`. |
| `Text` | Texto actual, con las marcas. |
| Eventos C# | `CharacterShown` (`CharacterShownArgs`: índice, carácter, voz activa y si se mostró por `Skip`), `EventReached` (nombre del evento), `WaitingInput`, `TypingCompleted`, `Hidden`. |

El componente no lee input: quien lo usa decide cuándo llamar a los métodos. Las llamadas hechas desde un listener mientras el typewriter avanza se aplican al terminar ese avance.

## Marcas del texto

Las marcas usan los mismos `< >` que TextMeshPro. El parser solo consume las que reconoce (las reservadas de abajo y los nombres de presets y voces registrados) y deja el resto tal cual para TextMeshPro, así que `<color>`, `<b>`, `<sprite>`, `<link>` y demás siguen funcionando. Para escribir un `<` literal que coincida con una marca, usa `<noparse>…</noparse>`.

**Marcas puntuales** (no se cierran):

| Marca | Efecto |
|---|---|
| `<pause=s>` | Detiene el typewriter `s` segundos. |
| `<speed=x>` | Multiplica la velocidad base por `x` desde ese punto. `<speed=1>` vuelve a la base. |
| `<waitinput>` | Detiene el typewriter hasta que se llame a `Continue()`. |
| `<event=nombre>` | Dispara el evento `nombre` al llegar a ese punto. |
| `<voice=nombre>` | Cambia la voz de tecleo desde ese punto. |

**Marcas de rango:**

| Marca | Efecto |
|---|---|
| `<instant>…</>` | El bloque aparece entero de golpe (sus letras aplican su efecto de aparición a la vez). |
| `<preset>…</>` | Aplica el preset a ese tramo. Varios presets pueden solaparse. |
| `<preset param=valor param2=valor>…</>` | Igual, sobrescribiendo parámetros del preset solo en ese tramo. |

**Cierre de marcas de rango:** `</>` cierra la última marca de rango propia abierta (no cierra marcas de TextMeshPro); `</nombre>` cierra la última abierta con ese nombre; una marca de rango sin cerrar llega hasta el final del texto.

**Nombres reservados:** `pause`, `speed`, `waitinput`, `event`, `voice`, `instant` y las marcas de TextMeshPro. Un preset o una voz con nombre reservado, repetido o con caracteres no válidos (solo letras, dígitos, `_` y `-`) no se registra y se avisa en Settings. Los nombres no distinguen mayúsculas.

**Valores de los parámetros:** se asignan al campo serializado del efecto con el mismo nombre (sin distinguir mayúsculas). Se admiten `float`, `int`, `bool`, enumerados por nombre, colores en formato HTML (`#rrggbb`, `red`…) y vectores separados por comas. Un parámetro desconocido o con un valor no válido genera un aviso y se ignora.

## Tipos de efecto y presets

Cada `TextEffect` declara su tipo:

| Tipo | Cuándo se ejecuta |
|---|---|
| **Aparición** | Durante un tiempo desde que la letra se hace visible (progreso de 0 a 1 con un `Ease` de PrimeTween). |
| **Continuo** | Sin fin, mientras la letra está visible. |
| **De un golpe** | Una vez, durante un tiempo, cuando aparece la primera letra de su tramo. Afecta a todo el tramo a la vez. |
| **Desaparición** | Durante `Hide()`. |

Los presets de aparición y de desaparición también valen como marca de rango: sustituyen al efecto por defecto en ese tramo.

Presets incluidos (se pueden duplicar y editar, o crear otros con **Create > HarryTools > Text Effects > Effect Preset**):

| Preset | Tipo | Parámetros del efecto |
|---|---|---|
| `fade` | Aparición | `duration`, `ease`, `direction` |
| `scale` | Aparición | `duration`, `ease`, `from`, `direction` |
| `slide` | Aparición | `duration`, `ease`, `offset`, `direction` |
| `fadeout` | Desaparición | `duration`, `ease`, `direction` |
| `shake` | Continuo | `amplitude`, `frequency` |
| `wave` | Continuo | `amplitude`, `frequency`, `wavelength` |
| `pulse` | Continuo | `min`, `frequency` (pulso de alfa) |
| `colorpulse` | Continuo | `color`, `frequency` |
| `shakeonce` | De un golpe | `duration`, `ease`, `amplitude` |
| `punch` | De un golpe | `duration`, `ease`, `strength` |

Los parámetros que se pueden escribir en una marca son los campos serializados del efecto que contiene el preset; se ven en su inspector.

### Efectos propios

Hereda de `TextEffect`, declara los parámetros como campos serializados y escribe `Apply`. El efecto aparece en el selector de tipos del preset igual que los del paquete, y sus campos se pueden sobrescribir desde las marcas.

```csharp
using HarryTools.TextEffects;
using UnityEngine;

[System.Serializable]
public sealed class TiltEffect : TextEffect {
    [SerializeField] private float degrees = 8f;
    [SerializeField] private float frequency = 1f;

    public override EffectKind Kind => EffectKind.Continuous;

    public override void Apply(ref CharacterState _char, in EffectTime _time) {
        _char.RotateAroundCenter(Mathf.Sin(_time.GlobalTime * frequency * Mathf.PI * 2f) * degrees);
    }
}
```

`CharacterState` da acceso a las cuatro esquinas y colores de la letra (`BottomLeft`, `TopLeft`, `TopRight`, `BottomRight`, `ColorBL`…), su índice y el de su tramo, y trae ayudas (`Translate`, `ScaleAroundCenter`, `RotateAroundCenter`, `MultiplyAlpha`, `LerpColor`). `EffectTime` da `Elapsed`, `Progress` (0 a 1 sin easing; los continuos reciben 0 y usan `GlobalTime`) y `GlobalTime`. `Apply` se llama cada frame por cada letra afectada: no debe reservar memoria. La base aporta `duration` y `ease` (`Eased(t)`).

## Voz de tecleo

`TypingVoice` (**Create > HarryTools > Text Effects > Typing Voice**) define:

- **Tag Name**: nombre para `<voice=nombre>`.
- **Every N Characters**: suena cada N caracteres que pueden sonar.
- **Skip Whitespace / Skip Punctuation**: qué caracteres no suenan.
- **Pitch Range**: multiplicador de tono aleatorio (mínimo y máximo).
- **Sound**: sonido del catálogo de `audio` (solo con `com.harryb97.tools.audio`). En un `TextMeshPro` 3D suena en la posición del componente; en UGUI, sin posición.

El componente usa su **Default Voice** hasta que `<voice=nombre>` la cambia; el cambio dura hasta el final del texto o hasta la siguiente marca `<voice>`. Mostrar el texto con `Skip` no hace sonar los caracteres saltados. Sin `audio`, el evento `CharacterShown` informa de la voz activa para reproducir el sonido por otra vía.

## Eventos y cola de acciones

La tabla **Events** del componente son entradas `{ nombre, ActionList, ejecutar al saltar }`. Al llegar a `<event=nombre>`:

1. Se dispara el evento C# `EventReached` en ese momento.
2. Si hay una entrada con ese nombre (sin distinguir mayúsculas), su `ActionList` entra en la **cola de acciones** del componente: cada lista espera a que termine la anterior. El typewriter no espera a la cola.
3. Con `Skip`, los eventos saltados entran en la cola en orden, salvo los de entradas con *Run When Skipped* desactivado.

`SetText` y `Hide` no vacían la cola; desactivar o destruir el componente la cancela. Un `<event>` sin entrada solo dispara el evento C#, y la validación lo avisa.

Una fuente de eventos distinta de la tabla se puede dar por código con `SetEventProvider(ITextEventProvider)`.

### Acciones

Para el paquete `actions`, con un `EffectText` como objetivo (desplegable **Text Effects**):

| Acción | Descripción |
|---|---|
| **Show Text** | Llama a `Show()`. Con *Wait Until Complete* espera a que acabe de escribirse, incluidas las esperas de `<waitinput>`. |
| **Hide Text** | Llama a `Hide()`. Con *Wait Until Hidden* espera a que quede oculto. |
| **Skip Text** | Llama a `Skip()`. |
| **Continue Text** | Llama a `Continue()`. |

## Localización

Con `com.unity.localization` instalado, el componente acepta un `LocalizedString` (modo *Localized*), que se edita con el dibujado de Unity. Reglas al cambiar el idioma en ejecución:

- Si el texto se estaba escribiendo (o esperando `<waitinput>`), vuelve a empezar con el texto nuevo, pero los eventos ya disparados (por posición: el n-ésimo `<event>`) no se repiten.
- Si estaba completo, se muestra completo sin typewriter.
- Si se estaba ocultando, queda oculto con el texto nuevo.

`SetText(string)` sigue disponible para alimentar el texto desde fuera. El paquete solo depende del paquete de localización de Unity.

## Validación

En el inspector del componente, siempre visible y actualizada al editar:

- Marca de rango mal formada, `</>` sin nada abierto, parámetro inexistente o con valor no válido.
- Preset o voz desconocidos.
- `<event>` sin entrada en la tabla del componente.
- Entre idiomas (modo *Localized*): comparado con el idioma base del proyecto, cada traducción debe tener los mismos `<event>` y `<waitinput>` (mismo número y nombres), y es un **error** si no. Que falten o sobren pausas o presets es un **aviso**.

El menú **Tools > HarryTools > Text Effects > Validate String Tables** valida todas las String Table Collections del proyecto (sintaxis, presets, voces y paridad entre idiomas) y escribe el resultado en la consola. No comprueba los nombres de eventos contra ninguna tabla, porque dependen de quién use la cadena. Requiere `com.unity.localization`.

## Vista previa en el editor

En el inspector de `EffectText`, en edit mode y con el objeto activo: selector de idioma (con `LocalizedString`) y botones **Play**, **Skip**, **Continue**, **Hide**, **Restart** y **Stop**. El texto se ve en la Scene y la Game view con sus efectos, pausas y desaparición. Los eventos se listan en el inspector según ocurren, sin ejecutar sus acciones, y no hay sonido. Al parar o deseleccionar, el TMP vuelve a su estado y la escena y el prefab no se modifican.

## Rendimiento

- El texto se parsea y se preparan los buffers solo al cambiar el texto; ahí sí se reserva memoria.
- Por frame no hay allocations mientras el texto se escribe, se anima o se oculta; un test mide `GC.Alloc` para comprobarlo.
- Los efectos se calculan en un solo bucle sobre las letras visibles, sin tweens por letra: el `Ease` de PrimeTween se evalúa como función.
- Un texto completo sin efectos continuos deja de tocar el mesh.

## Notas

- El estado estático es compatible con Play sin recarga de dominio.
