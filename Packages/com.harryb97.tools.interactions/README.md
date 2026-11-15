# Interactions

Base genérica de interacciones para Unity, independiente del controlador:

- **Interactuador** (`Interactor`): quien interactúa, por ejemplo el jugador. Lleva el foco y ejecuta interacciones por tipo.
- **Interactuado** (`Interactable`): el objeto con el que se interactúa, por ejemplo el pomo de una puerta. Tiene una lista de interacciones.
- **Interacción** (`Interaction`): una forma de interactuar con ese objeto. Tiene un tipo (primaria, secundaria…), un texto para la UI ("Abrir") y lo que ocurre al ejecutarla.

El paquete **no detecta** nada: no lanza raycasts ni usa triggers. Cada controlador detecta a su manera y se lo comunica al `Interactor` con `SetFocus` e `Interact`. El sample *Interactions* de `com.harryb97.tools.fps3dcontroller` lo hace con un raycast desde la cámara.

Lo visual (outline, mensajes en pantalla, iconos de botón) tampoco es cosa del paquete: los eventos de foco e interacción avisan para que cada juego lo pinte como quiera.

> **Beta:** la API aún puede cambiar antes de la 1.0.0.

## Instalación

Instala el paquete desde [OpenUPM](https://openupm.com/packages/com.harryb97.tools.interactions/):

```sh
openupm add com.harryb97.tools.interactions@1.0.0-beta.1
```

Por URL de git hay que añadir también su dependencia, porque Unity no la resuelve en ese caso:

```json
"com.harryb97.tools.interactions": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.interactions#interactions/1.0.0-beta.1",
"com.harryb97.tools.serializereferences": "https://github.com/HarryB977/HarryTools.git?path=/Packages/com.harryb97.tools.serializereferences#serializereferences/1.0.3"
```

## Montaje sin código

1. **Tipos de interacción.** *Create > HarryTools > Interactions > Interaction Type*. Crea uno por cada forma de interactuar que tenga tu juego, por ejemplo `Primary` y `Secondary`. El paquete no decide cuántos hay ni a qué botón van: eso lo hace quien detecta.
2. **Interactuado.** En el objeto (con su collider), *Add Component > HarryTools > Interactions > Interactable*. Añade una interacción por tipo:
   - **Type**: el asset del tipo.
   - **Text**: *Fixed Text* o *Localized Text* (ver más abajo). Es opcional.
   - **Active**: si se puede ejecutar.
   - **Ignore While Running**: activado por defecto. Mientras una respuesta sigue en marcha (por ejemplo, la animación de la puerta), los nuevos intentos se ignoran.
   - **Responses**: lo que ocurre. *Unity Event* viene en el paquete; *Action List* aparece si el proyecto tiene `com.harryb97.tools.actions`.
3. **Interactuador.** En el jugador, *Add Component > HarryTools > Interactions > Interactor*, y el componente que detecta (por ejemplo, el del sample de `fps3dcontroller`).

Si dos partes del mismo objeto se usan por separado (un pomo y un pestillo), cada una es un `Interactable` con su collider. Si son varias acciones sobre la misma parte (abrir y bloquear el pomo), van en el mismo `Interactable`, cada una con su tipo.

## Uso desde código

```csharp
using HarryTools.Interactions;

// Quien detecta (raycast, trigger…):
interactor.SetFocus(_hitInteractable);      // null para soltar el foco
if (_primaryPressed) interactor.Interact(primaryType);

// La UI del juego:
interactor.FocusChanged += (_previous, _current) => { /* mostrar u ocultar el mensaje y el outline */ };
interactor.FocusUpdated += _interactable => { /* el texto o las interacciones del foco cambiaron */ };
foreach (Interaction _interaction in interactor.Focus.AvailableInteractions) {
    Debug.Log($"{_interaction.Type.name}: {_interaction.Text}");
}

// En ejecución:
_interactable.SetActive(false);
if (_interactable.TryGetInteraction(secondaryType, out Interaction _lock)) _lock.SetText("Unlock");
```

| Miembro | Qué hace |
|---|---|
| `Interactor.SetFocus(Interactable)` | Cambia el foco. Un objeto que no puede recibir foco cuenta como `null` |
| `Interactor.Interact(InteractionType)` | Ejecuta sobre el foco la primera interacción activa de ese tipo. Devuelve `false` si no se ejecutó nada |
| `Interactor.Focus` | El foco actual, o `null` |
| `Interactor.FocusChanged` | `(anterior, actual)`, cualquiera de los dos puede ser `null`. También hay un `UnityEvent<Interactable>` |
| `Interactor.FocusUpdated` | El foco no cambió, pero sí su texto o sus interacciones |
| `Interactor.Interacted` | `(Interactable, Interaction)` tras ejecutar |
| `Interactable.FocusEntered` / `FocusExited` / `Interacted` | Lo mismo visto desde el objeto. También como `UnityEvent` en el inspector |
| `Interactable.Changed` | Cambió su estado activo, su habilitación o alguna interacción |
| `Interactable.CanBeFocused` | Activo, habilitado y con al menos una interacción activa |
| `Interactable.AvailableInteractions` | Interacciones activas, en orden |
| `Interactable.TryGetInteraction(InteractionType, out Interaction)` | Primera interacción activa de ese tipo |
| `Interactable.SetActive(bool)`, `Interaction.SetActive(bool)` | Activar o desactivar |
| `Interaction.SetText(string)` / `SetText(IInteractionText)` | Cambiar el texto. Con `string` se usa un texto fijo |
| `Interaction.Text`, `Type`, `IsActive`, `IsRunning` | Estado de la interacción |

Para crear tus propios textos o respuestas, implementa `IInteractionText` o `IInteractionResponse` en una clase `[Serializable]` con `[TypeMenu("...")]`, y aparecerán en el desplegable del inspector.

## Reglas del foco

- Al cambiar de foco se avisa primero al anterior (`FocusExited`) y después al nuevo (`FocusEntered`).
- Llamar a `SetFocus` con el foco actual no hace nada.
- Un `Interactable` inactivo, deshabilitado o sin interacciones activas no puede recibir foco. Si lo pierde mientras está en foco (se desactiva, se destruye o se queda sin interacciones activas), el `Interactor` lo suelta.
- Al deshabilitar el `Interactor`, suelta el foco.
- `Interact` sin foco, con tipo `null` o con un tipo que el foco no tiene devuelve `false`, sin errores ni eventos.
- Si hay varias interacciones del mismo tipo, se usa la primera activa.

## Ensamblados opcionales

Se compilan solo si el proyecto tiene el paquete correspondiente. Sin él, no aparecen y no dan errores.

| Ensamblado | Requiere | Incluye |
|---|---|---|
| `HarryTools.Interactions.Actions` | `com.harryb97.tools.actions` 2.0.0-beta.1 o superior | Respuesta **Action List**, que ejecuta un `ActionList` con `Self` = el Interactable y `Target` = el GameObject del Interactor. Acciones **Interactions/Set Interactable Active**, **Set Interaction Active** y **Set Interaction Text**; por defecto apuntan a `Self` |
| `HarryTools.Interactions.Localization` | `com.unity.localization` | Texto **Localized Text** (`LocalizedString`). Avisa con `FocusUpdated` al cambiar de idioma |

Con Actions y Conditions, la lógica condicional va dentro de la lista. Por ejemplo, una puerta que forcejea si está bloqueada: *If* (condición del juego) → animación de forcejeo; si no → animación de abrir.

## Limitaciones

- Sin interacciones de mantener pulsado ni de mirar durante un tiempo.
- Un `Interactor` no bloquea el objeto: si dos enfocan el mismo, cada uno recibe sus eventos.
- `Localized Text` obtiene el texto de forma síncrona (`GetLocalizedString`).
- Sin guardado de estado.
