# Audio

Sistema de audio para Unity, inspirado en [JSAM](https://github.com/Jacky-Yang/JSAM) (MIT):

- Catálogo de assets de audio (sonidos, música, voces, conversaciones y perfiles espaciales) con una clase generada para acceder a ellos desde código.
- Reproducción por bus con pooling, límite de instancias, cooldown y prioridades, sin GC en el camino normal.
- Música con crossfade, intro más loop y puntos de loop, y zonas de música.
- Voces y conversaciones con emisores, presets de efecto y colas.
- Volumen, mute, pausa y ducking por bus, con persistencia.
- Módulos acústicos enchufables (oclusión, reverb...) y compatibilidad con WebGL.
- Acciones opcionales para `actions` y un selector con búsqueda y previsualización en el inspector.
- Sample opcional de subtítulos localizados (requiere Unity Localization), con eventos para pintarlos con tu propia UI.

> **Beta:** la API aún puede cambiar antes de la 1.0.0.

## Instalación

Instala el paquete desde [OpenUPM](https://openupm.com/packages/com.harryb97.tools.audio/). La CLI añade también UniTask y los paquetes `com.harryb97.*` de los que depende:

```sh
openupm add com.harryb97.tools.audio@1.0.0-beta.2
```

Para instalarlo por URL de git hay que añadir también sus dependencias, porque Unity no las resuelve en ese caso. UniTask (`com.cysharp.unitask`) se instala desde OpenUPM:

```json
"com.harryb97.tools.audio": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.audio#audio/1.0.0-beta.2",
"com.harryb97.tools.gameloop": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.gameloop#gameloop/1.1.0",
"com.harryb97.tools.logger": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.logger#logger/1.0.3",
"com.harryb97.tools.domainreload": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.domainreload#domainreload/1.0.3",
"com.harryb97.tools.serializereferences": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.serializereferences#serializereferences/1.0.3",
"com.harryb97.harryutils": "https://github.com/HarryB977/HarryUtils.git#2.1.0",
"com.cysharp.unitask": "2.5.10"
```

Las acciones (carpeta `Actions/`, asmdef `HarryTools.Audio.Actions`) solo se compilan si el proyecto tiene los paquetes `actions` y `conditions`.

## Primeros pasos

1. Abre **Edit > Project Settings > HarryTools > Audio** (o **Tools > HarryTools > Audio > Open Settings**). Ahí se crea el `AudioProjectSettings` y se configuran el mixer, el catálogo, los buses, el ducking, los límites de voces y la ruta del fichero de ids generado.
2. Crea la carpeta `Assets/Audio` (es la **Category Root Folder** por defecto). Sus subcarpetas pasan a ser los grupos de la clase generada.
3. Crea un sonido con **Create > HarryTools > Audio > Sound** dentro de, por ejemplo, `Assets/Audio/Player/`, y asigna uno o varios clips.
4. Reprodúcelo:

```csharp
using HarryTools.Audio;

Audio.Play(Sounds.Player.Jump);                       // sin posición: sigue al AudioListener (origen si no hay)
Audio.Play(Sounds.Player.Jump, transform.position);   // 3D en un punto
Audio.Play(Sounds.Player.Footstep, transform);        // 3D siguiendo a un transform
```

El mixer es opcional: si no hay, el volumen se multiplica en cada `AudioSource`.

> Dentro de código en un namespace `HarryTools.*` distinto de `HarryTools.Audio`, `Audio` puede resolverse al namespace en lugar de a la clase. Usa `HarryTools.Audio.Audio` o un alias (`using AudioSystem = HarryTools.Audio.Audio;`), igual que `UI` en el paquete `ui`.

## Tipos de asset

| Asset | Menú | Para qué |
|---|---|---|
| **Sound** | Create > HarryTools > Audio > Sound | Efectos. Bus SFX por defecto |
| **Music** | Create > HarryTools > Audio > Music | Pistas largas con intro y puntos de loop. Bus Music |
| **Voice** | Create > HarryTools > Audio > Voice | Una línea de diálogo con su `Speaker`. Bus Voice |
| **Conversation** | Create > HarryTools > Audio > Conversation | Lista de voces con pausa entre líneas |
| **Speaker** | Create > HarryTools > Audio > Speaker | Quién habla; puede llevar un preset de efecto por defecto |
| **Effect Preset** | Create > HarryTools > Audio > Effect Preset | Filtros paso bajo/alto, distorsión y eco, y una salida de mixer opcional |
| **Spatial Profile** | Create > HarryTools > Audio > Spatial Profile | Ajustes espaciales reutilizables; sustituyen a los inline |
| **Catalog** | Create > HarryTools > Audio > Catalog | Lista de todos los assets; lo mantiene el editor |

Los sonidos, músicas y voces comparten estos ajustes: selección de clip (Random, RandomNoRepeat, Shuffle, Sequence), rango de volumen y pitch aleatorios, delay, **Cooldown**, **Max Instances** con su política (StealOldest, StealQuietest, Ignore), prioridad, bus, salida de mixer propia, ajustes espaciales, loop, FadeIn/FadeOut, *Ignore Pause* y *Follow Time Scale*.

Con el pool lleno, un sonido nuevo quita la voz menos audible (prioridad × volumen × atenuación estimada por distancia) entre las de prioridad igual o menor a la suya, y solo si es menos audible que él; si no, no suena. Una voz de prioridad mayor nunca se quita, por lejos que esté.

### IDs y clase generada

Cada asset recibe un ID estable. Si lo duplicas con Ctrl+D, el duplicado recibe uno nuevo y el original no cambia. El editor mantiene el catálogo y genera un fichero de ids (por defecto `Assets/HarryTools/Audio/AudioIds.g.cs`) con las clases `Sounds`, `Music`, `Voices` y `Conversations`, agrupadas por subcarpeta:

```csharp
Audio.Play(Sounds.Player.Jump);
Audio.Music.Play(Music.Menu.Theme);
Audio.Voices.Play(Voices.Npc.Greeting);
```

- Puedes forzar la regeneración con **Tools > HarryTools > Audio > Regenerate Ids**.
- La ubicación del fichero y el namespace de las clases (por defecto el global) se cambian en Project Settings. Se compila en el assembly de la carpeta donde esté: si el juego usa sus propios asmdef, ponlo dentro de la carpeta de uno de ellos.
- Renombrar o borrar un asset hace que el código que lo usaba deje de compilar. Es a propósito: así no quedan referencias rotas en silencio.
- Los campos `SoundRef`, `MusicRef`, `VoiceRef` y `ConversationRef` guardan el ID, no el asset. Una referencia a un asset que todavía no está en el catálogo se muestra como "Missing" hasta que el catálogo se reconstruye.
- El selector del inspector busca por nombre y tiene un botón ▶ para previsualizar el audio.

## Handles

`Audio.Play` devuelve un `AudioHandle`, una estructura con generación: si la voz ya se reutilizó para otro sonido, el handle antiguo no puede parar ni modificar el nuevo.

```csharp
AudioHandle h = Audio.Play(Sounds.Ambience.Wind, transform)
    .WithVolume(0.6f, 0.8f)
    .WithPitch(0.95f, 1.05f);

h.SetVolume(0.2f, 1.5f);   // a 0.2 en 1.5 s
h.At(otraPosicion);
h.Follow(otroTransform);
h.Pause(); h.Resume();
h.Stop(0.5f);              // con fundido; -1 usa el FadeOut del asset

h.OnEnded(_h => Debug.Log("fin"));
await h.WaitForEnd(token);

Audio.Started += _h => { };  Audio.Ended += _h => { };
Audio.Stop(Sounds.Ambience.Wind);
Audio.StopAll(0.5f);      // sonidos, voces y conversaciones; la música sigue (Audio.Music.Stop)
```

`IsValid`, `IsPlaying`, `IsPaused` y `Asset` consultan el estado actual. Si el transform que sigue un sonido se destruye, el sonido continúa en su última posición y termina con normalidad.

## Música

```csharp
Audio.Music.Play(Music.Menu.Theme);          // crossfade -1
Audio.Music.Play(Music.Level.Forest, 2f);    // crossfade de 2 s
Audio.Music.Stop();                          // Stop(-1)
Audio.Music.Pause(); Audio.Music.Resume();
Audio.Music.Changed += _asset => { };
```

- Crossfade `-1`: usa el `FadeIn` del asset si no suena nada; si ya suena algo, usa `DefaultCrossfade` de los ajustes. `Stop(-1)` usa el `FadeOut` del asset.
- Una pista puede tener un **Intro** que suena una vez antes del loop (`Clips[0]` es el loop), o **puntos de loop** (LoopStart/LoopEnd) dentro del clip. Los puntos de loop necesitan un rango de al menos 0,5 s.
- El pitch de la música es siempre 1: ni el rango de pitch ni *Follow Time Scale* se aplican.

### Zonas de música

Añade un **MusicZone** (Add Component > HarryTools > Audio > Music Zone) a un objeto con un collider (se recomienda trigger): Box, Sphere, Capsule o MeshCollider convexo. Mientras el listener esté dentro, suena su música; si hay zonas solapadas gana la de mayor `Priority` y, en empate, la última en la que se entró.

- Los `MeshCollider` tienen que ser convexos: la zona se comprueba con `ClosestPoint`.
- Al salir de todas las zonas se restaura la música previa a la primera zona, aunque se haya llamado a `Audio.Music.Play` estando dentro.

## Voces y conversaciones

```csharp
Audio.Voices.Play(Voices.Npc.Greeting, emisor);          // AudioHandle
ConversationHandle c = Audio.Voices.Play(Conversations.Shop.Intro);
await c.WaitForEnd(token);
c.Stop();
int linea = c.CurrentLine;

Audio.Voices.LineStarted += _info => { };
Audio.Voices.LineEnded += (_info, _interrupted) => { };
Audio.Voices.StopAll();
```

- Una conversación reproduce sus líneas en orden, con la `PauseAfter` de cada una. Esa pausa no cuenta mientras el audio (o el bus de la línea) o `AudioListener.pause` están en pausa, salvo que la voz tenga *Ignore Pause*.
- Las líneas en cola reservan una voz del pool: dimensiona **Max Voices** teniendo en cuenta las conversaciones que puedan estar activas a la vez.
- Si a una línea en cola le roban su voz, se descarta sin lanzar eventos.
- La reserva cuenta para `MaxInstances` y arranca el `Cooldown` del asset.

### Emisores

El componente **AudioEmitter** representa a un personaje: tiene un `Speaker`, un `Effect` opcional (si está vacío, usa el `DefaultEffect` del speaker) y una `BusyPolicy` (Queue: las líneas esperan su turno; Interrupt: una línea nueva corta la actual).

```csharp
emisor.Say(voz);       // VoiceAsset (p. ej. asignado en el inspector)
emisor.Play(sonido);   // SoundAsset, con el efecto del emisor
bool habla = emisor.IsSpeaking;
```

`Say` y `Play` del emisor reciben assets (`VoiceAsset`, `SoundAsset`), no las referencias generadas. Con una referencia, usa `Audio.Voices.Play(Voices.Npc.Greeting, emisor)`.

Si se destruye el GameObject del emisor en mitad de una línea, la conversación continúa en 2D.

### Presets de efecto: el teléfono

1. Crea un **Effect Preset** y activa *Low Pass* (cutoff 3000 Hz), *High Pass* (cutoff 500 Hz) y algo de *Distortion*.
2. Asígnalo como `Effect` del emisor del teléfono (o como `DefaultEffect` del speaker "Teléfono").
3. Cualquier línea o sonido que pase por ese emisor suena como una llamada.

## Buses, volumen, pausa y ducking

Los buses son `Master`, `Music`, `SFX`, `Ambience`, `Voice` y `UI`. Cada uno se asigna a un grupo del mixer y a un parámetro de volumen expuesto en dB (`MasterVolume`, `MusicVolume`...).

```csharp
Audio.SetVolume(AudioBus.Music, 0.5f);   // lineal 0..1
Audio.SetMuted(AudioBus.SFX, true);
float v = Audio.GetVolume(AudioBus.Music);
```

El volumen 0 se traduce a -80 dB (nunca `-Infinity`) y quitar el mute restaura el volumen anterior.

**Pausa**: es explícita, solo con `Audio.Pause()` y `Audio.Resume()`; no depende de `Time.timeScale`. `Audio.Pause(AudioBus.Master)` pausa todos los buses. Los assets con *Ignore Pause* siguen sonando, y *Follow Time Scale* (opcional por asset) escala el pitch con `Time.timeScale`.

**Ducking**: cada regla indica un bus disparador, un bus objetivo, la atenuación en dB y los tiempos de ataque y liberación. La atenuación se aplica como un desplazamiento en dB sobre el parámetro de volumen expuesto, y el disparador son las voces del bus que suenan y no están en pausa. Por ejemplo, bajar Music -8 dB mientras suena Voice.

### Persistencia

Los volúmenes y el mute se guardan en `PlayerPrefs` por defecto (clave `PersistenceKey` en los ajustes), con un guardado diferido para no escribir en cada cambio. Para otro sistema de guardado, implementa `IAudioSettingsStore` (por ejemplo, un adaptador para `localdata`) y asígnalo con `Audio.SetStore`, que carga los datos al asignarlo:

```csharp
public sealed class MiStore : IAudioSettingsStore {
    public async UniTask<AudioVolumeData?> Load() {
        // devuelve null si todavía no hay nada guardado
        return null;
    }

    public async UniTask Save(AudioVolumeData _data) {
        // guarda _data donde corresponda
    }
}

Audio.SetStore(new MiStore());
```

## Componentes

- **SoundPlayer** (Add Component > HarryTools > Audio > Sound Player): reproduce un sonido al activarse, al empezar o a mano (`Play`/`Stop`). Puede seguir al objeto y pararse al desactivarse.
- **AudioEmitter** y **MusicZone**: ver arriba.

## Acciones

Si el proyecto tiene `actions`, aparecen estas acciones en el menú **Audio**: *Play Sound*, *Stop Sound*, *Play Music*, *Stop Music*, *Play Voice*, *Play Conversation*, *Set Bus Volume*, *Pause Audio* y *Resume Audio*. Las de reproducción pueden esperar a que termine el audio y, si la lista se cancela, lo paran.

## Módulos acústicos

El núcleo no tiene código de geometría. Los efectos acústicos (oclusión, reverb...) los aportan módulos que implementan `IAcousticModule` y se registran con `Audio.RegisterModule(m)` / `Audio.UnregisterModule(m)`.

```csharp
public sealed class MiOclusion : IAcousticModule {
    public int Order => 0;
    public float UpdateInterval => 0.1f;   // 0 = en cada tick

    public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
        for (int i = 0; i < _queries.Length; i++) {
            bool tapado = Physics.Linecast(_queries[i].ListenerPosition, _queries[i].SourcePosition);
            _results[i].VolumeMultiplier = tapado ? 0.4f : 1f;
            _results[i].LowPassCutoff = tapado ? 1500f : AcousticResult.NeutralLowPass;
        }
    }
}
```

Contrato:

- Cada módulo escribe su propio resultado partiendo de un `AcousticResult.Neutral`; no se acumula sobre lo de otros módulos.
- El núcleo combina los resultados de todos los módulos en orden: los volúmenes se multiplican, gana el paso bajo más bajo y el paso alto más alto, y la posición percibida es la del último módulo que la fija.
- Un módulo con `UpdateInterval` sigue aportando su último resultado entre evaluaciones.
- Solo se simulan hasta `MaxSimulated` voces por tick.
- Un módulo que lanza una excepción se registra en la consola y se desregistra.
- `Evaluate` no debe reservar memoria si el juego busca cero GC.

## WebGL

En WebGL no hay efectos ni parámetros de mixer, así que:

- El volumen, el mute y el ducking por bus se aplican a `AudioSource.volume`.
- Los presets de efecto, los filtros y los módulos acústicos no se aplican.
- No se usa `PlayScheduled`.

## Sample de subtítulos

Sample opcional (Package Manager > HarryTools Audio > Samples > Subtitles) que añade subtítulos localizados a los sonidos, las voces y la música del catálogo. No pinta nada: lanza eventos y tu UI decide cómo mostrarlos. El núcleo no depende del sample ni de Unity Localization.

> **Beta:** el sample forma parte de la `1.0.0-beta.2` y su API puede cambiar antes de la 1.0.0.

### Instalación

1. Instala **Unity Localization** (`com.unity.localization`) **antes** de importar el sample; si no, el sample no compila.
2. Importa el sample desde el Package Manager.
3. La demo necesita unos ajustes de localización que un sample no puede traer. Una sola vez: **Edit > Project Settings > Localization > Create** para crear los *Localization Settings*, y en **Locale Generator** añade `English (en)` y `Spanish (es)`. Sin ellos la demo avisa una vez por consola y no muestra textos.
4. Abre `Demo/SubtitlesDemo.unity`. La demo lee el teclado con el **Input System**. Si tu escena ya tiene un `AudioListener`, desactívalo: la demo trae el suyo.

### Uso

1. Añade a un asset de audio la extensión **Subtitle Extension** (lista *Extensions*, es un `[SerializeReference]`). Los speakers llevan la **Speaker Subtitle Extension** con el nombre localizado.
2. Cada **entrada** tiene un texto localizado (`LocalizedString`), un tipo (**Dialogue**, texto hablado, o **Caption**, descripción de un sonido), un `Delay` desde que empieza el sonido y una `Duration` (0 dura hasta la siguiente entrada del mismo tipo o hasta el final del audio).
3. Una **excepción por clip** (`ClipOverrides`) sustituye a las entradas del asset cuando suena ese clip. Es útil con assets de varios clips (un grito, un paso) cuyo texto cambia según el clip.
4. **Distancia:** un sonido 3D solo muestra sus subtítulos si el oyente está dentro de su distancia de escucha (15 m por defecto). Se cambia por asset (*Override Hearing Distance*) o globalmente con un `SubtitleSettings` (`Subtitles.Settings`, que también lleva el margen de salida para que no parpadee en el borde).
5. Engánchate a los eventos:

```csharp
using HarryTools.Audio.Subtitles;

Subtitles.Started += _info => Mostrar(_info.Id, _info.SpeakerName, _info.Text, _info.Kind);
Subtitles.TextChanged += _info => Cambiar(_info.Id, _info.Text);   // cambio de idioma con la línea visible
Subtitles.Ended += _info => Quitar(_info.Id);
```

Cada `Started` recibe exactamente un `Ended` con el mismo `Id`. `SubtitleInfo` lleva `Text`, `Kind`, `Speaker`, `SpeakerName`, `Asset`, `Handle`, `Duration`, `Position` e `Is3D`.

**La UI debe fiarse de `Ended`, no de `Duration`.** `Duration` es el tiempo previsto (`-1` si dura hasta que un sonido en loop pare) y puede no cumplirse: el audio puede pararse antes, o la distancia o las opciones del jugador pueden ocultar la línea.

### Detalles a tener en cuenta

- El texto llega tal cual sale de la tabla: no se interpretan marcas. Un `[wait=1]` en la tabla llega literal al evento (la demo incluye uno).
- Dos entradas del mismo tipo con el mismo `Delay`: la anterior no llega a mostrarse. Entradas de tipos distintos pueden coincidir.
- Con *End On Stop* desactivado, al parar el audio las líneas siguen el tiempo que les quedaba. Ese tiempo corre en tiempo no escalado, también con la pausa global.
- Para el hablante solo se usa `VoiceAsset.Speaker`; el speaker de un `AudioEmitter` no cuenta.
- Con una distancia de escucha `<= 0`, un sonido 3D nunca muestra sus subtítulos.
- **Música:** una pista con la extensión muestra sus subtítulos siempre (sin filtro de distancia). No hay `AudioHandle` (`Handle` no es válido). Al pasar del intro al loop la línea de tiempo no se reinicia; al cambiar de pista (crossfade incluido) o pararla, las líneas de la anterior terminan.
- **Opciones del jugador:** `Subtitles.ShowDialogue` y `Subtitles.ShowCaptions` (activadas por defecto) ocultan cada tipo. Se guardan en `PlayerPrefs`; para guardarlas en otro sitio implementa `ISubtitleSettingsStore` (`Load` y `Save`) y pásalo a `Subtitles.SetStore`.
- Otra fuente de textos distinta de Unity Localization: implementa `ISubtitleTextSource` y pásala a `Subtitles.SetTextSource`.

### Demo

`SubtitlesDemo.unity` no tiene UI: `SubtitleConsoleLogger` escribe en la consola cada `Started`, `TextChanged` y `Ended`, por ejemplo `[Dialogue] Mara: Hey you! Over here. (2.2s) #2`. Mueve el oyente con WASD o las flechas (hacia la lluvia, a 20 m, para entrar en su distancia de 15 m) y usa las teclas:

| Tecla | Acción |
|---|---|
| 1 | El NPC dice un diálogo de dos entradas escalonadas |
| 2 | Sonido "attention call" con tres clips y excepciones por clip |
| 3 | Cambia de idioma (en/es) |
| 4 | Alterna `ShowDialogue` |
| 5 | Alterna `ShowCaptions` |

## Limitaciones

- El audio no se pausa solo con `Time.timeScale`; hay que llamar a `Audio.Pause()`.
- Las zonas de música no admiten `MeshCollider` cóncavos.
- Los puntos de loop necesitan un rango de al menos 0,5 s y la música no admite pitch.
- Una conversación necesita voces libres en el pool para las líneas en cola.
- Todavía no hay integración con Timeline, colisiones, partículas, sonidos de UI ni el puente con `signals`, ni registro en `serviceprovider`.

## Comprobaciones manuales

Hay cosas que los tests no pueden juzgar y conviene escuchar: una música con intro más loop (que el salto al loop no se note) y una con puntos de loop (que el corte caiga donde toca).

## Tests

Los tests EditMode y PlayMode están en `Tests/`. Para ejecutarlos, añade el paquete a `testables` en el manifest del proyecto.

## Créditos

- Inspirado en [JSAM](https://github.com/Jacky-Yang/JSAM) (MIT), de Jacky Yang.
