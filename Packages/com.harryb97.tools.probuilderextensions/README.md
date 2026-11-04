# ProBuilder Extensions

Herramientas que amplían ProBuilder. Depende de `com.unity.probuilder` 6. El generador de texturas necesita URP.

## Hotspot UV

Asigna a cada cara seleccionada el rectángulo (*patch*) de un atlas *hotspot* que mejor encaja con su proporción y su tamaño en el mundo.

1. Crear un **Hotspot Texture**: `Create > HarryTools > ProBuilder Extensions > Hotspot Texture`. Hay dos formas de definir sus patches:
   - Asignarle un **Hotspot Layout** (recomendado; es lo que hace el generador).
   - Sin layout: asignarle la textura del atlas y dibujar los rectángulos en píxeles.
2. Abrirlo con doble clic (o `Tools > HarryTools > ProBuilder Extensions > Hotspot Rect Creator`) para dibujar o editar los patches.
   - Clic izquierdo y arrastrar: crear · Clic izquierdo: seleccionar · Clic derecho: borrar · Botón central: desplazar · Rueda: zoom
3. Asignar a los objetos un material con esa misma textura.
4. En ProBuilder, seleccionar caras y usar **Hotspot Selection** (en la barra de ProBuilder, el menú contextual de la escena o `Tools > HarryTools > ProBuilder Extensions > Hotspot Selection`).

### Opciones de Hotspot Selection

- **Allow Rotation**: permite girar la cara 90° si así encaja mejor. Cada patch puede prohibirlo.
- **Allow Mirror**: permite voltear al azar los patches que lo admiten.
- **Scale Tolerance**: cuánto puede diferir el tamaño del patch del de la cara (0,25 = ±25%). Solo se aplica a los Hotspot Texture con layout. Si ningún patch entra, se usa una franja de tiling para caras largas o, si tampoco hay, el más parecido (con un aviso en la consola).
- **Preserve Aspect**: encaja la cara dentro del rectángulo sin deformarla, en vez de estirarla.
- **Random Tolerance**: cuánto peor que el mejor patch puede ser otro para entrar en el sorteo. Volver a aplicar elige otro al azar.

### Hotspot Layout

Conjunto de patches independiente de la resolución (`Create > HarryTools > ProBuilder Extensions > Hotspot Layout`). Se usa tanto para generar el atlas como para mapear, así que siempre coinciden.

- **World Width**: metros que cubre el atlas a lo ancho. Fija la densidad: ancho de la textura / World Width.
- **Opciones por patch** (en el Rect Creator, con el patch seleccionado):
  - **Allow Rotation**: desactivar en paredes o en lo que tenga que quedar derecho.
  - **Allow Mirror X / Y**: volteo aleatorio para que caras iguales no se vean idénticas.
  - **Allow Tiling**: el patch se repite a lo largo en vez de estirarse. Solo en patches que ocupan todo el ancho o todo el alto.
  - **Inset**: píxeles que se recortan por cada lado al mapear, para evitar que se cuele el vecino con los mipmaps.

### Layouts de serie

En `Packages/ProBuilder Extensions/Layouts`. Todos son de 8×8 m (512 px/m a 4096 px), pensados para interiores:

| Layout | Contenido |
|---|---|
| **Walls** | Paredes de 2,6 m de alto y de 0,37 a 3,25 m de ancho, medias paredes de 1,2 m y una franja de tiling para paredes de más de 4 m. Sin rotación. |
| **Floors** | Tablones (franjas de tiling) y baldosas o losas de 0,3 a 2,45 m. |
| **Trims** | Zócalos y molduras de 6 a 30 cm (franjas de tiling), arrimaderos y piezas pequeñas. |
| **Frames** | Jambas de puertas y ventanas, dinteles, alféizares y paneles. Sin rotación. |
| **Generic** | Cuadrados y rectángulos de 0,12 a 4,2 m para todo lo demás. |

## Generador de texturas hotspot

Crea un atlas hotspot a partir de dos texturas tileables y un layout: una **capa superior** (pintura, yeso, barniz…) y una **capa inferior** que asoma en las juntas y en los desconchones (ladrillo, madera, mortero…). Cada patch recibe un bisel en sus bordes, y el desgaste se calcula por código, sin máscaras pintadas.

