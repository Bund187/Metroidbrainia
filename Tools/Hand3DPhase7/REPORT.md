# Informe de fase 7

## Resultado y alcance

Se reducen cálculos redundantes de la máscara del pulgar y reconstrucciones de jerarquía al aplicar perfiles locales. No hay una mejora de FPS demostrada: los tiempos de esta entrega son **pruebas sintéticas de CPU en Unity Editor**, no mediciones de fotogramas de Game. La aceptación visual y la captura comparativa de gameplay en Unity Profiler permanecen pendientes; el procedimiento está preparado en README.md y MANUAL.md.

Se conservan Legacy, perfiles, capturas, eventos y serialización. No se cambia la escena funcional ni se añaden componentes. No se usan operaciones de Git ni se inicia fase 8.

## Análisis actual, sin reutilizar supuestos antiguos

Se consultaron documentación, herramientas y pruebas de fases 0, 0.5, 1, 2, 3, 4, 5, 6 y QuaternionFix. Se revisaron Rig, PoseComposition, ArmMovement, PoseActions, InteractionCoordinator, ContactDetection, DoorInteractor, DocumentInteractor, DocumentDisplay y ThumbOverlay.

| Área | Hallazgo actual y decisión |
|---|---|
| Pulgar | RefreshMask horneaba y proyectaba los 872 vértices del brazo, aunque solo 51 aparecen en los 66 triángulos del pulgar. También lo hacía en Full Arm, que no usa máscara. Optimizado. |
| Recogida | TryShow llamaba RefreshMask antes de SourceFrame/ApplyFrame, que volvía a llamarlo inmediatamente. Eliminada la primera llamada; no se difiere la actualización inicial. |
| Composición Universal | Apply reconstruía toda la jerarquía después de cada escritura local aunque la siguiente escritura no leyera datos mundiales. Agrupado dentro del perfil, sin conservar poses entre fotogramas. |
| Rig/Legacy | Resolución de objetivos de dedos ya dirigida por eventos/transiciones; el buffer, lookup y capturas tienen ciclo de vida explícito. Se conserva. |
| Movimiento/IK | Longitudes, eje y referencias de reposo ya se inicializan una vez. El objetivo, cámara, codo y muñeca siguen siendo dinámicos. No se cambia el solver ni se añaden cachés. |
| Acciones | Las contribuciones reutilizan listas. Las asignaciones al iniciar acciones tienen ciclo de vida de solicitud, no son automáticamente basura por frame. Se conserva la protección de reentrancia de fase 6. No se deduce ausencia de GC del contador no fiable. |
| Coordinador | Referencias cacheadas al inicializar; LateUpdate solo valida al propietario activo. No necesita otro registro/manager. |
| Contacto/física | Fase 1 ya reutiliza la muestra de mano, descarta candidatos antes del raycast y sincroniza por ciclo de consulta. No se modifica Physics.SyncTransforms ni RaycastAll. |
| Puertas | Las comprobaciones de mantenimiento de Update y LateUpdate pueden observar estados diferentes antes/después de mover bisagra/pomo/cámara. No se deduplican mediante un resultado viejo del frame. |
| Documentos | Alcance solo al adquirir; mientras se sostiene se mantiene movimiento libre. Localización suscrita por documento, estilo/texto se asignan al mostrar o al recibir StringChanged. Se conserva. |
| UI | Escala/layout pueden cambiar con CanvasScaler, pantalla, sprite y jerarquía. Se conserva su cálculo por actualización; no se introduce una lista frágil de invalidaciones. |

## Cambios aplicados

1. ThumbOverlay mantiene el Mesh y las listas reutilizables y añade un HashSet local reutilizable para deduplicar índices del pulgar en cada refresco. Sigue leyendo la geometría animada mediante BakeMesh(true) en Thumb Overlay y Thumb Mask. Se conservan los índices originales, fórmula de proyección, triángulos, recorte near plane, stencil, materiales y misma textura. No hay caché de pose ni de cámara/layout.
2. Full Arm sincroniza imagen/layout y modo pero omite BakeMesh y proyección en el bucle normal. Sample Source Pixels sigue solicitando geometría explícitamente, incluso en Full Arm; el diagnóstico síncrono mantiene su coste deliberado y no entra en el bucle de animación.
3. DocumentDisplay deja una sola actualización de máscara al terminar ApplyFrame durante TryShow. Seguimiento, tamaño 0,58, transiciones, devoluciones y localización permanecen iguales.
4. PoseComposition acumula escrituras locales y usa FlushWorld antes de consumir datos mundiales, de componer muñeca o de recordar raíz, y al salir del perfil. No se cambia el orden de perfiles/canales/prioridades/acciones, ni se conserva una jerarquía sucia entre llamadas a Apply. NormalizeRotation/RotationNorm, validación de fuentes y frontera TRS permanecen intactas.

