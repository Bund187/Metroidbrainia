# Documentos con el brazo 3D — TestArmScene

## Configuración incluida

Se han preparado las tres instancias existentes: Elisabeth_BellHintNote, HabilityMemo y Elisabeth_IconNote. Conservan los componentes originales DocumentHandInteractable sin cambios. Cada instancia añade Hand3DDocumentBinding, con su sprite, LocalizedString, distancia particular y evento onPickedUp copiados del original. Fuente, tamaño y color se consultan en el documento original. Las claves de localización y los eventos de descubrimiento existentes se conservan.

En la instancia del brazo se añaden Hand3DInteractionCoordinator, Hand3DDocumentInteractor y Hand3DDocumentDisplay. DocumentHoldPoint es hijo directo de wrist.r, inicialmente situado como HandGripPoint. La UI Hand3DHeldDocument es una copia independiente del diseño de HeldDocument de Prototype, sin el componente HeldDocumentDisplay ni dependencia de ArmView. Solo se leyó Prototype; no se guardó ni modificó.

El controlador original DocumentHandInteractable.TryBeginInteraction requiere HeldDocumentDisplay, cuya implementación depende de ArmView. Por eso el adaptador experimental reproduce únicamente el ciclo de presentación/ocultación y conserva los datos originales; no activa un brazo sprite auxiliar ni cambia el contrato original. No se modifica ningún script compartido.

## Detección y exclusión

En Exploration, mantener R2/RMB y acercar la mano al documento. No hace falta L2/LMB. Se reutilizan la proyección real de la mano, la proyección del mundo, el cálculo del recorrido entre muestras y la comprobación de obstáculos del adaptador existente de puertas. Se respetan RawImage, UV, FOV y resolución final. No se comparan distancias físicas entre ViewModel y papel.

Screen Contact Radius usa píxeles a 1080p, escalados con la altura de Game (55 por defecto). Maximum Reach comienza en 0.9 m; también se respeta el límite particular copiado del documento. El raycast ignora jugador, ViewModel y el documento objetivo; paredes y otros objetos independientes sí bloquean. Solo se exige alcance/visibilidad al adquirir.

Los bindings activos de la misma escena se registran en OnEnable y se eliminan en OnDisable/OnDestroy, sin búsquedas globales por frame. Entre los documentos válidos se prioriza el contacto actual más cercano visualmente y después los cruces del recorrido. El documento elegido queda fijo hasta terminar. El coordinador es local al brazo y solo permite un dueño: una puerta o un documento. Durante la restauración visual se espera a que termine la transición antes de otra adquisición. Si ambos tipos coinciden exactamente en un frame libre, las puertas conservan la prioridad de su orden explícito de ejecución.

Las puertas conservan su detección, límites, empuje, bloqueo y motor DoorHandInteractable. Se añadieron únicamente el uso compartido de las proyecciones/obstáculos y la exclusión de propiedad. La liberación de una puerta no borra HandHold.

## Presentación legible

El documento se muestra como Image + TMP_Text bajo un mismo RectTransform en el Canvas Overlay normal, antes del RawImage del brazo para que los dedos se dibujen por delante. No entra en RT_PixelArm. Sprite, texto, fuente, color y cambios de idioma siguen unidos y a resolución normal de UI.

Hand3DDocumentDisplay se ejecuta después del rig y alinea Paper Grip con la proyección de DocumentHoldPoint. Attachment Duration suaviza únicamente la adquisición: después no se introduce retraso en el seguimiento. Screen Offset usa píxeles de referencia a 1080p; Rotation Offset y Visual Scale ajustan la presentación frontal de la unidad completa. Paper Grip es el DocumentGripPoint copiado dentro del papel: ajustarlo para elegir qué borde sujetan los dedos. DocumentHoldPoint se ajusta en el espacio local de la muñeca, sin tocar las articulaciones de los dedos.

Mientras se sostiene, el renderer y collider originales se ocultan y se restauran al terminar. Su Transform, jerarquía, materiales y escala nunca se modifican. Soltar R2/RMB, cambiar a HandEdit, desactivar componentes o destruir/desactivar el documento cancela la sujeción. No hay física de caída ni comprobación repetida de distancia inicial. Las acciones de pose se suspenden durante la sujeción, como en el flujo original; los botones individuales de dedos siguen funcionando y actualizan la combinación lógica que se recuperará al soltar.

## Capturar HandHold

