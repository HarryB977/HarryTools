# Change Log:

## 1.2.1

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.2.0

    - Feat: 'SceneReference' propio, sin dependencias externas. Guarda el GUID de la escena y sincroniza en el Editor su ruta y su dirección de Addressables
    - Feat: Las escenas que no están en la build ni son Addressables se omiten con un aviso en vez de fallar
    - Fix: Las escenas Addressables se cargan por su dirección y no por su ruta

### Dependencias

    - Eliminadas 'com.eflatun.scenereference', 'com.mackysoft.serializereference-extensions' y 'com.jsteinhauer.unitydomainreloadhelper'
    - Añadida 'com.harryb97.tools.serializereferences'

## 1.1.1

    - Los Assembly Definitions ahora usan el nombre en vez del GUID
    
## 1.1.0

    - Se ha añadido la posibilidad de cargar y descargar escenas marcadas como Addressables

### Dependencias

    - Añadida dependencia 'com.unity.addressables'
    - Se ha cambiado la dependencia 'devlocker.utils.scenereference' por 'com.eflatun.scenereference'

## 1.0.0

    - Primera versión