# Change Log:

## 1.2.5

    - Añadido README con la instalación, el uso y la configuración
    - Dependencia `com.harryb97.tools.domainreload` de 1.0.0 a 1.0.2: la 1.0.0 no está publicada y no se podía resolver desde OpenUPM
    - Dependencia `com.harryb97.harryutils` de 1.0.1 a 2.0.1: la 1.0.1 no está publicada y no se podía resolver desde OpenUPM

## 1.2.4

    - Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.localization` y el Package Manager avisa de las actualizaciones
    - `package.json`: añadido `repository` y el autor ya no incluye email
    - LICENSE: el titular pasa a ser HarryB97

## 1.2.3

    - El menú de creación ahora está en 'Assets > Create > HarryTools > Localization' en vez de 'Tools > Localization'

## 1.2.2

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.2.1

### Dependencias

    - Se ha cambiado la dependencia 'com.jsteinhauer.unitydomainreloadhelper' por 'com.harryb97.tools.domainreload'

## 1.2.0

    - Feat: Nueva sobrecarga del método 'SetText' que permite parámetros para los strings marcados como 'smart'

## 1.1.0

    - Feat: Se ha añadido un método estático que libera la información de los datos estáticos de las clases pertinentes

## 1.0.2

    - Fix: Las banderas ahora se recuperan correctamente

## 1.0.1

    - Los Assembly Definitions ahora usan el nombre en vez del GUID

    - Las banderas ahora se recuperan de los Addressables

## 1.0.0

    - Primera versión