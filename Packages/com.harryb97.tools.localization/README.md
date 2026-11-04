# Localization

Capa sobre el paquete de localización de Unity (`com.unity.localization`). Inicializa el sistema, mantiene las tablas del idioma actual y busca textos y sprites por su key. Incluye componentes para textos TMP y para un selector de idioma.

## Instalación

```sh
openupm add com.harryb97.tools.localization
```

## Uso

```csharp
using HarryTools.Localization;

await LocalizationSystem.Initialize();       // idioma elegido por los Startup Selectors de Unity
await LocalizationSystem.Initialize("es");   // fuerza un idioma por su código

LocalizationSystem.OnInitalize += () => { };
LocalizationSystem.OnSelectedLocaleChanged += () => { };

LocalizationSystem.ChangeLocale("en");       // también acepta un Locale o un índice de AllLocales
```

Estado: `Initialized`, `CurrentLocale`, `AllLocales` y `GetCurrentLocaleIndex`.

### Textos y sprites

```csharp
string _title = LocalizationSystem.GetString("menu_title");
string _score = LocalizationSystem.GetString("score_label", 120);   // strings marcados como smart
int _count = LocalizationSystem.GetStringsCount("tutorial_");      // keys que contienen el texto
Sprite _icon = await LocalizationSystem.GetSprite("shop_icon");    // tabla de assets, se carga por Addressables

string _key = LocalizationSystem.GetLocalizedStringKey(localizedString);
```

`GetString` busca la key en todas las tablas de strings y devuelve `""` si no la encuentra. `GetSprite` devuelve `null`. Hay que llamarlos después de `Initialize`.

## Componentes

### Localization Text

Pone en un `TextMeshProUGUI` el texto de un `LocalizedString`. Lo actualiza en `Start` (esperando a que `LocalizationSystem` esté inicializado) y cada vez que cambia el idioma.

- **Key**: el `LocalizedString`.
- **Initialize On Start**: no tiene efecto en el código actual.
- **Mesh Label**: el texto TMP. Se rellena solo con el del mismo objeto al añadir el componente.

Para cambiar la key desde código: `SetKey(LocalizedString)`.

### Selector de idioma

- **Localization Select Language**: al inicializarse la localización instancia un **Item** por cada idioma disponible dentro de **Content**.
- **Localization Select Language Item**: el prefab de cada idioma.
  - **Label**: texto TMP con el nombre nativo del idioma.
  - **Flag**: `Image`, no se usa en el código actual.
  - **Check**: `Image` que se activa en el idioma seleccionado.

  Asigna `Call_OnSelectLocal()` al `OnClick` de un botón para seleccionar ese idioma.

## Banderas

Crea el asset desde **Assets > Create > HarryTools > Localization > Localization Config**:

- **Default Flag**: sprite (Addressable) que se usa si el idioma no está en la lista.
- **Flags**: pares `Locale` + sprite (Addressable).

`LocalizationConfig` no se carga solo: asigna la instancia antes de usarla.

```csharp
LocalizationConfig.SetInstance(config);
Sprite _flag = await LocalizationConfig.Instance.GetFlag(LocalizationSystem.CurrentLocale);
```

## Limitaciones

- Los sprites de `GetSprite` y `GetFlag` se cargan con Addressables y el paquete no libera sus handles.
- En el Editor el estado estático se reinicia al entrar en el Modo Play, aunque la recarga de dominio esté desactivada.
