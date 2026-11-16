# Fase 8 — consolidación

Guía actual: `Assets/Scripts/Experiments/Hand3D/README.md`. Inventario completo: INVENTORY.md. Alcance/evidencias/resultados: REPORT.md. Aceptación visual pendiente: MANUAL.md.

## Ejecutar

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase8/Run.ps1 -Phase8Only
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase8/Run.ps1 -QuaternionAudit -BuildPlayer
```

Requiere Unity 6000.6.3f1, licencia y la copia temporal marcada de fases anteriores; se admite `-ValidationProject`. No instala paquetes, no usa Git y nunca utiliza producción como proyecto Unity. Se copian fuentes/escena actuales a la copia. `Phase5Validation.Prepare` recrea únicamente la referencia funcional temporal usada desde fase 5; no se publica ni se guarda sobre producción.

El manifiesto before-files.json protege todos los archivos de Assets/Packages/ProjectSettings salvo cinco scripts Editor y la guía actual. historical-files.json protege todas las herramientas/evidencias de fases 0–7 y QuaternionFix. Se rechazan assets inesperados. Runtime C#, escenas completas, perfiles/capturas, metas, UnityEvents, prefabs y componentes compartidos quedan protegidos por hash.

`-Phase8Only` compila y ejecuta solo consolidación; el modo completo añade fases 0–7 y opcionalmente auditoría/build. Los fixtures históricos se conservan en disco; dentro de TEMP se mantienen las adaptaciones privadas de montaje de fase 6. La prueba técnica de 0.5 sigue como referencia histórica; las regresiones actuales de fases 5–7/auditoría prueban la cadena/cuerpo completo con los backends vigentes. No se ejecuta su runner antiguo contra fuentes incompatibles con su manifiesto.

Los contadores de fase 7 y auditoría se inyectan solo en fuentes temporales. GC de fase 7 permanece N/D: el control positivo detecta contador no fiable. Las mediciones sintéticas no son FPS y esta fase no reclama otra mejora de rendimiento.

Los fixtures de fase 8 crean bibliotecas/runtime **nuevos y descartables solo en la copia**. Prueban revisión sin cambios, actualización/adición selectiva por ID, preservación de perfiles/asignaciones/capturas/eventos, rechazos atómicos, guardado/recarga, Undo/Redo y uso de perfiles publicados. No borran archivos de producción ni aceptan publicar durante Play o en destinos fuera del directorio runtime experimental.

La independencia de valores de captura se prueba cambiando rotaciones/posiciones de modo y tiempos Legacy antes de inicializar Universal, luego recorriendo 32 combinaciones, ambos modos, movimiento y capas de agarre. Otro caso negativo comprueba y documenta la dependencia de inicialización que aún exige flags/bindings Legacy: no se interpreta como un defecto nuevo ni como independencia completa. Ese caso emite deliberadamente el diagnóstico de captura inválida y exige rechazo; no es un error de Game observado.

La build Development se genera en TEMP y usa el probe runtime de fase 6 (PointClick/eventos, ambos modos/backends). El uso de un perfil publicado se comprueba en el fixture actual sobre runtime/compositor reales. El probe nographics no acredita apariencia ni proceso manual de clicks en la ventana.
