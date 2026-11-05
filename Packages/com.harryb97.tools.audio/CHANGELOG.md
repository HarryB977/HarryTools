# Changelog

## [1.0.0] - 2026-11-05
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
