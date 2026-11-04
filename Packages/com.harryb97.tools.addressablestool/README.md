# Addressables Tool

Métodos estáticos sobre Addressables para cargar e instanciar assets por label, dirección, `AssetReference` o `IResourceLocation`. Cada carga devuelve el asset junto a su handle para poder liberarlo.

## Instalación

```sh
openupm add com.harryb97.tools.addressablestool
```

## Uso

```csharp
using HarryTools.AddressablesTool;

await AddressableSystem.Initialize();
await AddressableSystem.UpdateCatalogs();   // busca y aplica catálogos remotos nuevos
```

`UpdateCatalogs` está pensado para cuando en los ajustes de Addressables está activa la opción de actualizar los catálogos manualmente.

### Cargar

```csharp
(Sprite _icon, AsyncOperationHandle<Sprite> _handle) = await AddressableSystem.LoadAsset<Sprite>("UI/Icon");
// ...
Addressables.Release(_handle);

(IList<AudioClip> _clips, AsyncOperationHandle<IList<AudioClip>> _clipsHandle) =
    await AddressableSystem.LoadAssets<AudioClip>(addresses, _clip => Debug.Log(_clip.name));
```

`LoadAsset` acepta una dirección, un `AssetReference` o un `IResourceLocation`. `LoadAssets`, una colección de cualquiera de ellos y un callback opcional por asset. Si la carga falla, el asset es `default` pero el handle se devuelve igualmente.

### Instanciar

```csharp
(GameObject _enemy, AsyncOperationHandle<GameObject> _handle) =
    await AddressableSystem.InstantiateAsset("Enemies/Goblin", _position, _parent, Quaternion.identity);
// ...
Addressables.ReleaseInstance(_enemy);
```

Igual que `LoadAsset`, acepta dirección, `AssetReference` o `IResourceLocation`.

### Por labels

```csharp
// Unión de los labels. null si no hay ningún asset
IList<IResourceLocation> _locations = await AddressableSystem.GetAssetsLocations("Enemies", "Bosses");

AddressableGroup<GameObject> _group = await AddressableSystem.CreateAddressableGroup<GameObject>("Enemies");
if (_group.Count > 0) {
    await _group.LoadAllAssets(_prefab => Debug.Log(_prefab.name));
    foreach (GameObject _prefab in _group.Assets) { }

    _group.ReleaseAssets();
}
```

`AddressableGroup<T>` expone `Count`, `Locations`, `Assets` y `OperationHandle`.

## Limitaciones

- La rotación por defecto de `InstantiateAsset` es `default(Quaternion)` (0, 0, 0, 0), no `Quaternion.identity`. Pásala siempre.
- `CreateAddressableGroup` devuelve `null` si no se le pasa ningún label. Si ningún asset tiene el label, el grupo tiene `Count` 0 y `LoadAllAssets` no se debe llamar.
