# HarryTools
Herramientas que uso generalmente en mis proyectos de Unity.

Cada paquete está en `Packages/<nombre-del-paquete>/` con su propio `package.json` y `CHANGELOG.md`.

## Instalar un paquete en un proyecto
Los paquetes están publicados en [OpenUPM](https://openupm.com/packages/?q=com.harryb97). Con la [CLI de OpenUPM](https://openupm.com/docs/getting-started-cli.html), desde la carpeta del proyecto:

```sh
openupm add com.harryb97.tools.multiscene
```

La CLI añade el registro de OpenUPM al `Packages/manifest.json` con los scopes necesarios, y Unity instala las dependencias que estén en OpenUPM o en el registro de Unity. Las actualizaciones aparecen en el Package Manager (**My Registries**) con el botón **Update**.

También se puede instalar por URL de git, con la ruta del paquete y la etiqueta de su versión:

```json
"com.harryb97.tools.multiscene": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.multiscene#multiscene/1.2.3"
```

Así Unity no resuelve las dependencias: las `com.harryb97.*` del `package.json` del paquete también hay que añadirlas al manifest.

## Editar un paquete desde un proyecto
1. En el manifest del proyecto, apuntar el paquete a la copia local del repositorio:
   `"com.harryb97.tools.multiscene": "file:<ruta-local>/HarryTools/Packages/com.harryb97.tools.multiscene"`
2. Hacer los cambios, subir la versión en su `package.json` y anotarlos en su `CHANGELOG.md`.
3. Hacer commit, crear la etiqueta `<paquete>/<versión>` (por ejemplo `multiscene/1.2.3`) y subir ambos: `git push --follow-tags`.
4. Volver a poner en el manifest la versión del registro. OpenUPM publica la nueva versión a partir de la etiqueta en unos 15-30 minutos.

## Etiquetas
Cada versión de cada paquete tiene una etiqueta `<paquete>/<versión>`, donde `<paquete>` es el nombre sin `com.harryb97.tools.`. OpenUPM la detecta con el `gitTagPrefix` `<paquete>/`, y la versión de la etiqueta tiene que coincidir con la del `package.json`. Una etiqueta publicada no se mueve: si hay que corregir algo, se sube la versión.

## GUIDs
Cada `.meta` debe tener un GUID único. Al crear un paquete nuevo copiando otro, hay que regenerar los GUIDs de los `.meta` copiados; si no, Unity da errores de conflicto de GUID al instalar ambos paquetes.
