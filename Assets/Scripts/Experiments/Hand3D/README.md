# Brazo 3D experimental — guía actual

Ámbito exclusivo: TestArmScene, Unity 6000.6.3f1, URP y Unity Input System. Para desarrollar poses nuevas usar **Universal** en Hand3DRig. Legacy permanece como respaldo; no borrar sus capturas, bindings ni componentes. No hay que ejecutar Prepare ni importar de nuevo la escena para usar el sistema existente.

## Arquitectura y autoridad

| Elemento | Responsabilidad |
|---|---|
| Hand3DInputController | Traduce entrada a dedos, modo, movimiento y solicitudes de acción; comunica movimiento/fin al coordinador. No conoce puertas ni documentos concretos. |
| Hand3DFingerState | Única combinación lógica de cinco bits; Finger/ FlexedFingers describen los cinco dedos y 32 combinaciones. |
| Hand3DPoseRecognizer | Reconoce por combinación; nombre y OnAction original son datos compartidos. No ejecuta acciones ni escribe huesos. |
| Hand3DPoseActions | Asocia combinaciones con acciones reutilizables; Begin/Tick/End, prioridades, eventos y cancelaciones. |
| Hand3DModeController | Exploration/HandEdit, transición e inspección. Recibe la referencia Universal; conserva capturas de modos para Legacy. |
| Hand3DRuntimePoses / Hand3DPoseProfile | Datos de poses y asignaciones accesibles en player. No referencian clases/objetos de Editor. |
| Hand3DPoseComposition | Compone referencia de modo, extremos, gesto, clic/acciones, interacción y objetivo IK en buffers. Normaliza antes de TRS. |
| Hand3DRig | Selecciona backend; en Universal hace el único commit final de huesos y gestiona rechazo/fallback. Legacy conserva su ruta alternativa. |
| Hand3DArmMovement | Entrada espacial, retorno y solver de dos segmentos; calcula sobre el buffer en Universal. No sustituir referencia por pose transformada del frame anterior. |
| Hand3DInteractionCoordinator | Propiedad exclusiva local y adquisición/liberación; cancela acciones y delega movimiento restringido al propietario. |
| Hand3DInteractionPose | Petición, pesos y offset de muñeca compartidos; sus arrays de capturas visuales son respaldo Legacy. |
| Hand3DContactDetection / Hand3DScreenProjection | Proyección, barrido, alcance y obstáculos comunes, sin política única de selección. |
| Hand3DDoorInteractor / Binding | Selección y mantenimiento del pomo, movimiento consumido y adaptación al motor de puerta existente. |
| Hand3DDocumentInteractor / Binding | Contacto inicial, sujeción libre, ocultación/devolución del objeto y adaptación de sus datos. No depende del detector de puertas. |
| Hand3DDocumentDisplay / ThumbOverlay | Papel/texto de resolución completa entre brazo pixelado y pulgar; misma textura, máscara de geometría animada y tres modos diagnósticos. |

Orden principal: Input (-50), movimiento (100), Rig (200), coordinador (215), puertas (220), documentos (225), display (240). La adquisición posterior al rig conserva la posible latencia visual de un ciclo, documentada desde fases anteriores. No hay dos commits Universal compitiendo ni un manager global.

Flujo: entrada → estado/reconocimiento → solicitud de acción o movimiento → composición/IK → commit → contacto/adquisición → presentación. Una interacción cambia su capa visual; no modifica la combinación lógica de dedos. El evento de compatibilidad OnAction solo lo despacha PoseActions.

## Bibliotecas y trabajo habitual

- Edición: Assets/Editor/Experiments/Hand3D/PoseProfiles/HandPoses.asset.
- Runtime: Assets/Experiments/Hand3D/RuntimePoses/TestArmRuntimePoses.asset, asignado en Hand3DRig.
- Ventana: Tools → Metroidbrainia → Hand 3D → Pose Editor.

