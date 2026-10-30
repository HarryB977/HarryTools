# Unity Service Locator
Integración simple de un sistema que provee servicios en Unity.

## ¿Qué es exactamente?
Service Locator es un patrón de diseño que sirve para desacoplar la lógica de las entidades In-Game, de la obtención de métodos por medio de servicios en forma de interfaces.
Es una alternativa a otros patrones similares como la **Inyección de Dependencias**

Se usa a partir de un objeto centralizado, llamado "ServiceProvider", el cual crea y reparte instancias de servicios (Managers), las cuales poseen los servicios en forma de interfaces.
Para crear estas instancias, el "ServiceProvider" toma uso de un "Scope" en el que se registran los servicios.

Los servicios son inmutables, pero las instancias pueden variar su uso dependiendo del Scope al que pertenezcan.
De esta forma, una entidad dentro de la partida puede adquirir un servicio usando "ServiceProvider" y ejecutar un método del mismo, o recuperar el valor de una variable, sin que haya una conexión directa entre el Manager y ella misma.

# Instalación
1. Ve a **Unity**, en la pestaña de **"Window/Package Manager"**
2. Dale al símbolo de **"+"** y a la opción de *"Install package from git URL..."*
3. Pega el link de este repositorio [``https://github.com/RazgriZ77/Unity-ServiceLocator.git``]
4. Pulsa en **"Install"**

## Sample
El proyecto trae consigo un ejemplo de cómo inicializar Service Provider y la funcionalidad de los Scopes.
