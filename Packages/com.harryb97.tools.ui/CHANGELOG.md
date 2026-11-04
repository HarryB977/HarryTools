# Changelog

## [1.0.3] - 2026-11-04
### Cambiado
- `displayName` de `UI` a `HarryTools UI`, para distinguirlo en la carpeta Packages y en el Package Manager. Los samples nuevos se importan en `Assets/Samples/HarryTools UI/`.

## [1.0.2] - 2026-11-04
### Cambiado
- Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.ui` y el Package Manager avisa de las actualizaciones
- `package.json`: añadido `repository` y el autor ya no incluye email
- LICENSE: el titular pasa a ser HarryB97
- Añadida la licencia SIL OFL 1.1 de la fuente Inter (`Samples~/HeroUI/Fonts/OFL.txt`)
- README: instalación desde OpenUPM

## [1.0.1] - 2026-11-03
### Cambiado
- Textos del inspector, tooltips, ventanas del editor, mensajes de consola y samples en inglés.
- README: cómo instalar PrimeTween desde el Asset Store (pulsar Install en `PrimeTweenInstaller.asset`) y que la versión de la dependencia es la mínima.

## [1.0.0] - 2026-11-03
### Añadido
- Primera versión, basada en PurrUI 1.6.1-beta.1 (MIT). Sustituye a los paquetes `popups`, `views`, `overlays` y `uicanvas`.
- Sistema de vistas por capas (Views, Popups y Overlays) con un único `UIRoot` y la fachada estática `UI`.
- Ciclo de vida por vista: `Cached`, `Preloaded` y `Transient`. Ocultar desactiva el `Canvas`, no el GameObject. `ShowAsync` y `PreloadAsync` usan `InstantiateAsync`.
- Popups con cola, varias instancias del mismo tipo, reserva para reutilizarlos y fondo oscurecido que los cierra.
- Overlays con un solo activo y `ShowWhile`, que oculta el overlay aunque la tarea falle o se cancele y le pasa el progreso (`IProgress<float>`).
- Transiciones con PrimeTween y UniTask, configurables desde el inspector: Fade, Slide, Scale y Parallel.
- Paleta: roles personalizados con id estable, tonos (OKLab), aviso de contraste WCAG y paleta por defecto.
- Iconos: sets de iconos como datos, varios sets (`<icon=set:nombre>`), importador (MDI `meta.json` y `codepoints` de Google) y buscador con previsualización. Material Design Icons actualizado a 7.4.47.
- RectangleGraphic: degradado en el contorno, sombra interior, desplazamiento de la sombra y soporte del softness de RectMask2D.
- Samples `HeroUI` y `Demo`.

### Eliminado respecto a PurrUI
- Sounds2D y los sonidos de los controles.
- `MonoView`, `ViewStack` y `ViewTransitions`, sustituidos por `View`, las capas y `IViewTransition`.
