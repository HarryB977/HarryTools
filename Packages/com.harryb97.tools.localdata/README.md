# Local Data

Guarda y carga objetos como archivos JSON en `Application.persistentDataPath`, usando `JsonUtility`.

## Instalación

```sh
openupm add com.harryb97.tools.localdata
```

## Uso

```csharp
using HarryTools.LocalData;

[Serializable]
public class SaveData {
    public int coins;
    public string playerName;
}

bool _created = await LocalDataSystem.SaveData(data, "Saves", "Slot1");        // false si el archivo ya existe
bool _written = await LocalDataSystem.OverwriteData(data, "Saves", "Slot1");   // crea o sobrescribe
SaveData _data = await LocalDataSystem.LoadData<SaveData>("Saves", "Slot1");   // default si no existe o no se puede leer
bool _removed = await LocalDataSystem.RemoveData("Saves", "Slot1");

bool _exists = LocalDataSystem.CheckData("Saves", "Slot1");
bool _dirExists = LocalDataSystem.CheckDirectory("Saves");
```

El ejemplo guarda en `{persistentDataPath}/Saves/Slot1.json`. La carpeta se crea al escribir si no existe. Los métodos asíncronos devuelven `UniTask`.

## Limitaciones

- Rigen las reglas de `JsonUtility`: el tipo tiene que ser `[Serializable]` y solo se guardan los campos públicos o con `[SerializeField]`. Los campos que falten en el archivo se quedan con su valor por defecto.
- Los guardados `.dat` de las versiones 1.x no se leen.
- Si falla la escritura o el borrado se registra un error y se devuelve `false`. Si falla la lectura, un aviso y `default`.

## Sample

**Local Data Sample**, desde *Package Manager > Local Data > Samples*: un componente `LocalDataManager` con las opciones *Load Local Data*, *Save Local Data* y *Remove Local Data* en su menú contextual, que trabajan sobre la clase `TestData`.