1. Fuera de Play, seleccionar PlayerCapsule/Capsule/armsFBX de TestArmScene en la ventana y asignar la biblioteca de edición. El selector de backend está en el rig: usar Universal para comprobar perfiles nuevos. La herramienta no cambia el backend ni guarda la escena automáticamente.
2. Elegir contexto Exploration, HandEdit, Interaction o Action. Crear un perfil con ID único (o dejar que se genere); duplicar crea otro ID. Cambiar Display name no cambia identidad. Nunca reutilizar un ID para otra pose por accidente.
3. Seleccionar una referencia/base apropiada: Mode.exploration o Mode.handEdit. Las capas relativas de muñeca requieren la base de muñeca relativa a cámara; usar Companion layers para previsualizar fragmentos complementarios. Una base/compañero de preview no es una dependencia publicada implícitamente.
4. Preview Pose aplica el perfil sobre una copia descartable; Begin Manual Pose permite posar esa copia desde la referencia. No editar los huesos originales para capturar Universal.
5. Elegir FullBody, ArmOnly, WristOnly, FingersOnly o Custom; Rotation, Position o ambos. HandEdit de un gesto debe afectar solo dedos. La referencia base HandEdit sí puede contener cuerpo. Position no permite estirar segmentos IK; el permiso explícito no elimina la validación geométrica.
6. Capture Pose modifica solo el perfil seleccionado; Restore/Cancel cierra la copia. Ajustar pesos, canales, contexto y transiciones en Selected profile data. Guardar con Save Library. Las capturas Legacy y la escena no se reescriben.
7. En Publish selected Universal profiles, asignar el runtime existente. Marcar explícitamente los IDs a añadir/actualizar. La selección inicial está vacía; Select current profile only es una elección explícita del usuario.
8. Validate and review selected publication valida rig, referencias, canales, máscaras, datos, asignaciones y bases relativas. Muestra ADD, UPDATE o UNCHANGED por ID. Confirm selective publication vuelve a validar y abre la confirmación con el destino y la lista exacta; Cancel no modifica el asset.
9. Publish selected profiles aplica y guarda solo esos perfiles, manteniendo IDs, los demás perfiles y las asignaciones actuales. Undo/Redo recupera datos completos del runtime; después usar Save runtime asset (including Undo/Redo) en su Inspector para persistir la reversión o el redo.
10. Los IDs nuevos permanecen inactivos hasta asignarlos explícitamente: Exploration/HandEdit para referencias, Gestures para capas de gesto, Grab Door/Grab Document para las interacciones, o profileId en una acción Profile Pulse. Editar estas asignaciones en el Inspector del runtime y pulsar Validate runtime assignments. Guardar el asset; entrar en Play con Universal y comprobar resultado. No hay sincronización automática mientras se juega.

La publicación **no** copia todo HandPoses.asset, no elimina perfiles y no sustituye asignaciones. OK_Test_Capture, Exploration_FullBody_Test y Partial_Capture_Test solo se publican si se seleccionan expresamente; un nombre de prueba no es una prohibición, tampoco una selección automática. Los perfiles nuevos vacíos/sin influencia se rechazan. Los fragmentos vacíos migrados que ya son placeholders funcionales pueden conservarse.

Initial migration / new runtime asset (advanced) conserva la exportación inicial de 13 perfiles funcionales a un archivo nuevo. No es el procedimiento habitual para actualizar TestArmRuntimePoses.asset y nunca sobrescribe un archivo existente.

## Gesto nuevo y acciones

1. En Hand3DPoseRecognizer añadir nombre y combinación única (0–31): pulgar=1, índice=2, corazón=4, anular=8, meñique=16. OK=3; Point=29. Los campos Visual Overrides y orientación capturada del reconocedor pertenecen a Legacy.
2. Crear/capturar/publicar sus capas Universal. En el runtime añadir una entrada Gestures con la misma combinación y sus IDs Exploration y HandEdit, en orden de composición. Incluir explícitamente los fragmentos complementarios necesarios. Para HandEdit de gesto, solo dedos.
3. En Hand3DPoseActions.Bindings añadir esa combinación y una o varias definiciones de acción. Un binding vacío significa deliberadamente ninguna acción; si no hay binding se utiliza el adaptador del enum Action antiguo.
4. Create → Metroidbrainia → Hand 3D → Actions permite crear Point Click, Configured Event o Profile Pulse según sus entradas existentes. PointClick conserva pulso/duración/amplitud; Configured Event es instantáneo y no conserva Run ni reloj. Profile Pulse exige Universal y un profileId publicado con contexto Action o del modo vigente; no admite el adaptador especial ExplorationWristOffset.
5. Prioridad alta se compone después; a igual prioridad se respeta el orden configurado. El evento de binding se invoca una sola vez por solicitud, incluso con varias acciones; OnAction original se conserva según invokeOriginalGestureEvent. No añadir otro emisor paralelo de esos eventos.
6. Gestos/modos/backend y adquisición pueden cancelar acciones. Ocupación/restauración bloquea solicitudes nuevas. Requires Arm Control y acciones World añaden sus permisos; World en HandEdit requiere opt-in explícito. No hay mecánicas ambientales nuevas en esta fase.

## Interactuables nuevos

Para interacción automática por contacto, crear un adaptador local del objeto y un owner que implemente IHand3DInteractionSession. Reutilizar Hand3DContactDetection con cámara/punto de mano/displays/capas propios; mantener su Sweep y política de candidatos por adaptador. Aplicar descartes baratos antes de HasObstacle; conservar exclusiones y sincronización por ciclo. Solicitar CanAcquire/TryAcquire al coordinador antes de poseer la mano. Un tercer tipo no requiere añadir su clase a Input.

