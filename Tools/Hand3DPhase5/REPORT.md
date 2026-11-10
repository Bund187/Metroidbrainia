# Phase 5 — Informe de implementación

## Alcance y archivos

Modificados:

- `Assets/Scripts/Experiments/Hand3D/Hand3DRig.cs`: selector Legacy/Universal, diagnóstico, composición y commit, restauración y fallback.
- `Assets/Scripts/Experiments/Hand3D/Hand3DArmMovement.cs`: objetivo y geometría IK compartidos; ruta Universal sobre buffers, sin escrituras finales de Transform.
- `Assets/Scripts/Experiments/Hand3D/Hand3DModeController.cs`: evaluación de una referencia proporcionada y tiempos universales sin reiniciar accidentalmente la transición.
- `Assets/Scripts/Experiments/Hand3D/Hand3DInteractionPose.cs`: consulta de la variante retenida durante liberación y Tick con duración externa.
- `Assets/Editor/Experiments/Hand3D/Hand3DPoseEditor.cs`: exportación explícita a una copia runtime.
- `Assets/Editor/Experiments/Hand3D/Hand3DEditors.cs`: diagnóstico del backend efectivo.
- `Assets/Scenes/TestArmScene.unity`: solo `poseSystem: 0` y referencia `runtimePoses` del Rig; el verificador elimina esas dos líneas y exige igualdad exacta con la escena inicial.

Creados:

- `Assets/Scripts/Experiments/Hand3D/Hand3DRuntimePoses.cs`: contenedor ScriptableObject de datos/asignaciones accesible en player.
- `Assets/Scripts/Experiments/Hand3D/Hand3DPoseComposition.cs`: compositor de datos por rig, sin nuevo componente.
- `Assets/Editor/Experiments/Hand3D/Hand3DRuntimePoseExport.cs`: exportación no destructiva y validación del asset.
- `Assets/Experiments/Hand3D/RuntimePoses/TestArmRuntimePoses.asset`: copia funcional independiente de 13 perfiles, con sus metadatos/carpetas generados por Unity.
- `Tools/Hand3DPhase5/`: referencia inicial, runner, pruebas, probe temporal, resultados e instrucciones.

No se eliminan archivos ni capturas. No se modifican Input, reconocedor, acciones, coordinador, detectores, renderizado ni overlay. No se realizan operaciones de Git.

## Decisiones y simplificación

La composición Universal sustituye las escrituras finales concurrentes de movimiento y rig por un buffer y un commit del Rig. La geometría de alcance/codo y la actualización del objetivo se comparten con Legacy; no se añade otro solver. Los perfiles se buscan por asignaciones e identificadores, sin comparar nombres de gestos en runtime.

Solo son necesarias dos clases runtime nuevas: datos persistentes y compositor. El exportador es exclusivamente Editor. No hay nuevos MonoBehaviours obligatorios, managers, buses ni un framework de acciones. La dependencia del orden de escrituras finales disminuye; no se cambia el orden de contacto/adquisición.

La duplicación Legacy/Universal es temporal y deliberada: no se retiran sus campos ni su ruta de reproducción. Los componentes todavía conservan inicialización, referencias y capturas Legacy necesarias durante la comparación. El reconocedor continúa aportando reconocimiento y los productores existentes aportan intención/weights; migrar su arquitectura no forma parte de esta fase. Por tanto no se presenta esta entrega como eliminación definitiva de toda la duplicación histórica.

## Validación y significado

Las ejecuciones guardan un runId y hashes para impedir aceptar resultados antiguos. Las comparaciones recargan escenas independientes; un backend no recibe como referencia los huesos ya alterados por el otro. Se compara entrada lógica, tiempo y desplazamientos iguales.

La batería anterior se ejecuta con Legacy: fase 0 = 611, fase 1 = 254, fase 2 = 668, fase 3 = 1.212, fase 4 = 940; total 3.685. No se atribuyen automáticamente esas mismas pruebas a Universal.

