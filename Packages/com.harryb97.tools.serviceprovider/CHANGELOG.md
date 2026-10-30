# Change Log:

## 1.3.0

    - Feat: Nuevo componente 'ServiceBinding' que asigna una referencia de la escena (GameObject o componente) a un campo de un servicio marcado con '[ServiceBindable]'. Si el 'ServiceProvider' aún no está inicializado, el binding se aplica al inicializarse. Al destruir el componente, el campo vuelve a null
    - Feat: Interfaz opcional 'IServiceBindingCallbacks' para que el servicio reciba 'OnBind' / 'OnUnbind'
    - Feat: 'IServiceLocator.TryGetByImplementation' e 'IServiceScope.GetServiceByImplementation' para recuperar un servicio por su clase concreta
    - Feat: Inspector propio de 'ServiceBinding' con desplegable de campos '[ServiceBindable]'

## 1.2.0

    - Feat: Nueva interfaz opcional 'IServiceDispose' que permite a los servicios de ejecutar código previo al cierre de la aplicación (tanto en Build como Editor)

## 1.1.0

    - Se ha añadido la clase abstracta "ServiceProviderBase", para que solo se tenga que crear una clase que herede de ella y establecer el Scope deseado

## 1.0.3

    - Se ha añadido la carpeta 'Sample~'

## 1.0.2

    - Primera versión