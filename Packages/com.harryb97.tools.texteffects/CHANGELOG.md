# Changelog

## [1.0.0-beta.1] - 2026-11-16
### Añadido
- Componente `EffectText` para `TMP_Text` (UGUI y 3D): typewriter con velocidad base, tiempo escalado o sin escalar, mostrar al activarse, `SetText`, `Show`, `Skip`, `Continue`, `Hide` y `HideImmediate`, estado consultable y los eventos `CharacterShown`, `EventReached`, `WaitingInput`, `TypingCompleted` y `Hidden`.
- Marcado dentro del texto: `<pause>`, `<speed>`, `<waitinput>`, `<event>`, `<voice>`, `<instant>` y marcas de presets con parámetros opcionales. Cierre con `</>` o `</nombre>`, marcas sin cerrar hasta el final del texto, `<noparse>` y respeto de las marcas de TextMeshPro.
- Efectos de aparición, continuos, de un golpe y de desaparición como `TextEffectPreset`, con 10 presets incluidos (`fade`, `scale`, `slide`, `fadeout`, `shake`, `wave`, `pulse`, `colorpulse`, `shakeonce`, `punch`) y efectos propios heredando de `TextEffect`. Desaparición letra a letra (en ambos sentidos) o todo a la vez.
- `TypingVoice`: voz de tecleo con frecuencia, filtro de espacios y puntuación, variación de tono y sonido del catálogo del paquete `audio` (opcional).
- Tabla de eventos con `ActionList` por evento, opción de ejecutar al saltar y cola secuencial de acciones. `ITextEventProvider` para sustituir la tabla por código.
- Acciones **Show Text**, **Hide Text**, **Skip Text** y **Continue Text** para `actions`.
- Texto localizado en el propio componente con `LocalizedString` (opcional, `com.unity.localization`), con reglas de cambio de idioma que no repiten los eventos ya disparados.
- `TextEffectsSettings`: registro global de presets y voces, efectos por defecto, tiempo y velocidad por defecto, con página en **Project Settings > HarryTools > Text Effects** que crea el asset y mantiene el registro al importar presets y voces.
- Validación del marcado en el inspector y entre idiomas, y menú **Tools > HarryTools > Text Effects > Validate String Tables**.
- Vista previa en edit mode desde el inspector, sin sonido y sin ejecutar las acciones de los eventos.
- Cero allocations por frame mientras el texto se escribe, se anima o se oculta.
