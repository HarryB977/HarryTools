# ProBuilder Extensions

Herramientas que amplían ProBuilder. Depende de `com.unity.probuilder` 6.

## Hotspot UV

Asigna a cada cara seleccionada el rectángulo de un atlas *hotspot* que mejor encaja con su proporción y su tamaño en el mundo.

1. Crear un **Hotspot Texture**: `Create > HarryTools > ProBuilder Extensions > Hotspot Texture`, y asignarle la textura del atlas.
2. Abrirlo con doble clic (o `Tools > HarryTools > ProBuilder Extensions > Hotspot Rect Creator`) y dibujar los rectángulos.
   - Clic izquierdo y arrastrar: crear · Clic derecho: borrar · Botón central: desplazar · Rueda: zoom
3. Asignar a los objetos un material con esa misma textura.
4. En ProBuilder, seleccionar caras y usar **Hotspot Selection** (en la barra de ProBuilder, el menú contextual de la escena o `Tools > HarryTools > ProBuilder Extensions > Hotspot Selection`). Sus opciones permiten elegir el Hotspot Texture, rotar y voltear UVs.

### Opciones

- **Texels Per Unit** (en el Hotspot Texture): densidad de píxeles por unidad del mundo con la que se compara el tamaño de la cara. Con `0` solo se compara la proporción.
- **Allow Rotation**: permite girar la cara 90° si así encaja mejor.
- **Preserve Aspect**: encaja la cara dentro del rectángulo sin deformarla, en vez de estirarla.
- **Random Tolerance**: cuánto peor que el mejor rectángulo puede ser otro para entrar en el sorteo. Con `0` siempre se usa el mejor. Volver a aplicar elige otro al azar.

Basado en [Hotspot UV](https://github.com/BennyKok/unity-hotspot-uv) de BennyKok (MIT). Más sobre hotspotting: [DreamUV](https://github.com/leukbaars/DreamUV#hotspotting).
