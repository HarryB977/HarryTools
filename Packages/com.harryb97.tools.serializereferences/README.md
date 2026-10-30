# Serialize References

Sustituto de `com.mackysoft.serializereference-extensions` sin dependencias de terceros.

## Uso

```csharp
using HarryTools.SerializeReferences;

[SerializeReference, TypeSelector] private IMyInterface value;
[SerializeReference, TypeSelector] private IMyInterface[] values;
[SerializeReference, TypeSelector(AllowNull = false, UseToStringAsLabel = true)] private IMyInterface other;

[Serializable, TypeMenu("Categoría/Mi Tipo")] public class MyType : IMyInterface { }
[Serializable, HideInTypeSelector] public class HiddenType : IMyInterface { }
```

| Mackysoft              | HarryTools                 |
|------------------------|----------------------------|
| `[SubclassSelector]`   | `[TypeSelector]`           |
| `[AddTypeMenu]`        | `[TypeMenu]`               |
| `[HideInTypeMenu]`     | `[HideInTypeSelector]`     |

Los nombres son distintos a propósito: ambos paquetes pueden convivir en el mismo proyecto sin ambigüedades mientras se migra.

## Mejoras respecto al original

- **Referencias duplicadas**: al añadir un elemento a un array (o un elemento de un struct que contiene referencias), Unity copia la *referencia* del anterior, y ambos comparten instancia. Ahora se detecta y se convierte en una copia independiente automáticamente, también para referencias anidadas. Solo actúa en campos con `[TypeSelector]`. Se puede desactivar en *Preferences > HarryTools > Serialize References*.
- **Copias con `EditorJsonUtility`**: al cambiar de tipo, copiar/pegar o clonar se conservan las referencias a objetos de Unity (el original perdía assets y objetos de escena).
- **Tipos perdidos**: si una clase se renombra o elimina, el campo lo indica en rojo con un botón para limpiarlo. Para renombrar sin perder datos usa `[UnityEngine.Scripting.APIUpdating.MovedFrom]`.
- **Menú contextual** (click derecho > *Type Selector*): copiar, pegar (crea instancia nueva), pegar solo valores, hacer instancia independiente, resetear y vaciar.
- Clases anidadas, tipos no públicos y `[HideInInspector]` en los campos hijos funcionan correctamente.

## SerializeReference Explorer

*Tools > HarryTools > SerializeReference Explorer*

Reúne todos los componentes de las escenas abiertas (o del prefab en edición) que contienen `[SerializeReference]`, sin importar el drawer que usen, y permite:

- Editarlos desde un solo lugar (con Undo).
- Filtrar por tipo de referencia, buscar por nombre de objeto, componente o tipo.
- Ver y corregir referencias duplicadas (botón *Duplicados*) y limpiar tipos perdidos.
- Seleccionar el objeto en la jerarquía.
