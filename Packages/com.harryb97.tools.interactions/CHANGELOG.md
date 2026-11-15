# Changelog

## [1.0.0-beta.1] - 2026-11-15
### Añadido
- `Interactor`: lleva el foco (`SetFocus`) y ejecuta interacciones por tipo (`Interact`), con los eventos `FocusChanged`, `FocusUpdated` e `Interacted`. Suelta el foco al deshabilitarse o cuando el foco deja de poder recibirlo.
- `Interactable`: lista de interacciones, estado activo, `CanBeFocused`, `AvailableInteractions`, `TryGetInteraction` y los eventos `FocusEntered`, `FocusExited`, `Interacted` y `Changed`, también como `UnityEvent`.
- `Interaction`: tipo, texto opcional para la UI, estado activo, *Ignore While Running* y respuestas. Texto y estado modificables en ejecución.
- `InteractionType`: asset que identifica un tipo de interacción.
- Textos: *Fixed Text* e `IInteractionText` para crear otros.
- Respuestas: *Unity Event* e `IInteractionResponse` para crear otras.
- Ensamblado opcional `HarryTools.Interactions.Actions` (con `com.harryb97.tools.actions`): respuesta *Action List* y acciones *Set Interactable Active*, *Set Interaction Active* y *Set Interaction Text*.
- Ensamblado opcional `HarryTools.Interactions.Localization` (con `com.unity.localization`): texto *Localized Text*.