## Optimizaciones descartadas y complejidad

- No omitir BakeMesh por asumir que el pulgar está inmóvil al sostener papel. Huesos, brazo, cámara y layout siguen siendo dinámicos.
- No reemplazar solver, límites, preferencia de codo, ni reutilizar el resultado del frame anterior. No alterar geometría para ahorrar trabajo.
- No cachear perfiles/máscaras/índices resueltos por un framework nuevo: los datos pueden editarse y deben observarse sin invalidaciones incompletas. Las búsquedas existentes no justifican esa complejidad en este alcance.
- No sustituir RaycastAll por un buffer truncable ni eliminar sincronizaciones entre ciclos de movimiento. Un ahorro hipotético no justifica perder obstáculos o pomos móviles.
- No cachear el tamaño del documento ni el layout con un conjunto incompleto de claves de invalidación.
- No cambiar enumeraciones, cancelar con pools ni reescribir listas sin una medida fiable que justifique la complejidad. El contador de asignaciones del entorno falla el control positivo; no se reclama ninguna reducción de GC.

La complejidad aumenta ligeramente: un conjunto reutilizable de índices, una sobrecarga privada para muestreo explícito, un booleano local y un helper de flush. Se considera proporcionada a la reducción medida. No hay managers, servicios globales, nuevos MonoBehaviours, paquetes, pools ni cachés de poses. El sistema de acciones/gestos se mantiene extensible sin especializaciones nuevas.

## Archivos

Modificados en producción, todos bajo `Assets/Scripts/Experiments/Hand3D/`:

- `Hand3DThumbOverlay.cs`.
- `Hand3DDocumentDisplay.cs`.
- `Hand3DPoseComposition.cs`.

Creados exclusivamente bajo `Tools/Hand3DPhase7/`: Run.ps1, Phase7Validation.cs, Compare.ps1, README.md, REPORT.md, MANUAL.md, before-files.json, historical-files.json, snapshots Before y evidencias por ejecución en Results. El snapshot de PoseActions conserva una referencia de análisis; su fuente de producción no cambia. No se elimina ningún archivo de producción ni se modifica ninguna meta.

## Condiciones y límites de las cifras

Unity 6000.6.3f1, mismo equipo/copia temporal, Edit Mode batch con gráficos, no Play. Escenas independientes, dt lógico 1/60, 40 llamadas de calentamiento y cinco lotes de 100 llamadas; mediana en microsegundos por invocación. Importación, compilación, carga e inicialización se excluyen. El JSON conserva las cinco muestras, no solo el resumen.

**GC Alloc no disponible:** GC.GetAllocatedBytesForCurrentThread devuelve cero incluso con una reserva explícita de byte[4096] retenida mediante GC.KeepAlive. El arnés final incluye este control positivo, almacena allocationCounterSupported=false y marca bytes=-1 (N/D). Se retira la interpretación inicial de los ceros como ausencia de asignaciones. No hay mejora de GC demostrada; hace falta la captura de GC Alloc de Unity Profiler en Game. El HashSet y las listas se reutilizan por inspección de código, pero no se convierte esa observación en una cifra de bytes/frame.

Los contadores son instrumentación de las fuentes temporales. Un ProfilerMarker delimita el arnés sintético; no hay captura de gameplay del Profiler ni marcadores añadidos al runtime de producción. La build Development y el probe nographics validan ejecución, no rendimiento ni píxeles finales.

Los tiempos de reposo/retorno/HandEdit mezclan las llamadas indicadas y el driver; no representan automáticamente todo el sistema. GrabDoor_pose_contact_IK es una pose de agarre y objetivo de contacto móvil sintético sobre la cadena real, no apertura física de las nueve puertas. Documento incluye display, máscara y step de brazo/acciones/rig. No aislar su diferencia como si fuera solo coste de UI. La inicialización tiene contadores de horneado al recoger, pero no se presenta un tiempo de carga o de primera recogida fiable.

No se han medido separadamente física, mantenimiento de puertas reales, GPU/stencil, localización, reconstrucción final de Canvas ni devolución. Su cobertura de estado viene de las regresiones. El protocolo de Profiler incluye los diez escenarios pedidos y distingue Editor de Development Build; se necesita ejecución controlada en Game para cuantificar esos costes.

Las pruebas de máscara varían layout, UV, textura RT temporal a 640×360, dedos y movimiento. No sustituyen un cambio real de resolución de Game, idioma ni una revisión de oclusión sobre el papel. El manual exige estas comprobaciones en ambos backends. No se reclama ausencia de diferencias perceptibles sin esa aceptación.

## Evidencias intermedias conservadas

