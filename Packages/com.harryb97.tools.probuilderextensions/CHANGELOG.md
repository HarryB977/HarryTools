# Change Log:

## 1.1.0

    - Nuevo generador de texturas hotspot (Tools > HarryTools > ProBuilder Extensions > Hotspot Texture Generator): a partir de una capa superior, una inferior y un layout genera Base Color, Normal, máscara Metallic/Smoothness y Occlusion para URP Lit, un material y un Hotspot Texture
    - Bisel y desgaste procedurales con un control de intensidad: desde un bisel suave con un leve cambio de tono hasta desconchones que dejan ver la capa inferior
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
