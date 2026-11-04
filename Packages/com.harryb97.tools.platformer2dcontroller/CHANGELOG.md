# Change Log:

## 1.0.4

    - `displayName` de `Platformer 2D Controller` a `HarryTools Platformer 2D Controller`, para distinguirlo en la carpeta Packages y en el Package Manager. Los samples nuevos se importan en `Assets/Samples/HarryTools Platformer 2D Controller/`

## 1.0.3

    - Añadido README con la instalación, el uso y la configuración
    - Dependencia `com.harryb97.harryutils` de 1.1.2 a 2.0.1: la 1.1.2 no está publicada y no se podía resolver desde OpenUPM

## 1.0.2

    - Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.platformer2dcontroller` y el Package Manager avisa de las actualizaciones
    - `package.json`: añadido `repository` y el autor ya no incluye email
    - LICENSE: el titular pasa a ser HarryB97

## 1.0.1

    - Fix: Los archivos .meta tenían GUIDs repetidos con los de otros paquetes, lo que provocaba errores de conflicto de GUID al tener ambos paquetes instalados

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.0.0

    - Primera versión