La batería nueva compara las 32 combinaciones en ambos modos, transiciones temporizadas de OK/Point, PointClick, agarres y liberación, cambios lógicos mientras se sostiene, inspección, movimiento y retorno. Incluye captura completa sintética de shoulder/bicep/forearm, reproducción de referencia de codo, conservación de longitudes/escalas, orientación final, límite imposible, ciclos de retorno, pausa con dt=0, alternancia de backends, rechazo de configuración incompleta y respeto a huesos no registrados. Dos fixtures reales ejercitan adquisición de puerta/documento y cancelación por pérdida de foco con Universal.

No equivale a probar visualmente las nueve puertas ni los tres documentos en Universal. La cobertura exhaustiva de gameplay previa sigue siendo Legacy; las pruebas manuales del README son necesarias para Universal, especialmente bloqueo/apertura automática, destrucción/desactivación durante contacto, cambios rápidos y presentación del documento.

Se compila y ejecuta una build Windows Development aislada. El probe comprueba carga de los 13 perfiles, ausencia de referencias a assemblies Editor en el tipo runtime, evaluación Universal con estado lógico OK y vuelta a Legacy. Usa `-batchmode -nographics`: demuestra compatibilidad con player, no calidad visual.

## Diferencias y riesgos

- **Semántica de codo:** Universal deriva la preferencia de la referencia activa. La comparación de movimiento registra diferencias internas de hasta aproximadamente 0,08037 unidades y 12,089 grados respecto a Legacy. No se clasifican como error numérico ni se ocultan con la tolerancia del extremo: se separan las métricas de mano, dedos y muñeca y se prueba el objetivo factible del solver. Su aceptabilidad visual sigue pendiente.
- **Orientación y extremos:** los informes registran máximos separados para mano, dedos y muñeca. Quaternions equivalentes se comparan mediante ángulo, no componentes.
- **Cambio de backend:** reconstruye estados de interpolación; conserva intención lógica y permite volver a Legacy, pero una conmutación a mitad de animación puede cambiar su fase visual. La revisión manual debe decidir si hace falta una transición específica de comparación.
- **Adquisición:** persiste el posible frame entre contacto y primera composición Grab, por el orden existente Rig/detectores. No se introduce una reordenación de gameplay sin validación.
- **Datos:** no hay sincronización automática entre biblioteca Editor y copia runtime. Los cambios requieren exportación y asignación explícitas. Los perfiles experimentales no se activan por nombre.
- **Geometría:** los offsets que alterarían segmentos IK se rechazan; las escalas no se escriben. No se implementa soporte genérico de offsets relativos a cámara fuera de las semánticas importadas.
- **Curvas:** se conservan las semánticas existentes: SmoothStep para dedos/modos/agarres y curva especial de orientación de muñeca. No se promete reinterpretar cualquier curva arbitraria como un sistema nuevo de animación.

El chequeo de integridad protege todos los archivos iniciales ajenos a la lista de cambios, incluida Prototype, scripts antiguos, prefabs compartidos, biblioteca original, paquetes y configuración global. También protege las referencias e informes aceptados de fases 0–4. La entrega se detiene en fase 5, con Legacy predeterminado y validación visual Universal pendiente.

## Resultados finales

Ejecución `45b53544c66d4587b45809a0795cd627`: 3.685 regresiones anteriores + 55.422 comprobaciones de fase 5 = **59.107 superadas**. Ver `Results/45b53544c66d4587b45809a0795cd627/phase5-results.json` y los cinco informes anteriores en esa misma carpeta.

Máximos medidos: posición de mano/dedos `1,47454e-6` unidades; error del destino factible IK `1,09257e-6` unidades; ángulo de dedos/muñeca `0` grados en las muestras comparadas. El máximo interno del brazo (`0,0803653` unidades / `12,0890` grados) se clasifica como diferencia de preferencia del codo, con revisión visual pendiente.

Build Windows Development: `Succeeded`, 0 errores. Probe standalone: `PASS`. Copia runtime instalada y escena configurada con Legacy. El manifiesto `tested-files.json` incluye scripts, metadatos, asset runtime y escena final; `integrity.txt` confirma los archivos protegidos. El cierre del Editor durante el build emite avisos internos de `JobTempAlloc`; no se ha determinado su origen y no se presentan como una medición de rendimiento del compositor.