Hand3DInteractionPose conserva los campos anteriores de Grab y añade un perfil HandHold independiente. La preparación solo inicializa este perfil nuevo con las rotaciones Flexed ya capturadas; es una base para ajustar visualmente, no una postura de papel calibrada para todos los documentos.

1. En el Inspector de Hand3DInteractionPose, seleccionar la pestaña **HandHold** de las herramientas de captura.
2. Añadir wrist.r y los forearm.Twist0.r/Twist1.r necesarios a Additional Hand Hold Bones. No añadir forearm.r ni el brazo principal: pertenecen al IK.
3. En reposo, pulsar **Capture Additional Neutral** antes de modificar esos huesos.
4. Si se necesitan pequeñas traslaciones de dedos, activar Capture Hand Hold Finger Positions y pulsar **Capture HandHold Finger Position Base** antes de moverlos. No se capturan posiciones del brazo/cámara/personaje.
5. Pulsar **Preview HandHold / Flexed Base**, ajustar y pulsar **Capture HandHold**. Guarda rotaciones locales de dedos y huesos adicionales, y las posiciones opcionales. No sobrescribe Grab, OK ni Point.
6. Recuperar la previsualización de reposo; si se usaron traslaciones, pulsar **Preview HandHold Finger Position Base** para restaurarlas. Guardar TestArmScene. No aplicar overrides al FBX ni al prefab compartido.

Hand3DRig sigue siendo el único escritor de muñeca/dedos/Twist. HandHold o Grab se mezclan sobre la pose lógica y tienen prioridad visual sobre sus ajustes de muñeca. Al terminar se recuperan suavemente la combinación ACTUAL, sus Visual Overrides y la orientación Exploration. Hand Hold Transition Duration controla esa transición. Los estados Extended/Flexed nunca se sustituyen por HandHold.

## Nuevos documentos y depuración

Ejecutar **GameObject > Metroidbrainia > Test Hand 3D > Prepare Document Interaction** en TestArmScene, fuera de Play. Usa Undo, añade componentes ausentes y conserva los bindings ya preparados. Después de editar sprite, LocalizedString, distancia o evento del documento original, usar **Sync From Original Document** en su binding. Los cambios de fuente/tamaño/color se leen directamente del original.

Para instanciar documentos durante Play, usar una instancia/variante experimental previamente preparada con binding: se registrará automáticamente. Un DocumentHandInteractable nuevo sin binding necesita preparación; sus campos privados no se inspeccionan mediante reflexión en runtime. No aplicar cambios al prefab compartido de Prototype.

Activar Debug Contact en Hand3DDocumentInteractor: muestra cantidad, candidata, distancias, proyección, collider bloqueante, motivo de rechazo y HandHold activo. Puede convivir con el panel de puertas; no genera logs por frame.

## Verificación

Compilación C# comprobada contra las referencias actuales de Unity: cero errores. Superadas 35 comprobaciones aisladas de documentos y 128 de regresión/serialización. Usan entrada simulada y comprueban adquisición, distancia, pared, barrido rápido, múltiples documentos, objetivo persistente, exclusión con puertas, ocultación/restauración, eventos, captura de HandHold, posiciones opcionales, seguimiento de Paper Grip, texto y recuperación de OK/Point. La copia de la escena final reabierta resuelve tres documentos, nueve puertas, UI, fuente y punto de sujeción; preparar de nuevo no duplica componentes.

Estas comprobaciones NO validan el aspecto en Game ni un cambio real de idioma cargado desde las tablas del proyecto. Revisar manualmente:

- Los tres documentos: proximidad, R2 mantenido antes del contacto, cruce rápido, rechazo desde lejos y detrás de una pared.
- Ajuste de DocumentHoldPoint y Paper Grip: borde sujeto, tamaño y orientación legibles, dedos delante del papel, sin saltos durante movimiento/retorno.
- Sprite y texto de cada documento, fuente/tamaño/color y cambio de idioma real mientras está sujeto.
- Desde OK y Point: coger, cambiar dedos, soltar y comprobar círculo, orientación y acciones restauradas.
- HandEdit, desactivar/destruir documento y soltar R2: sin UI residual ni objetos ocultos.
- Dos documentos cercanos y una puerta próxima: un solo agarre, sin cambiar objetivo.
- Puertas: ambos sentidos de bisagra, cancelación antes de 10 grados, apertura automática, bloqueo y restauración de Grab.

No se modifican Prototype, prefabs compartidos, scripts originales de brazo/documentos/puertas, cámaras, materiales, paquetes ni controles asignados.
