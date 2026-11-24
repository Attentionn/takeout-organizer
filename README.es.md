# Organizer

[English](README.md)

Organizer es una aplicación de consola para Windows que ordena fotos y videos de una o varias carpetas, incluidas las exportaciones de Google Fotos. Tú eliges los orígenes, el destino y el nombre de tu fototeca. Antes de mover nada, puedes revisar adónde irá cada archivo.

## Primer uso

1. Descarga y descomprime el proyecto, o clona el repositorio, en una carpeta donde puedas guardar archivos. Necesitas Windows con .NET Framework 4.8. Haz doble clic en **Organizer.cmd**. La primera vez compila el programa.
2. Elige **2 · Español** y luego **1 · Preparar una vista previa**. Escribe o arrastra una carpeta de origen y pulsa Enter. Repite para añadir más carpetas. Pulsa Enter en una línea vacía para terminar.
3. Elige dónde guardar la fototeca y qué nombre ponerle. Por ejemplo, si eliges `D:\Fotos` y la llamas `Recuerdos`, quedará en `D:\Fotos\Recuerdos`. Los orígenes y el destino deben ser carpetas independientes del mismo volumen. En un NAS, usa la misma unidad de red o el mismo recurso compartido para todas las rutas.
4. Al terminar el análisis, abre **Plan.csv** en la ruta que muestra el programa. Cuando quieras organizar los archivos, elige **2** y escribe **MOVER**. Para devolverlos a sus rutas originales, elige **3** y escribe **DESHACER**. Conserva la carpeta **Planes** junto al programa para que pueda encontrar el registro.

## Cómo queda la fototeca

```text
Recuerdos/
  2024/06 - junio/2024-06-07/Fotos/Vacaciones/
  2024/06 - junio/2024-06-07/Capturas/Facebook/
  2019/Mes desconocido/Videos/
  Sin fecha/Fotos/
```

El programa busca primero la fecha de toma en el JSON asociado de Takeout, luego en los metadatos del archivo y después en su nombre. Si solo conoce el año de la carpeta, usa **Mes desconocido**. Conserva los nombres de los álbumes. El idioma que elijas al crear la vista previa determina los nombres de los meses y las categorías.

Las capturas y los archivos de Facebook se reconocen por el nombre o la carpeta. Los videos con nombres hexadecimales de 32 caracteres quedan en **Posibles TikToks** cuando no hay otra pista de captura o Facebook. Se conservan originales, ediciones y duplicados. Si dos archivos tendrían el mismo nombre, se añade un sufijo como ` (2)`. Los adjuntos JSON, XMP, AAE y THM con pareja identificable siguen a su archivo. **Revisar.csv** recoge los casos ambiguos y los errores de lectura.

## Línea de comandos y desarrollo

```powershell
.\Build.cmd
.\Organizer.exe prever "D:\Fotos\Recuerdos" "D:\Importaciones\Camara" "D:\Importaciones\Takeout"
.\Organizer.exe aplicar ".\Planes\CARPETA_DEL_LOTE"
.\Organizer.exe deshacer ".\Planes\CARPETA_DEL_LOTE"
.\tests\Check.ps1
```

Sustituye `CARPETA_DEL_LOTE` por la carpeta que imprimió la vista previa. Desde la línea de comandos, aplicar y deshacer ejecutan el movimiento inmediatamente. Si se interrumpe, repite el mismo comando con el mismo lote. El código de salida es 0 cuando termina bien, 1 en caso de error y 2 cuando quedan archivos pendientes. Puedes elegir el idioma con `--lang es` o `--lang en` antes del comando.

Vuelve a ejecutar **Build.cmd** al modificar el código. Las pruebas usan archivos temporales generados. Git excluye el ejecutable compilado y la carpeta **Planes**, donde se guardan las rutas locales, los nombres de archivo y el registro de movimientos.
