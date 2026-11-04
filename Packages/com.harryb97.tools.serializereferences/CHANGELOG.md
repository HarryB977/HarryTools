# Change Log:

## 1.0.3

    - `displayName` de `Serialize References` a `HarryTools Serialize References`, para distinguirlo en la carpeta Packages y en el Package Manager. Los samples nuevos se importan en `Assets/Samples/HarryTools Serialize References/`

## 1.0.2

    - Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.serializereferences` y el Package Manager avisa de las actualizaciones
    - `package.json`: añadido `repository` y el autor ya no incluye email

## 1.0.1

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.0.0

    - Primera versión
    - Basado en 'com.mackysoft.serializereference-extensions' 1.7.0
    - Corrección automática de referencias compartidas al añadir elementos a un array
    - Copiado/pegado conservando referencias a objetos de Unity
    - Aviso y limpieza de tipos perdidos
    - Ventana 'SerializeReference Explorer'
