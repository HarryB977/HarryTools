# Conditions

Condiciones modulares configurables desde el inspector. Depende de `com.harryb97.tools.serializereferences` y `com.harryb97.tools.scenerefs`.

## Instalación

Además de este paquete, el manifest del proyecto debe incluir `com.harryb97.tools.scenerefs` y sus dependencias `com.harryb97.tools.logger` y `com.harryb97.tools.domainreload` (los paquetes por git/file no resuelven dependencias entre sí).

## Uso

```csharp
using HarryTools.Conditions;

[SerializeField] private ConditionList conditions;   // Modo All (AND) / Any (OR)

if (conditions.Check(LogicContext.From(this))) { ... }
```

## Crear una condición

```csharp
[Serializable, TypeMenu("Puzzles/Is Solved")]
public class PuzzleIsSolved : Condition {          // 'Condition' añade la opción "Invert"
    [SerializeField] private Puzzle puzzle;

    protected override bool Evaluate(LogicContext _context) => puzzle != null && puzzle.IsSolved;
}
```

También se puede implementar `ICondition` directamente si no se quiere la opción de invertir.

## Conceptos

- **Síncronas**: una condición solo comprueba, nunca espera. Para esperar a que algo ocurra se usa la acción `Wait Until` del paquete *Actions*.
- **`LogicContext`**: `Self` (quién ejecuta) y `Target` (objeto secundario opcional). Permite condiciones reutilizables sin arrastrar referencias.
- **`TargetGameObject` / `TargetComponent<T>`**: campos que se resuelven desde `Self`, `Target` o un objeto asignado en el inspector.
- **Referencias por ID**: el origen `Custom` es un `SceneRef<T>`. Acepta objetos de la misma escena, de otra escena o de un asset, y los recupera por el ID del objeto (se muestra en la cabecera de su GameObject). Al cambiar a 2.0.0 hay que volver a asignar las referencias `Custom` y `Behaviour Is Enabled`.
- **Combinaciones**: `All (AND)`, `Any (OR)` y `Not` son condiciones más, se pueden anidar.

## Incluidas

Logic (Constant, All, Any, Not) · Random (Chance) · GameObject (Is Active, Compare Tag, Object Is Assigned) · Components (Behaviour Is Enabled, Animator Is In State, Audio Source Is Playing) · Transform (Distance) · Application (Is Platform, Is Debug Build)
