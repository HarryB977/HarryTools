# Domain Reload

Sustituto de `com.jsteinhauer.unitydomainreloadhelper` sin dependencias de terceros.

Con *Enter Play Mode Options* y la recarga de dominio desactivada, los campos estáticos conservan su valor entre sesiones de Play. Estos atributos los restauran al entrar en el Modo Play. Si la recarga de dominio está activa no hacen nada, porque Unity ya reinicia los estáticos.

## Uso

```csharp
using HarryTools.DomainReload;

// Vuelve a su valor por defecto (null, 0, false...)
[ClearOnReload] private static Action OnChanged;

// Vuelve al valor indicado
[ClearOnReload(10)] private static int lives = 10;

// Recibe una instancia nueva del tipo del campo (necesita constructor sin parámetros)
[ClearOnReload(AssignNewInstance = true)] private static List<int> values = new();

// Se ejecuta al entrar en el Modo Play (método estático sin parámetros)
[ExecuteOnReload]
private static void ResetCache() { }
```

## Limitaciones

- Solo campos y métodos **estáticos**. Los campos `readonly` y `const` se ignoran con un aviso.
- Los tipos genéricos abiertos (`MyClass<T>`) se ignoran: no hay una instancia concreta que limpiar.
- Solo actúa en el Editor. En las builds siempre hay recarga de dominio.
