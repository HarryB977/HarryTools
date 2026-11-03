# HarryTools
Herramientas que uso generalmente en mis proyectos de Unity
Cada paquete está en `Packages/<nombre-del-paquete>/` con su propio `package.json` y `CHANGELOG.md`.

## Instalar un paquete en un proyecto
Se añade al `Packages/manifest.json` del proyecto con la URL de git, la ruta del paquete y la etiqueta de su versión:

```json
"com.harryb97.tools.multiscene": "https://github.com/RazgriZ77/HarryTools.git?path=/Packages/com.harryb97.tools.multiscene#multiscene/1.2.1"
```

Unity no resuelve las dependencias de un paquete instalado por git: las dependencias `com.harryb97.*` que aparecen en el `package.json` del paquete también hay que añadirlas al manifest del proyecto.
Al ser un repositorio privado, Unity usa las credenciales de git del sistema (Git Credential Manager).

## Editar un paquete desde un proyecto
1. En el manifest del proyecto, apuntar el paquete a la copia local del repositorio:
   `"com.harryb97.tools.multiscene": "file:<ruta-local>/HarryTools/Packages/com.harryb97.tools.multiscene"`
2. Hacer los cambios, subir la versión en su `package.json` y anotarlos en su `CHANGELOG.md`.
3. Hacer commit, crear la etiqueta `<paquete>/<versión>` (por ejemplo `multiscene/1.2.2`) y subir ambos: `git push --follow-tags`.
4. Volver a poner la URL de git en el manifest con la nueva etiqueta.

## Etiquetas
Cada versión de cada paquete tiene una etiqueta `<paquete>/<versión>`, donde `<paquete>` es el nombre sin `com.harryb97.tools.`. Una etiqueta publicada no se mueve: si hay que corregir algo, se sube la versión.

## GUIDs
Cada `.meta` debe tener un GUID único. Al crear un paquete nuevo copiando otro, hay que regenerar los GUIDs de los `.meta` copiados; si no, Unity da errores de conflicto de GUID al instalar ambos paquetes.
