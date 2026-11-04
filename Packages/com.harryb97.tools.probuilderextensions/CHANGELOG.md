# Change Log:

## 1.2.0

    - Generador hotspot: nuevo Edge Width, una franja dañada que recorre todo el borde de cada patch con un ancho mínimo garantizado; ya no quedan tramos del borde sin daño
    - Nuevo Style de desgaste: Cracks (grietas finas que salen del canto, por defecto), Bites (los mordiscos de antes) o None
    - Nueva Wear Texture opcional: una imagen de grunge cuyo borde se repite a lo largo de cada patch sin deformarse, con Texture Border y Texture Depth; las motas sueltas se eliminan
    - Chip Size y Chip Density se sustituyen por Wear Size. Las recetas anteriores se abren como Bites sin franja y con el mismo aspecto (su Intensity puede cambiar de valor)
    - Nuevo mapa Cracks en la vista previa y en `HotspotMap`

## 1.1.1

    - Corregido: al generar, los mapas Normal, Mask y Occlusion se exportaban vacíos (la importación del primer PNG invalidaba el render de los siguientes). Ahora se renderizan todos antes de escribir ningún archivo

## 1.1.0

    - Nuevo generador de texturas hotspot (Tools > HarryTools > ProBuilder Extensions > Hotspot Texture Generator): a partir de una capa superior, una inferior y un layout genera Base Color, Normal, máscara Metallic/Smoothness y Occlusion para URP Lit, un material y un Hotspot Texture
    - Bisel y desgaste procedurales: el daño crece desde los bordes con un contorno irregular y su ancho se controla con Intensity; Cavity oscurece juntas y desconchones
    - Cada capa se escala con Tile Size (metros reales que cubre la textura) y puede alinearse a la esquina de cada patch (Align To Patches) para que ladrillos y tablones empiecen enteros
    - Variation controla cuánto cambia la capa de un patch a otro
    - Nuevo asset Hotspot Layout con opciones por patch: Allow Rotation, Allow Mirror X/Y, Allow Tiling e Inset
    - Cinco layouts de serie para interiores (Walls, Floors, Trims, Frames, Generic) a 512 px/m
    - Hotspot UV: tolerancia de escala para los Hotspot Texture con layout (por defecto ±25%), franjas de tiling para caras largas, espejo aleatorio, inset y aviso cuando una cara no encaja en ningún patch
    - El Rect Creator edita también Hotspot Layouts y las opciones de cada patch
    - Los Hotspot Texture existentes siguen funcionando sin cambios
    - `HotspotTexture.FindBestRect` pasa a ser `FindBestPatch` y cambia la firma de `HotspotAction.HotspotFace`
    - Una receta duplicada genera sus propias salidas en vez de sobrescribir las de la original

## 1.0.0

    - Primera versión
    - Incluye Hotspot UV, basado en el de BennyKok con soporte para ProBuilder 6, elección del rectángulo por proporción y tamaño, aleatoriedad entre los mejores candidatos, rotar y voltear UVs
