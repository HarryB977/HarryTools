# UI

Sistema de interfaces para uGUI basado en [PurrUI](https://github.com/PurrNet/PurrUI):

- Vistas por capas (Views, Popups y Overlays).
- Paleta de colores con temas.
- Iconos dentro de textos TMP.
- Gráficos procedurales: rectángulos redondeados con contorno, sombras y degradados.

Sustituye a los antiguos paquetes `popups`, `views`, `overlays` y `uicanvas`.

## Instalación

Añade al `Packages/manifest.json` del proyecto el paquete y sus dependencias. Unity no resuelve las dependencias de un paquete instalado por git, así que hay que añadirlas también.

```json
"com.harryb97.tools.ui": "https://github.com/RazgriZ77/HarryTools.git?path=/Packages/com.harryb97.tools.ui#ui/1.0.0",
"com.harryb97.tools.logger": "https://github.com/RazgriZ77/HarryTools.git?path=/Packages/com.harryb97.tools.logger#logger/1.0.0",
"com.harryb97.tools.domainreload": "https://github.com/RazgriZ77/HarryTools.git?path=/Packages/com.harryb97.tools.domainreload#domainreload/1.0.1",
"com.harryb97.tools.serializereferences": "https://github.com/RazgriZ77/HarryTools.git?path=/Packages/com.harryb97.tools.serializereferences#serializereferences/1.0.1",
"com.harryb97.harryutils": "https://github.com/RazgriZ77/HarryUtils.git#2.0.0",
"com.cysharp.unitask": "2.5.10",
"com.kyrylokuzyk.primetween": "1.5.0"
```

- **UniTask** se instala desde OpenUPM: scope `com.cysharp.unitask` en el registro `https://package.openupm.com`.
- **PrimeTween** se instala de una de estas dos formas:
  - Con el `.tgz` del Asset Store (`"file:../Packages/com.kyrylokuzyk.primetween.tgz"`).
  - Desde el registro npm del autor: scope `com.kyrylokuzyk`, URL `https://registry.npmjs.org`.

## Vistas

### Montar la interfaz

Crea el `UIRoot` desde **GameObject > HarryTools > UI Root**. Contiene un Canvas a pantalla completa y las tres capas:

| Capa | Para qué | Orden |
|---|---|---|
| **Views** | Pantallas por las que se navega | debajo |
| **Popups** | Ventanas pequeñas, con cola y fondo oscurecido | en medio |
| **Overlays** | Lo que tapa todo, por ejemplo una pantalla de carga. Solo hay uno activo | encima |

Solo puede haber **un `UIRoot` activo**. Si aparece otro, se avisa por consola y se ignora. Ponlo en la escena de arranque o persistente, o marca *Dont Destroy On Load*.

### Crear una vista

Una vista es un prefab cuya raíz tiene un componente que hereda de `View`. `View` añade automáticamente `Canvas`, `CanvasGroup` y `GraphicRaycaster`.

```csharp
using HarryTools.UI;

public class InventoryView : View {
    [SerializeField] private TMPro.TMP_Text title;

    public InventoryView Setup(string _title) {
        title.text = _title;
        return this;
    }
}
```

Añade los prefabs a una **View Collection** (**Create > HarryTools > UI > View Collection**). La colección puede buscarlos sola en las carpetas que le indiques. Asigna la colección al `UIRoot`. También puedes colocar vistas directamente dentro de una capa de la escena: se ocultan al empezar y se muestran igual que las demás.

### Mostrar y ocultar

```csharp
UI.Show<InventoryView>().Setup("Mochila");   // capa Views
await UI.ShowAsync<ShopView>();              // espera a que se instancie y termine de entrar
UI.Hide<InventoryView>();                    // busca en todas las capas
view.Hide();                                 // desde la propia vista; se puede asignar a un botón
await UI.WaitUntilHidden<InventoryView>();

UI.Views.Show<MapView>();
UI.Popups.Show<ConfirmPopup>().Setup("¿Salir?");
UI.Overlays.Show<FadeOverlay>();
UI.Overlays.Hide();                          // oculta el overlay activo

UI.ViewShown += _view => Debug.Log(_view.name);
```

En la capa, la vista de arriba es la única que recibe la entrada. Con **Cull Views Behind**, las vistas que quedan detrás dejan de dibujarse. Para reaccionar a los cambios de estado, sobrescribe `OnShown`, `OnHidden`, `OnBecomeForeground` y `OnBecomeBackground`.

### Ciclo de vida (Lifetime)

Ocultar una vista **desactiva su `Canvas`**, no el GameObject. No reconstruye el layout ni llama a `OnEnable` u `OnDisable`, así que tener vistas ocultas cuesta casi solo memoria.

| Lifetime | Cuándo se crea | Al ocultarse |
|---|---|---|
| **Cached** (por defecto) | La primera vez que se muestra | Se conserva |
| **Preloaded** | Al iniciar el `UIRoot`, o con `await UI.PreloadAsync<T>()` | Se conserva |
| **Transient** | Cada vez que se muestra | Se destruye |

Usa **Preloaded** para las vistas pesadas o muy usadas. Se precargan en su **Default Layer**, que también es la capa donde se muestra la vista con `view.Show()` cuando todavía no pertenece a ninguna. Así, una pantalla de carga precargada debe tener *Default Layer = Overlays*.

`ShowAsync` y `PreloadAsync` instancian con `InstantiateAsync`, que reparte el coste entre varios frames. Si se cancelan, no dejan instancias sueltas.

### Popups

- Un popup con **Respect Queue** espera su turno si ya hay otro visible. `Show` devuelve la instancia al momento, así que puedes llamar a `Setup` aunque todavía esté en cola.
- Puede haber varios popups del mismo tipo con datos distintos.
- El fondo oscurecido aparece con el primer popup. Al hacer clic en él se oculta el popup de arriba, salvo que tenga desactivado **Hide On Backdrop Click**.
- `UI.Popups.HideAll()` oculta todos los popups y vacía la cola.

### Pantallas de carga con `ShowWhile`

```csharp
await UI.Overlays.ShowWhile<LoadingOverlay>(SceneManager.LoadSceneAsync("Nivel"));
await UI.Overlays.ShowWhile<LoadingOverlay>(CargarDatosAsync());
int monedas = await UI.Overlays.ShowWhile<LoadingOverlay, int>(ContarMonedasAsync());

// Con progreso: si el overlay implementa IProgress<float>, recibe lo que reporte el proceso
await UI.Overlays.ShowWhile<LoadingOverlay>(async (progress, ct) => {
    for (int i = 0; i < 10; i++) {
        await CargarParte(i, ct);
        progress.Report((i + 1) / 10f);
    }
});
```

El overlay se oculta siempre al terminar, también si la tarea lanza una excepción o se cancela. La excepción llega igualmente a quien hace el `await`. Con una `AsyncOperation`, el overlay recibe `operation.progress` en cada frame.

### Transiciones

En el campo **Transition** de la vista se elige la animación, que usa PrimeTween con tiempo sin escalar:

- **Fade**: opacidad.
- **Slide**: entra desde un lado y sale hacia otro.
- **Scale**: escala.
- **Parallel**: varias transiciones a la vez.

Si una vista se oculta mientras está entrando, la entrada se cancela. Para una animación propia, implementa `IViewTransition` o sobrescribe `OnEnterTransition` y `OnExitTransition` en la vista.

## Paleta de colores

Crea una paleta con **Create > HarryTools > UI > Color Palette** y asígnala al `UIRoot`. Para usar otra paleta solo en una rama de la jerarquía, añade un `PaletteProvider`. Si no hay ningún proveedor, se usa la paleta por defecto del paquete.

- **Roles de serie**: Background, Surface, Accent, Success, Warning, Danger, Black, White y Muted. Cada uno tiene su color de contraste para el texto que va encima.
- **Roles personalizados**: en *Roles personalizados* añade los que necesites, por ejemplo "Oro" o "Épico". Se guardan por id, así que puedes renombrarlos sin romper nada.
- **Tonos**: cada `ColorInfo` tiene un **Tono** de -3 a 3 para obtener versiones más claras u oscuras de un rol sin crear roles nuevos. Se calcula en el espacio OKLab.
- **Contraste**: el inspector avisa cuando un rol y su contraste no llegan a 4.5:1, el nivel AA de WCAG.

`ColoredGraphic` aplica un rol a cualquier `Graphic`. Si la paleta cambia, cambia de color con una transición.

## Iconos

Añade **IconProcessor** a un texto TMP y escribe las etiquetas en el texto:

```
<icon=home> Inicio      <icon=mdi:account-circle> Perfil      <icon=house>
```

- El set por defecto es **Material Design Icons 7.4.47**, con unos 7.400 iconos.
- Los nombres admiten `-` o `_` indistintamente, sin distinguir mayúsculas. También se aceptan los alias, como `house` para `home`.
- **Window > HarryTools > UI > Icon Browser** abre el buscador:
  - Busca por nombre, alias o etiqueta y filtra por set.
  - Un clic copia la etiqueta al portapapeles.
  - Un doble clic la añade al texto TMP que tengas seleccionado.
- **Window > HarryTools > UI > Icon Set Importer** crea sets nuevos a partir de una fuente TTF y su lista de iconos. Admite el `meta.json` de Material Design Icons y el fichero `codepoints` de Google Material Symbols. Usa solo fuentes con licencia libre. El set nuevo se usa con `<icon=id:nombre>`.

## Gráficos procedurales

- **RectangleGraphic** dibuja rectángulos redondeados por SDF:
  - Relleno con degradado.
  - Marco y contorno, que puede llevar degradado.
  - Sombra exterior, con desplazamiento.
  - Sombra interior.
  - Emboss.
  - Respeta el softness de **RectMask2D**.
- **GlowGraphic** dibuja resplandores.

Las opciones nuevas (degradado del contorno, sombra interior y desplazamiento de la sombra) usan un material propio, pero solo en los gráficos que las activan. Los demás siguen agrupándose en el mismo draw call.

El roundness puede seguir el estilo de la paleta: Button, Panel, Input o Item.

## Samples

Se importan desde el Package Manager:

- **HeroUI**: plantillas de botones, toggles, inputs, paneles, separadores y sliders con tema; una vista de diálogo; y la pantalla de carga `LoadingOverlay`, preparada para `ShowWhile`.
- **Demo**: escena con las tres capas, dos popups en cola y una carga con barra de progreso.

## Tests

Los tests EditMode y PlayMode están en `Tests/`. Para ejecutarlos, añade el paquete a `testables` en el manifest del proyecto.

## Créditos

- Basado en [PurrUI](https://github.com/PurrNet/PurrUI) (MIT), de Pebbles Games Consultancy Corporation y Riten SARL.
- Iconos: [Material Design Icons](https://pictogrammers.com/library/mdi/), de Pictogrammers. Licencia en `Runtime/Icons/Sets/MaterialDesignIcons/LICENSE.txt`.
