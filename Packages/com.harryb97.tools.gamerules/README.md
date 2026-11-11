# GameRules

> **Obsoleto.** Esta es la última versión y no recibirá más cambios. Usa [Conditions](https://github.com/HarryB977/HarryTools/tree/main/Packages/com.harryb97.tools.conditions) (`openupm add com.harryb97.tools.conditions`), que cubre lo mismo y está mantenido.

Reglas que se comprueban para permitir o denegar una acción del juego. Se eligen y configuran desde el inspector.

## Instalación

```sh
openupm add com.harryb97.tools.gamerules
```

Depende de `com.harryb97.harryutils` y `com.mackysoft.serializereference-extensions` (el selector de subclases del inspector).

## Uso

```csharp
using HarryTools.GameRules;

[SerializeField] private GameRuleList buyRules;   // Validation + lista de reglas

buyRules.UpdateParameters(new ShopParameters { Gold = gold });
if (buyRules.ValidateRules) { ... }
```

En tiempo de ejecución se pueden añadir y quitar reglas con `AddRule`, `AddRules` y `RemoveRule`. `new GameRuleList(_other)` crea una copia.

## Crear una regla

```csharp
[Serializable]
public class HasEnoughGold : IGameRule {
    [SerializeField] private int cost = 100;
    private int gold;

    void IGameRule.UpdateParameters(GameRuleParameters _parameters) {
        if (_parameters is ShopParameters _shop) gold = _shop.Gold;
    }

    bool IGameRule.Validate() => gold >= cost;
}

public class ShopParameters : GameRuleParameters {
    public int Gold;
}
```

`GameRuleParameters` es una clase vacía: se hereda de ella para pasar los datos que necesiten las reglas. `ExampleRule` viene incluida y siempre es válida.

## Validación

| `GameRuleValidation` | `ValidateRules` es `true` si... |
|---|---|
| `AllTrue` | todas las reglas son válidas |
| `OneTrue` | al menos una es válida |
| `NoneTrue` | ninguna es válida |
| `AllFalse` | ninguna es válida (igual que `NoneTrue`) |
| `OneFalse` | al menos una no es válida |

Sin reglas, `ValidateRules` devuelve `true`. Las reglas del inspector se añaden en la primera llamada a `UpdateParameters` o `ValidateRules`.
