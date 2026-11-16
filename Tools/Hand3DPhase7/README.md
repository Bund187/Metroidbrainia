# Fase 7 — reducción de trabajo del brazo experimental

Solo tres fuentes de producción cambian: `Hand3DThumbOverlay.cs`, `Hand3DDocumentDisplay.cs` y `Hand3DPoseComposition.cs`. No hay componentes, dependencias, campos serializados, assets ni configuración nuevos. Legacy se conserva.

## Reproducir

Desde la raíz, con Unity 6000.6.3f1 y la copia temporal marcada de fases anteriores:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase7/Run.ps1 -ProfileOnly -BeforeOptimizations
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase7/Run.ps1 -QuaternionAudit -BuildPlayer
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase7/Compare.ps1 -Before Tools/Hand3DPhase7/Results/<beforeId> -After Tools/Hand3DPhase7/Results/<afterId>
```

`-BeforeOptimizations` sustituye exclusivamente las tres fuentes de la **copia aislada** por `Before/*.cs.txt`. Nunca restaura producción. `-ProfileOnly` omite las regresiones; `-Phase6Only` ejecuta acciones y fase 7. Sin esos flags, se ejecutan las baterías de fases 0–6. Se conservan los fixtures históricos; las adaptaciones privadas de montaje son las de fase 6, exclusivamente en TEMP. La prueba técnica histórica de 0.5 se consulta como referencia; no se vuelve a ejecutar su runner contra fuentes posteriores incompatibles con su manifiesto. Los ensayos actuales de fase 5 y auditoría ejercitan cuerpo completo/IK sobre ambos backends.

El runner protege por SHA-256 todos los archivos actuales de Assets, Packages y ProjectSettings salvo las tres fuentes permitidas. Protege también todos los archivos históricos de Tools de fases 0–6 y QuaternionFix. La escena completa se conserva byte a byte, sin excepciones. No usa Git, no descarga paquetes y nunca pasa el proyecto original como projectPath. `Prepare` solo recrea la copia temporal funcional como en fase 6. No ejecutar Prepare/Capture manualmente en producción.

## Qué se mide realmente

Prueba **sintética de Edit Mode**, no fotogramas de Game: 40 llamadas de calentamiento, cinco lotes de 100 llamadas, delta lógico fijo 1/60 y escenas independientes por backend. `empty_cancel` utiliza 1.000 llamadas por lote. Stopwatch mide CPU transcurrida por invocación. Las tablas usan la mediana de los cinco lotes; JSON conserva todas las muestras. Carga, compilación, importación y creación de componentes quedan fuera. No se mide memoria nativa ni GPU. El driver de cambios rápidos incluye reflexión, identificado en el JSON; no atribuir su coste al rig aislado.

**GC no disponible en este entorno:** `GC.GetAllocatedBytesForCurrentThread` devuelve cero incluso al reservar explícitamente un array de 4.096 bytes retenido con GC.KeepAlive. El arnés final comprueba ese control positivo, guarda `allocationCounterSupported=false` y usa `bytes=-1` para indicar dato no disponible. Los ceros de ejecuciones iniciales no son evidencia de ausencia de asignaciones. Medir GC Alloc en Unity Profiler durante Game; no usar diferencias de heap como sustituto de bytes asignados por frame.

Los contadores de BakeMesh, proyecciones y UpdateWorld se inyectan solo en las fuentes temporales. Hay un `ProfilerMarker` `Hand3D.Phase7.Synthetic` en el arnés; no se incorpora instrumentación al runtime original. No se entrega una captura de Unity Profiler de gameplay ni una comparación de FPS. La Development Build verifica compatibilidad funcional; su probe nographics no mide la composición visual ni rendimiento de Game.

El bucle mide reposo, componentes rig/movimiento/acciones por separado, movimiento continuo, cambios de dedos, retorno seguido de reposo, inspección, OK, ciclos PointClick, pose GrabDoor con contacto IK móvil, los tres modos del overlay, documento real sostenido con pose HandHold/movimiento y cancelación vacía. GrabDoor usa un objetivo sintético sobre la cadena real: no mide física, apertura ni selección de puertas. Las regresiones históricas aportan el resto de escenarios de estado/IK/acciones/contactos. No se atribuyen tiempos individuales a apertura real de puertas, devolución, textos/localización, stencil/GPU ni Canvas completo: no están aislados por este benchmark.

## Corrección específica

- Proyección usada por los 66 triángulos comparada con la fórmula original, sobre 32 combinaciones por backend, interpolación en curso, desplazamiento de mano, UV y layout variables.
- Geometría no vacía y número de vértices esperado para sus triángulos visibles, textura/UV compartidos, desactivación/reactivación, sustitución temporal de RT a 640×360, layout, diagnóstico explícito de Full Arm y documento real mostrado, sostenido y devuelto. Los contadores comprueban también el horneado al recoger.
- Traza de 64 posturas con posiciones/rotaciones de las articulaciones; `Compare.ps1` compara fuentes anteriores y posteriores, verifica finitud/norma y equivalencia de orientación. No sustituye imágenes rasterizadas.
- Las baterías históricas conservan pruebas de las 32 combinaciones, transiciones, cuerpo completo, contactos múltiples, puertas bloqueadas, documentos independientes, exclusividad, pausas/foco, acciones simultáneas/cancelaciones y ciclo de vida.
- La auditoría adicional conserva el replay del quaternion inválido, NaN/Infinity/cero, normalización extrema y fallback. No se rebaja ninguna tolerancia de las pruebas anteriores.

La variación de tamaño del RectTransform/UV prueba el cálculo de máscara y layout, **no** un cambio real de resolución de Game. La comprobación de resoluciones 720/1080/1440 de fase 1 es matemática. Ambas limitaciones requieren revisión manual.

## Perfilado complementario de Game

En Unity Profiler, CPU Timeline y GC Alloc, Deep Profile desactivado, usar el mismo equipo, resolución y backend. Esperar importación y 300 fotogramas de calentamiento; registrar al menos 600 fotogramas por escenario. Registrar inicialización/primera recogida aparte. Repetir en Editor y Development Build con Autoconnect Profiler; no mezclar sus cifras. Comparar medianas y distribución, no un pico aislado. Usar la copia marcada con las fuentes Before para el anterior y las actuales para el posterior, sin guardar escenas funcionales.

Escenarios: reposo; movimiento continuo; movimiento rápido/retorno; cambios rápidos de cinco dedos; OK/Point; PointClick/Configured Event/Profile Pulse ya configurados; HandEdit/inspección; agarre/apertura manual y automática/puerta bloqueada; documento sostenido/móvil; liberación/devolución. Repetir Legacy y Universal donde la acción sea compatible. Profile Pulse sigue exigiendo Universal.

Observar UpdateRig/Evaluate/UpdateWorld, UpdateMovement/SolveUniversal, Tick de interactores/HasObstacle, Physics.SyncTransforms/RaycastAll, display/RefreshMask/BakeMesh, Canvas y GC. Medir también pérdida de foco/pausa y cambio de resolución por separado. El coste de los marcadores del arnés no representa todo el brazo. Consultar REPORT.md para resultados y límites; MANUAL.md para aceptación visual pendiente.