- `86d5bb6bd03643359f6c8548fdbd7152`: referencia inicial antes de editar runtime, 3.400 comprobaciones y tiempos sin contadores añadidos.
- `29917da110b249c3a375ad30af221ee2`: primer grupo, 92.495 regresiones de acciones y 3.400 comprobaciones de máscara.
- `7c74ee07dee2473c86e5f8be2b082a46`: referencia original con contadores y traza de posturas.
- `7ac40d40ad344e8cbba03ec7e73eb647`: grupos de máscara/composición, 151.602 regresiones, 3.400 comprobaciones adicionales, auditoría, build y standalone aprobados. Comparación con la referencia: 2.816 comprobaciones, posición máxima 0 y ángulo máximo 0,0000115812°. Posteriormente se preservó también el diagnóstico explícito Full Arm y se amplió el arnés; consultar la ejecución final para las fuentes definitivas.
- `4e9df922ba994fd89f64cf8dea567818`: intento fallido del arnés ampliado: GetMethod no discriminaba la sobrecarga heredada de OnPopulateMesh. Se corrigió especificando VertexHelper. No se cuenta como PASS ni se cambió producción para acomodar esa prueba.

## Resultados finales

Fuentes definitivas: regresión completa `67b395df091448b9b0b0356e543ea24d`; referencia sintética final `719af63905a54f8da14db73f556b4083`; medición optimizada final `a399d7bf67c241a583507f21a7bb9cdb`. Las dos últimas usan el control positivo de GC y exigen que PointClick realmente comience. Los hashes de las tres fuentes de producción de la regresión y de la medición final coinciden. No se reutiliza el intento fallido del arnés.

| Batería | Comprobaciones | Resultado |
|---|---:|---|
| Fase 0 | 611 | PASS |
| Fase 1 | 254 | PASS |
| Fase 2 | 668 | PASS |
| Fase 3 | 1.212 | PASS |
| Fase 4 | 940 | PASS |
| Fase 5 | 55.422 | PASS |
| Fase 6 | 92.495 | PASS |
| Total histórico 0–6 | **151.602** | **PASS** |
| Fase 7, arnés final | 3.472 | PASS |
| Comparación de trazas anterior/posterior | 2.816 | PASS |
| Total anterior más pruebas de fase 7 y comparación | **157.890** | **PASS** |
| Auditoría adicional de quaternions | 26.032.513 aserciones numéricas | PASS |
| Development Build Windows | Succeeded, 0 errores | PASS |
| Probe standalone | PointClick y eventos en dos modos × dos backends | PASS |

Las 3.470 comprobaciones de fase 7 de la ejecución completa también pasaron; la versión final añade dos comprobaciones de inicio efectivo de PointClick (una por backend). No se suman dos veces esas baterías ni se cuentan los intentos anteriores como escenarios nuevos.

Auditoría: **12.737.250 entradas TRS**, cero errores Quaternion To Matrix, cero errores por backend. Desviación máxima de norma al cuadrado local `9,19855338565867e-8` y mundial `9,881951257284527e-8`. Son los mismos límites que antes. El número de entradas baja frente a las 15.219.136 anteriores porque se reconstruye menos la jerarquía, no porque se reduzca el recorrido de estrés ni se relajen aserciones. La auditoría sigue ejecutando sus 6.000 frames principales por backend, retorno, ciclos y entradas inválidas.

La traza de 64 posturas compara 22 articulaciones: máximo error de posición **0 unidades**, máximo ángulo **0,0000115812°** al comparar los quaternions serializados en JSON con cálculo de doble precisión. No cambia ninguna captura. `serialization-check.json` registra las 7 + 14 declaraciones serializadas de overlay/display idénticas; composición no tiene campos serializados.

### CPU sintética antes/después

Todas las cifras siguientes son **µs por invocación**, mediana de cinco lotes, en las condiciones de Editor descritas anteriormente. No son ms por fotograma de Game. Las 32 operaciones completas y cada muestra están en `Results/a399d7bf67c241a583507f21a7bb9cdb/comparison.json` y sus JSON de origen. Una diferencia negativa significa menos tiempo en esta prueba; las fluctuaciones pequeñas de controles sin cambios no acreditan mejoras.

