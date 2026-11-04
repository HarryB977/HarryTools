# MultiScene

Carga y descarga grupos de escenas en modo aditivo. Cada escena puede estar en la lista de escenas de la build o ser un Addressable.

## Instalación

```sh
openupm add com.harryb97.tools.multiscene
```

## Configuración

1. Crea el asset desde **Assets > Create > HarryTools > MultiScene > Scene Groups Config** y añade grupos a **Scene Groups**. Cada grupo tiene:
   - **Group Name**: nombre con el que se carga el grupo.
   - **Active Scene**: escena que se marca como activa al terminar la carga.
   - **Scenes**: escenas del grupo, cada una con su **Type**. Las de tipo `Tool` no se descargan al cambiar de grupo; las `Default` sí.
   - **Scene Actions**: acciones `SceneGroup.ISceneAction` que se ejecutan durante la carga (selector de tipo de *Serialize References*).
2. Añade el componente `SceneLoader` (o una subclase) a un objeto de la escena inicial y asigna el asset en **Scene Groups Config**.

## Uso

```csharp
using HarryTools.MultiScene;

SceneLoader.Instance.LoadSceneGroup("Gameplay");
SceneLoader.Instance.LoadSceneGroup(0);

await SceneLoader.Instance.LoadSceneGroupAsync("Gameplay", new SceneLoader.Properties {
    reloadDupScenes = false,     // true: vuelve a cargar las escenas que ya estén abiertas
    delayBeforeLoading = 0.35f,  // segundos
    delayAfterLoading = 0.35f
});
```

Al cargar un grupo primero se descarga el anterior (salvo sus escenas `Tool`) y después se llama a `Resources.UnloadUnusedAssets`.

### Pantalla de carga

```csharp
public class GameSceneLoader : SceneLoader {
    protected override void BeforeLoadSceneGroup() { /* mostrar */ }
    protected override void AfterLoadSceneGroup() { /* ocultar */ }
}
```

### Eventos

```csharp
SceneGroupHelper _helper = SceneLoader.Instance.sceneGroupManager;

_helper.OnSceneLoaded += _sceneName => { };     // al lanzar la carga de cada escena
_helper.OnSceneUnloaded += _sceneName => { };   // al lanzar la descarga de cada escena no Addressable
_helper.OnSceneGroupLoaded += () => { };        // con todo el grupo cargado y la escena activa asignada
```

`SceneGroupHelper` también se puede usar sin `SceneLoader`: `LoadScenes(SceneGroup, IProgress<float>, bool)` y `UnloadScenes()`.

### Scene Actions

```csharp
using HarryTools.MultiScene;
using HarryTools.SerializeReferences;

[Serializable, TypeMenu("Audio/Play Music")]
public class PlayMusicAction : SceneGroup.ISceneAction {
    public void OnSceneLoaded(string _sceneName) { }
    public void OnSceneGroupLoaded() { }
}
```

## SceneReference

Campo serializable para referenciar una escena. En el inspector se arrastra un `SceneAsset` y al lado se muestra su estado: `Build`, `Addressable` o `Not loadable`.

```csharp
[SerializeField] private SceneReference scene;

if (scene.State == SceneReferenceState.Regular) SceneManager.LoadScene(scene.Name);
```

Guarda el GUID de la escena. La ruta (`Path`) y la dirección de Addressables (`Address`) se sincronizan en el Editor al dibujar el campo, al mover o borrar escenas, al cambiar entradas de Addressables y antes de cada build. `State` se calcula cada vez que se consulta.

## Limitaciones

- Las escenas que no están en la build ni son Addressables se omiten con un aviso.
- Una escena ya abierta no se vuelve a cargar si `reloadDupScenes` es `false`. La comprobación se hace por nombre.
- La sincronización automática (al mover escenas, cambiar Addressables o hacer build) solo recorre los ScriptableObjects de `Assets/`. Las `SceneReference` de escenas y prefabs solo se actualizan al verse en el inspector.