El owner libera mediante coordinator.Release/CancelSession; su ReleaseInteraction restaura el objeto una sola vez. ProcessMovement devuelve si consume la entrada. Las puertas restringen movimiento; documentos no. El coordinador cancela acciones al adquirir y conserva Restoring hasta que la pose se desvanece; no mantener otra bandera independiente de propiedad. Gestionar desactivación/destrucción y callbacks reentrantes.

Puertas conservan prioridad sobre documentos por su orden de actualización, 1,5 m / 100 px, contacto/seguimiento y bloqueo/apertura del motor existente. Documentos usan 0,9 m / 55 px al adquirir, movimiento libre mientras se sostienen y devolución. Radios son píxeles de referencia a 1080p. No añadir un chequeo continuo de alcance inicial al documento.

Una acción de gesto dirigida a un objeto es otro caso: una subclase de Hand3DGestureAction valida contacto, alcance, obstáculos y objetivo en CanBegin usando el detector común; no obtiene un objetivo ficticio de Input. Si necesita exclusividad, solicita el coordinador y respeta liberación/cancelación. No existe un selector ambiental genérico implementado ni hay que introducirlo para un evento instantáneo.

## Diagnóstico

- Rig: Effective pose backend y UniversalError. Un rechazo restaura y vuelve a Legacy. No ocultar el error recapturando ni sobrescribiendo perfiles originales.
- Runtime: Validate runtime assignments identifica IDs/roles/contextos ausentes. Publicación falla antes de modificar el destino. Un perfil nuevo sin asignación no activa un gesto.
- Pose Editor: elegir base y companions correctos; revisar stored/influence channels, captured, enabled, weight y espacios. Perfil parcial no equivale a una referencia de modo completa.
- Movimiento: LastUniversalRequestedTarget, LastUniversalFeasibleTarget, LastUniversalReachError y LastUniversalTargetLimited distinguen límite geométrico de fallo. No sustituir solver ni mover escalas para alcanzar destinos imposibles.
- Contacto: Debug Contact y Log Interactions permiten revisar alcance, separación/barrido, candidato, blocker y estado del pomo. Hay nueve bindings de puerta y tres de documento; se conserva descubrimiento de puertas compatibles.
- Pulgar: Thumb Overlay, Thumb Mask y Full Arm; revisar textura compartida y orden brazo/papel/pulgar. Sample Source Pixels es diagnóstico síncrono explícito, incluso en Full Arm; no demuestra oclusión final del Canvas. El bucle normal Full Arm omite BakeMesh; la máscara animada no.
- Quaternions: NormalizeRotation en la frontera de UpdateWorld valida/normaliza entradas antes de TRS y la cadena mundial. Un dato irrecuperable informa hueso, origen, fase y asset; no eliminar esa comprobación para silenciar avisos.
- Rendimiento: fase 7 conserva tiempos sintéticos y contadores, no una mejora de FPS de Game. El contador GC del arnés falla el control positivo; usar Unity Profiler para GC Alloc real.

## Qué Legacy sigue necesitando

La evaluación Universal usa los perfiles runtime, no los valores de Extended/Flexed, overrides, modos o agarres capturados en componentes. **La inicialización aún valida sus bindings, flags y referencias** para preparar el respaldo. Por ello no es seguro borrar esas estructuras aunque sus valores no alteren la evaluación Universal. Se conserva también el adaptador Action del reconocedor y parámetros PointClick antiguos mientras no existan asociaciones explícitas.

Inspection limits/speed/smoothing/sign, cadena/cámara, estado lógico, pesos/offset de interacción, entrada y eventos son configuración compartida, no basura Legacy. El enlace de compatibilidad existente al controlador 2D en puertas no crece ni se modifica; no utilizarlo como patrón para interactuables nuevos.

Una retirada futura necesita autorización, inicialización Universal independiente, política de fallo sin fallback, migración/reversión de datos y pruebas del cambio de backend. Esta fase no realiza esa retirada. Los botones Legacy se conservan con advertencias claras; usarlos modifica la escena y requiere guardar, a diferencia de Pose Editor.

## Validación y referencias

Protocolo visual de cierre, inventario por script y evidencias: Tools/Hand3DPhase8/MANUAL.md, INVENTORY.md y REPORT.md. El protocolo se limita a lo realmente disponible en TestArmScene; no exige pausa/localización ni trasladar sistemas desde Prototype. La guía no declara revisión visual realizada.

Histórico: Tools/Hand3DPhase0, Hand3DPhase05 y Hand3DPhase1–7, más Hand3DQuaternionFix. Los documentos DOOR_INTERACTION.md, GENERAL_DOORS_GRAB.md, DOOR_ACQUISITION_VALIDATION.md y DOCUMENT_INTERACTION.md conservan detalles de integración; las instrucciones antiguas de captura no sustituyen el flujo Universal descrito aquí.
