# Matriz de regresión: referencia actual

Las pruebas automáticas caracterizan el comportamiento actual, incluso cuando una política pudiera cambiarse más adelante. Las casillas manuales comienzan pendientes; no completar sin ejecutar Game en Unity y registrar resolución, dispositivo y evidencia.

| ID | Comprobación automática | Cobertura manual complementaria |
|---|---|---|
| REF | Componentes y referencias resueltas; 5 dedos/15 articulaciones, 9 bindings de puertas, 3 de documentos, perfiles, modos y máscara de 66 triángulos. Hashes originales. | [ ] Abrir escena y comparar jerarquía/Inspector con inventario. |
| F32 | Las 32 máscaras; OK=3, Point=29; rotaciones finales de las 15 articulaciones frente a Extended/Flexed + overrides. | [ ] Accionar cada dedo con teclado y mando; comprobar deformación y combinaciones durante interpolación. |
| ACT | OK sin acción; inicio y rechazo de clic superpuesto; cancelación por cambio de gesto; EventOnly y rechazo en pausa. | [ ] Curva temporal real de PointClick, amplitud, acción con LT/LMB en ambos modos y efectos reales. |
| MOV | Desplazamiento de ratón, límites leídos de escena, longitudes/offsets, pausa con dt=0, retorno y estados lógicos. | [ ] Ratón y stick a 30/60/144 FPS; centro, esquinas, alcance físico, retorno interrumpido y seguimiento de cámara. |
| MODE | Entrada/salida Exploration/HandEdit, congelación con dt=0, inspección con muñeca fija, persistencia lógica. | [ ] Alternar a mitad de transición; palma/dorso, signos y límites de inspección; gestos y muñeca en ambos modos. |
| DOOR | Adquisición por adaptador actual con marcador/puerta sintéticos; exclusión, cancelación, restauración, bloqueo y handoff a apertura automática. | [ ] Las 9 puertas reales, ambos sentidos, pared ajena y marco propio, cruce rápido, distancia, temblor completo, cierre y apertura hasta sus valores configurados. |
| DOC | Adquisición y evento único, ocultación, exclusión, devolución, restauración del mundo, seguimiento si el punto está proyectable, activación/textura/orden del pulgar y geometría no vacía. | [ ] Los 3 documentos, texto/idiomas/fuentes, tamaño, contacto percibido, seguimiento en esquinas, devolución suave y destrucción/desactivación. |
| LOGIC | OK conservado tras Grab y lifecycle; restauración de rotaciones de dedos tras Grab. | [ ] Repetir desde Point y cambiar dedos durante ambas interacciones; recuperar la combinación actual, no una foto inicial. |
| LIFE | Métodos reales de pérdida de foco y pausa de aplicación: cancelar y esperar liberación; HandEdit cancela documento; dt=0 congela movimiento. | [ ] Alt-Tab, pausa/reanudación durante recogida/devolución y Grab; RB/Shift con R2 mantenido; deshabilitar/re-habilitar componentes; restauración de locomoción y cursor. |
| RENDER | Referencias actuales, textura compartida, orden brazo/papel/pulgar y triángulos generados; sin comprobar píxeles finales. | [ ] Full Arm: brazo completo delante. Thumb Mask: silueta magenta siguiendo huesos. Thumb Overlay: solo pulgar delante; papel/texto intermedios y resto detrás. |

## Protocolo visual pendiente

1. Usar la escena actual sin ejecutar Prepare ni recapturar. Anotar Unity, resolución/aspecto, dispositivo y cualquier diferencia con los hashes de referencia.
2. Recorrer todas las combinaciones y probar OK/Point durante movimiento e inspección. Comprobar que controles del personaje se bloquean y restauran, incluidos Jump/Sprint y gravedad en HandEdit.
3. Probar cada una de las nueve puertas: contacto lento, entrada con R2 ya mantenido, barrido rápido, cancelación antes del umbral, apertura automática y bloqueo cuando corresponda. Registrar identificación de puerta y ángulos de esa instancia.
4. Probar los tres documentos, incluyendo eventos de descubrimiento existentes. Registrar sprite, idioma, fuente, tamaño y color. Verificar original oculto hasta finalizar devolución y su Transform intacto.
5. En HandHold, recorrer los tres modos del overlay; guardar capturas reales. El muestreo de alfa indica disponibilidad de textura, no acredita el resultado final del Canvas. Probar escala actual del rig y al menos 16:9 y 4:3.
6. Repetir cancelaciones por foco, pausa, cambio de modo y desactivación. Verificar dedo lógico actual, muñeca, ausencia de UI residual y controles restaurados.
7. Registrar PASS/FAIL por fila, evidencia y descripción de límites. No mezclar una prueba sintética con la validación del rig real en URP.

Las pruebas no simulan el transcurso real de `Time.deltaTime` del motor de puerta: comprueban el inicio de cierre, temblor y apertura automática, no su finalización visual o temporización. Tampoco emiten dispositivos físicos ni ejecutan tablas de localización reales en los documentos sintéticos.
