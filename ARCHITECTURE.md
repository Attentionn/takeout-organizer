# Organizer · Arquitectura

`src/Program.cs` coordina el menú bilingüe y los comandos `preview`, `apply` y `undo`. `src/Fechas.cs` reúne la lógica de fechas y `src/Organizador.cs` organiza los lotes. La vista previa genera `Plan.csv` y registra incidencias en `Revisar.csv`.

Aplicar y deshacer usan el lote existente en `Planes/`. Tras una interrupción se retoma el mismo lote. Preserva la correspondencia de cada medio con sus archivos auxiliares y la resolución de colisiones de nombres. Los casos ejecutables están en `tests/Check.ps1`.
