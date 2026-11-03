# Changelog

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
