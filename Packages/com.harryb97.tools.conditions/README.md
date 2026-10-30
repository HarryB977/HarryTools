# Conditions

Condiciones modulares configurables desde el inspector. Depende de `com.harryb97.tools.serializereferences`.

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
- **Combinaciones**: `All (AND)`, `Any (OR)` y `Not` son condiciones más, se pueden anidar.

## Incluidas

Logic (Constant, All, Any, Not) · Random (Chance) · GameObject (Is Active, Compare Tag, Object Is Assigned) · Components (Behaviour Is Enabled, Animator Is In State, Audio Source Is Playing) · Transform (Distance) · Application (Is Platform, Is Debug Build)
