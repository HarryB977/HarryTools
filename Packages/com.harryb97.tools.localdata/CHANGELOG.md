# Change Log:

## 2.0.0

    - Los datos se guardan en JSON con 'JsonUtility' en vez de 'BinaryFormatter' (obsoleto e inseguro). Los archivos pasan de '.dat' a '.json' y los guardados anteriores no se leen
    - El tipo guardado tiene que ser '[Serializable]' con campos serializables por 'JsonUtility'. Los campos que falten en un archivo antiguo se quedan con su valor por defecto
    - 'OverwriteData' trunca el archivo, así que unos datos más cortos ya no dejan restos al final
    - Si un archivo no se puede leer, 'LoadData' avisa con un warning y devuelve 'default'

## 1.0.1

    - Los Assembly Definitions ahora referencian por GUID en vez de por nombre, así que siguen funcionando aunque se renombre un ensamblado

## 1.0.0

    - Primera versión