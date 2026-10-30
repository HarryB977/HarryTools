# Change Log:

## 1.1.1

    - Fix: Los archivos .meta tenían GUIDs repetidos con los de otros paquetes, lo que provocaba errores de conflicto de GUID al tener ambos paquetes instalados

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.1.0

    - Se ha añadido un check en Popup para saber si respeta o no la cola (para que un Popup pueda aparecer sobre otro)

## 1.0.2

    - Se ha añadido un método en PopupManager para ocultar todos los popups que están en la cola

## 1.0.1

    - Los Assembly Definitions ahora usan el nombre en vez del GUID

## 1.0.0

    - Primera versión