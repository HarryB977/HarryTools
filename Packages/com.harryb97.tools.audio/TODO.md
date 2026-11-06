# Pendiente para la 1.0.0

El paquete está en beta (`1.0.0-beta.2`). Antes de publicar la 1.0.0 hay que cerrar lo siguiente.

## Comprobaciones a mano

- [ ] **Música sin clics.** Reproducir una pista con intro más loop y otra con puntos de loop, y escuchar el salto. Los tests miden 0 samples de desfase, pero hay que oírlo.
- [ ] **Volumen guardado.** Poner el bus Music a 0,2, salir del modo Play, volver a entrar y mirar en el mixer que `MusicVolume` sigue en el valor correspondiente.
- [ ] **Selector del inspector.** Abrir el buscador de un `SoundRef`, filtrar escribiendo, previsualizar con ▶ y comprobar que una referencia a un asset borrado aparece como "Missing".
- [ ] **Demo de subtítulos.** Importar el sample en un proyecto con Unity Localization e Input System, crear los Localization Settings con `en` y `es`, abrir `SubtitlesDemo.unity` y probar las teclas 1 a 5 y WASD: líneas en consola con su speaker, cambio de idioma con una línea visible, opciones de diálogo y captions, y la distancia de escucha al acercar el oyente a los sonidos 3D. Comprobar también que la lista *Extensions* de un asset deja añadir la **Subtitle Extension** desde el selector de tipos.
- [ ] **WebGL.** Cambiar la plataforma a WebGL y comprobar que compila y suena sin mixer (sin efectos ni parámetros expuestos).

## Samples

- [x] **Subtítulos.** Sample `Samples~/Subtitles` (`1.0.0-beta.2`): lee las líneas de las tablas de Unity Localization y solo emite eventos (inicio y fin de línea, texto, speaker); la UI la pone cada juego.
- [ ] **Acústica.** Sample opcional de módulos `IAcousticModule` basados en la geometría: oclusión y atenuación por paredes, eco según el tamaño de la sala o zona abierta, y portales opcionales (puertas) por donde sale el sonido. Límite de unas 10 fuentes simuladas a la vez.

## Antes de publicar

- [ ] Subir la versión a `1.0.0` en `package.json`, CHANGELOG y README (URL de instalación y comando de OpenUPM), y quitar el aviso de beta.
- [ ] Borrar este archivo.
