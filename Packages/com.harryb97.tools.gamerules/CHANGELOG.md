# Change Log:

## 1.0.5

    - `displayName` de `GameRules` a `HarryTools GameRules`, para distinguirlo en la carpeta Packages y en el Package Manager. Los samples nuevos se importan en `Assets/Samples/HarryTools GameRules/`

## 1.0.4

    - Añadido README con la instalación, el uso y la configuración
    - Dependencia `com.harryb97.harryutils` de 1.0.1 a 2.0.1: la 1.0.1 no está publicada y no se podía resolver desde OpenUPM

## 1.0.3

    - Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.gamerules` y el Package Manager avisa de las actualizaciones
    - `package.json`: añadido `repository` y el autor ya no incluye email
    - LICENSE: el titular pasa a ser HarryB97

## 1.0.2

    - Fix: Los archivos .meta tenían GUIDs repetidos con los de otros paquetes, lo que provocaba errores de conflicto de GUID al tener ambos paquetes instalados

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.0.1

    - Los Assembly Definitions ahora usan el nombre en vez del GUID

## 1.0.0

    - Primera versión