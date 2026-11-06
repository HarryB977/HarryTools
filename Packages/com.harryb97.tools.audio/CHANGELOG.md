# Changelog

## [1.0.0-beta.2] - 2026-11-06
### Añadido
- `AudioHandle` expone `Clip`, `Duration`, `IsLooping`, `Position`, `Is3D` y `Elapsed` (tiempo de audio sonado, sin el `Delay` ni el tiempo en pausa).
- `AudioHandle.InDelay`: `true` mientras no ha pasado el `Delay` del asset (la reproducción existe pero aún no se oye).
- `MusicChannel.Elapsed`: segundos sonados de la pista actual, sin reinicio en el paso de intro a loop y congelados con la pausa.
- Sample de subtítulos (`Samples~/Subtitles`, requiere Unity Localization): extensión de subtítulos para los assets, eventos `Started`, `TextChanged` y `Ended`, distancia de escucha, opciones del jugador y escena de demo con un logger de consola.

### Cambiado
- `MusicChannel.Stop` sin fundido ahora lanza `Changed(null)`.

## [1.0.0-beta.1] - 2026-11-05
### Añadido
- Assets de audio: sonidos, música, voces, conversaciones, speakers y perfiles espaciales, con IDs estables y catálogo mantenido por el editor.
- Clase de ids generada (`Sounds`, `Music`, `Voices`, `Conversations`) y referencias serializables (`SoundRef`, `MusicRef`, `VoiceRef`, `ConversationRef`).
- Reproducción por bus con pooling, `AudioHandle` con generación, límite de instancias, cooldown, prioridades y selección de clips, sin GC en el camino normal.
- Música con crossfade, intro más loop, puntos de loop y `MusicZone`.
- Voces y conversaciones, `AudioEmitter` con política de cola e interrupción, y presets de efecto (paso bajo/alto, distorsión, eco).
- Buses con volumen, mute y ducking, pausa explícita y persistencia en `PlayerPrefs` o en un `IAudioSettingsStore` propio.
- Módulos acústicos enchufables (`IAcousticModule`) y soporte de WebGL sin mixer.
- `SoundPlayer` y nueve acciones para el paquete `actions`.
- Project Settings (`Project/HarryTools/Audio`), selector con búsqueda y previsualización en el inspector, y menú **Tools > HarryTools > Audio**.