1. Crear una **Hotspot Recipe**: `Create > HarryTools > ProBuilder Extensions > Hotspot Recipe`.
2. Abrirla con doble clic (o `Tools > HarryTools > ProBuilder Extensions > Hotspot Texture Generator`). A la derecha se ve una vista previa de cada mapa.
3. Elegir un layout, las dos capas y el desgaste, y pulsar **Generate**.

### Receta

- **Layout**: los patches. Todas las recetas con el mismo layout producen los mismos rectángulos, así que una habitación mapeada una vez puede pasar de cuidada a vieja cambiando solo el material.
- **Top / Bottom**: Albedo (obligatorio, tileable), Normal y Metallic/Smoothness (opcionales; R = metálico, A = suavidad), tinte y:
  - **Tile Size**: metros reales que cubre una repetición de la textura (por ejemplo, 1 si la textura muestra 1 m de pared de ladrillo). Así los ladrillos miden lo mismo en todos los patches.
  - **Align To Patches**: la capa empieza en la esquina inferior izquierda de cada patch, sin desplazamiento ni giro. Útil para ladrillos, tablones o azulejos: cada patch arranca con una hilada entera. La textura tiene que empezar en una junta.
- **Wear**:
  - **Style**: el tipo de daño que se añade al borde.
    - **Cracks** (por defecto): grietas finas que salen del canto y se ramifican hacia dentro. Son surcos en la propia capa superior y no dejan ver la inferior. Para azulejos, yeso o esmalte.
    - **Bites**: mordiscos redondeados que dejan ver la capa inferior. Para metal o pintura saltada.
    - **None**: solo el borde dañado (Edge Width) y la textura de desgaste, si hay.
  - **Intensity**: cuánto daño añaden el estilo y la textura. 0 = solo el borde dañado; 1 = grietas largas y numerosas o mordiscos profundos. No cambia Edge Width.
  - **Bevel Width**: ancho del bisel redondeado en metros.
  - **Edge Width**: ancho mínimo, en metros, de la franja dañada que recorre todo el borde de cada patch y deja ver la capa inferior. En ningún punto del borde es más estrecha, así que no quedan tramos sin daño. 0 = bisel limpio.
  - **Wear Size**: tamaño del patrón en metros. En Cracks, la separación típica entre grietas (y su longitud con Intensity 1); en Bites, la longitud de cada mordisco.
  - **Wear Texture** (opcional): una imagen de "edge wear" o marco de grunge, blanca por dentro y con el borde negro y desgarrado. Solo se lee una franja de su lado izquierdo, que se repite a lo largo del borde de cada patch sin deformarse. Las motas sueltas se eliminan. Se combina con Style; para usar solo la textura, elige None.
    - **Texture Border**: qué fracción del ancho de la imagen se lee como franja.
    - **Texture Depth**: hasta dónde entra la textura en el patch (metros) con Intensity 1.
  - **Cavity**: cuánto se oscurecen juntas, grietas, zonas gastadas y el pie del bisel. Hace que los bordes se lean aunque las dos capas sean el mismo material.
  - **Edge Tone**: cuánto se aclara el borde gastado de la capa superior.
  - **Gap Depth**: profundidad de juntas y zonas gastadas respecto al bisel.
- **Resolution**: ancho de las texturas (el alto sigue la proporción del layout).
- **World Width Override**: cambia los metros que cubre el atlas sin tocar el layout.
- **Variation**: cuánto cambia la capa de un patch a otro. 0 = la textura sigue continua por todo el atlas; 1 = cada patch toma un trozo distinto (y gira 90° si el patch lo permite). Bájalo si los patches se ven de colores muy distintos.
- **Seed**: cambia el patrón de desgaste y la variación de cada patch.

### Salidas

En una carpeta junto a la receta, con su nombre:

- `<Receta>_BaseColor.png`, `<Receta>_Normal.png`, `<Receta>_Mask.png` (Metallic/Smoothness de URP) y `<Receta>_Occlusion.png`.
- `<Receta>.mat`: material URP Lit con los cuatro mapas.
- `<Receta> Hotspot.asset`: Hotspot Texture listo para Hotspot Selection.

Volver a generar sobrescribe los mismos archivos y conserva sus GUIDs, así que materiales y escenas siguen apuntando a ellos aunque se hayan movido de carpeta.

Basado en [Hotspot UV](https://github.com/BennyKok/unity-hotspot-uv) de BennyKok (MIT) e inspirado por el *Hotspot Texture Maker* de Scythe Editor. Más sobre hotspotting: [DreamUV](https://github.com/leukbaars/DreamUV#hotspotting).
