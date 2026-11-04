# Change Log:

## 1.1.3

    - Añadido README con la instalación, el uso y la configuración
    - Dependencia `com.harryb97.harryutils` de 1.1.2 a 2.0.1: la 1.1.2 no está publicada y no se podía resolver desde OpenUPM

## 1.1.2

    - Publicado en OpenUPM: se instala con `openupm add com.harryb97.tools.addressablestool` y el Package Manager avisa de las actualizaciones
    - `package.json`: añadido `repository` y el autor ya no incluye email
    - LICENSE: el titular pasa a ser HarryB97

## 1.1.1

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.1.0
    
    - Se ha añadido un método de inicialización
    - Se ha añadido un método para actualizar los catálogos remotos
    - Se han añadido métodos para cargar o instanciar assets y devuelven una tupla de dicho asset y su handle
    - "tags" renombrado a "labels" para unificar la nomenclatura con el package de addressables
    - Más "summaries" en los métodos para facilitar el uso de la herramienta

## 1.0.0

    - Primera versión