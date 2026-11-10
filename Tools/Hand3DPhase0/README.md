# Referencia del brazo 3D: fase 0

Este directorio prepara la refactorización; no implementa un nuevo runtime. Los scripts C# están fuera de `Assets` y no se importan en el proyecto original. No se ejecutan herramientas Prepare/Capture ni se guardan escenas.

## Referencia conservada

- `Reference/TestArmScene.unity.txt`: copia byte a byte de la escena guardada, fuera de Assets.
- `Reference/files.json`: SHA-256, tamaño y ruta de todos los archivos originales de Assets, Packages y ProjectSettings, más AGENTS.md. Permite detectar cambios, altas y bajas; nunca restaura archivos.
- `Reference/phase0-inventory.json`: inventario resuelto por Unity de la jerarquía del rig, componentes experimentales, bindings, cámaras, Canvas, RawImage y referencias directas del rig. Incluye JSON serializado, curvas, eventos y referencias identificadas mediante GlobalObjectId y ruta de jerarquía/asset.
- `Reference/phase0-combinations.json`: resultado de las 32 combinaciones en las 15 articulaciones, con las capturas actuales.
- `Reference/phase0-ik-chain.txt`: nombres y rutas reales de las tres articulaciones asignadas.
- `Results/`: resultados y log de la ejecución de esta fase. Un PASS de Editor no equivale a una validación visual en Game.

Los hashes conservan la identidad de los assets referenciados, pero no son una copia de seguridad de sus contenidos. La copia textual de escena y el inventario sí conservan los datos capturados. Para restaurar un modelo, textura o paquete cambiado posteriormente hace falta conservar su archivo original por los medios habituales del proyecto.

## Repetir

Desde la raíz del proyecto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase0/Reference.ps1 -Mode Verify
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase0/Run.ps1
```

El runner crea un destino nuevo en TEMP y copia Assets y ProjectSettings completos. Resuelve los paquetes mediante el PackageCache local ya instalado; no cambia los paquetes originales. Requiere Unity 6000.6.3f1, licencia disponible y cache local completo. No descarga ni instala paquetes deliberadamente. Si faltan dependencias o Unity solicita acceso a red, detener y revisar el entorno de validación.

La copia utiliza una resolución de Game de Editor, no un jugador standalone. Las pruebas invocan métodos actuales con pasos de tiempo explícitos cuando la API los admite; la entrada efectiva de adquisición se simula mediante reflexión, sin reemplazar el controlador. Los objetivos sintéticos permiten aislar detección de la colocación visual del rig. El código funcional de puertas, documentos, entrada y rig se copia sin adaptaciones ni stubs.

El runner verifica los originales antes y después y usa una marca para impedir ejecución accidental sobre el proyecto de trabajo. Por defecto exige un destino nuevo y nunca elimina destinos. Para reutilizar explícitamente una copia importada, usar `-OutputRoot <ruta-temporal> -ReuseIsolatedCopy`: exige la marca de origen y comprueba que el C# original y la escena copiados no hayan cambiado. Solo actualiza el arnés y sus resultados en esa copia. Un identificador de ejecución impide aceptar resultados antiguos. La ejecución inicial importa assets y puede tardar varios minutos.

`Reference.ps1 -Mode Capture` existe para crear una referencia nueva y se niega a sobrescribir una existente. No volver a capturar para hacer desaparecer una diferencia. Las fases futuras deberán distinguir cambios intencionados de datos preservados antes de adaptar esta verificación estricta.

## Límites de la fase

No se modifica el runtime, las poses, la escena original, Prototype, prefabs, metas, configuración ni controles. Las fases posteriores y la prueba técnica de poses completas no se inician automáticamente. Ver `REGRESSION_MATRIX.md` y `FULL_POSE_IK_PROPOSAL.md`.