| Operación/componente | Antes µs | Después µs | Diferencia µs | Condiciones | Riesgo específico |
|---|---:|---:|---:|---|---|
| Thumb Overlay, Legacy | 423,425 | 42,624 | -380,801 | RefreshMask, pose fija tras calentamiento | Índices/oclusión: oracle geométrico y revisión visual |
| Thumb Mask, Legacy | 425,877 | 41,773 | -384,104 | RefreshMask | Igual que overlay |
| Full Arm, Legacy | 431,091 | 1,011 | -430,080 | Refresco normal, sin diagnóstico explícito | Diagnóstico separado probado |
| Thumb Overlay, Universal | 424,421 | 43,178 | -381,243 | RefreshMask | Alineación final de Canvas pendiente |
| Thumb Mask, Universal | 417,409 | 43,234 | -374,175 | RefreshMask | Igual que overlay |
| Full Arm, Universal | 438,195 | 1,065 | -437,130 | Refresco normal | Igual que Full Arm Legacy |
| Rig Universal en reposo | 403,909 | 377,229 | -26,680 | Solo UpdateRig, no todo el juego | Flush previo a lectura mundial |
| Reposo Universal | 405,783 | 378,179 | -27,604 | Movimiento + acciones + rig | Igual que rig |
| Movimiento continuo Universal | 434,631 | 416,770 | -17,861 | Input sintético + step | IK sin cambios; traza comparada |
| Cambios de dedos Universal | 457,235 | 438,704 | -18,531 | Driver reflectivo incluido | No atribuir todo el coste al compositor |
| Retorno seguido de reposo Universal | 395,945 | 393,974 | -1,971 | Lote incluye reposo tras terminar retorno | No aísla coste de retorno activo |
| HandEdit Universal | 396,649 | 375,765 | -20,884 | Inspección por llamada | Orientación/transiciones en regresiones |
| OK Universal | 520,480 | 407,994 | -112,486 | Gesto establecido + step | Agrupación local, sin cachear gesto |
| PointClick Universal | 420,242 | 406,340 | -13,902 | Ciclos reales Begin/Tick/fin + step | Acciones/prioridad en fase 6 |
| GrabDoor, pose/contacto IK Universal | 803,065 | 509,799 | -293,266 | Objetivo móvil sintético, no física de puerta | Lecturas mundiales tras flush |
| Documento sostenido Legacy | 442,486 | 56,883 | -385,603 | Movimiento + HandHold + display + máscara | Texto/oclusión requieren Game |
| Documento sostenido Universal | 1.158,215 | 523,141 | -635,074 | Movimiento + HandHold + display + máscara | Igual, más agrupación de perfil |
| Rig Legacy en reposo, control | 6,153 | 6,191 | +0,038 | Solo UpdateRig, sin cambios en su código | Variación de medición; no mejora atribuida |
| Movimiento Legacy en reposo, control | 1,870 | 1,846 | -0,024 | Solo UpdateMovement | Variación de medición |
| Acciones inactivas, ambos backends | 0,018 | 0,018 | 0,000 | Solo Tick sin ejecuciones | Sin cambio ni mejora demostrada |
| GC Alloc | N/D | N/D | N/D | Control positivo de 4 KB devuelve 0 | No interpretar el contador como cero GC |

La mejora de máscara y de los perfiles de agarre es consistente con las llamadas eliminadas y con las ejecuciones intermedias. No se declara una mejora de FPS ni del coste total del juego. En Legacy, fuera de máscara/display, no hay mejora significativa demostrada.

### Reducción verificada de operaciones

| Operación | Antes | Después | Condición y alcance |
|---|---:|---:|---|
| Proyecciones de vértice | 872 | 51 | Cada RefreshMask normal de Thumb Mask/Overlay; ambos backends |
| BakeMesh normal de máscara | 1 | 1 | Se mantiene para seguir toda la animación |
| BakeMesh / proyecciones Full Arm | 1 / 872 | 0 / 0 | Bucle normal; el diagnóstico explícito sí calcula geometría |
| BakeMesh durante TryShow | 2 | 1 | Medido al recoger documento; ambos backends |
| UpdateWorld en reposo Universal | 21 | 20 | Cada step sintético |
| UpdateWorld con OK Universal | 27 | 21 | Cada step de gesto establecido |
| UpdateWorld GrabDoor/contacto IK Universal | 42 | 26 | Cada step del objetivo sintético móvil |
| UpdateWorld con documento Universal | 39 | 24 | Cada step + display sostenido |

No hay contadores inventados de raycasts, sincronizaciones, texto o GPU: esas rutas no cambian y no se les atribuye una reducción de llamadas.

## Integridad y trabajo pendiente del usuario

Verificación por hash aprobada antes y después: **Prototype.unity, TestArmScene completa, sistema sprite, FirstPersonArmController, ArmView, DoorHandInteractable, DocumentHandInteractable, prefabs, Starter Assets, perfiles/capturas/UnityEvents, paquetes y ProjectSettings intactos**. Todas las herramientas y evidencias anteriores permanecen intactas. Ningún archivo de producción creado o eliminado; solo las tres fuentes enumeradas cambian.

Queda ejecutar el protocolo breve de MANUAL.md en Universal y Legacy, especialmente pulgar/papel durante movimiento y devolución, cambios reales de resolución, textos localizados y pausa/foco. El protocolo de captura de Unity Profiler de README.md permite completar CPU/GC de Game y distinguir Editor/Development Build. No se marca esa revisión como realizada. La implementación y las comprobaciones automatizadas de fase 7 terminan aquí; no se inicia fase 8 ni se retira Legacy.
