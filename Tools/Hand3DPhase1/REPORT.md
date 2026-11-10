# Resultado de fase 1

Ejecución final: `Results/a32036ce58884aabb87fdaf4901f1695/`, Unity 6000.6.3f1, copia aislada del proyecto. **611 regresiones originales PASS y 254 comprobaciones adicionales PASS**, distribuidas estas últimas en 30 grupos. No se ha ejecutado una validación visual final en Game ni se ha iniciado la fase 2.

Evidencias: `baseline.json`, `report.json`, logs de migración/regresiones/comparación e `integrity.txt` del directorio final. Los intentos anteriores se conservan con sus errores de compilación/arnés; no se presentan como ejecuciones aprobadas. El campo scope del JSON original de fase 0 conserva su texto histórico «unmodified runtime»: el arnés es el original sin modificar, pero esta ejecución prueba el runtime de fase 1.

## Qué cambia

Se introduce una sola clase de producción, `Hand3DContactDetection`, y ningún MonoBehaviour nuevo. Los dos interactores dependen de ella y de la proyección existente. La utilidad calcula la posición efectiva de mano, desplazamiento barrido, proyección/visibilidad, distancia, contacto, tolerancia relativa a resolución y obstáculos. Los interactores siguen siendo responsables de estados, colección/selección de candidatos y comportamiento de su interacción. Se conserva el coordinador existente.

Desaparecen la dependencia funcional y la dependencia de tipo de documentos hacia el detector de puertas. El antiguo campo contact queda únicamente como referencia serializada oculta e inerte. El display ahora obtiene su proyección desde el interactor de documentos. El preparador de Editor usa una puerta opcional como fuente de configuración inicial; no es un requisito de ejecución ni sobrescribe ajustes independientes existentes.

La separación permite reutilizar los cálculos en otro interactuable sin acoplarlo a puertas; no evita implementar sus reglas particulares. No introduce managers, registros globales, interfaces universales ni un framework de interacción. El código común de selección NO se fuerza a ser idéntico: puertas mantiene su desempate con tolerancias y documentos su puntuación estricta original.

La reducción es de duplicación de cálculos y dependencias, no de líneas totales: los dos scripts anteriores sumaban 596 líneas; ahora los dos scripts y el helper suman 721. La diferencia corresponde a datos/consultas explícitos, configuración independiente, compatibilidad serializada y contadores. No se afirma una reducción de tamaño total ni de FPS sin medirlos.

## Datos preservados

El runner compara los 1.981 archivos originales contra fase 0, permitiendo exclusivamente los cuatro scripts indicados en README y la escena. Los 1.976 restantes deben mantener su hash. También verifica que las fases 0 y 0.5 no cambien y que no aparezcan assets inesperados. Los snapshots Before coinciden por SHA-256 con sus originales de fase 0.

Al quitar únicamente el bloque detection del interactor de documentos, la escena es textualmente idéntica a la referencia: todos sus demás componentes, valores, referencias, eventos, capturas, curvas y bindings permanecen intactos. No se vuelve a serializar la escena completa de producción con Unity.

La configuración nueva copia los valores efectivos originales:

| Dato | Valor |
|---|---|
| Cámara del mundo | fileID 1668637022 |
| Punto de contacto | fileID 690538582 |
| RawImage del brazo | fileID 1511958091 |
| RawImage del mundo | vacío, igual que antes |
| Offset de contacto | (0, 0), igual que antes |
| Máscara de obstáculos | 4294967291, igual que antes |
| Puertas | 1,5 m / 100 px de referencia; 9 bindings |
| Documentos | 0,9 m inicial / 55 px de referencia; 3 bindings |

No se han modificado Prototype, FirstPersonArmController, ArmView, motores compartidos de puertas/documentos, prefabs, recursos, paquetes, ajustes globales, controles, poses, IK, renderizado pixelado ni Hand3DThumbOverlay. No se han realizado operaciones de Git. La única adaptación del display es cambiar el componente proveedor de proyección, sin alterar sus cálculos visuales.

## Pruebas automatizadas

Los detectores anteriores se ejecutan realmente, con tipos renombrados y contadores transparentes, frente a los actuales. Ambos usan los componentes de brazo/motores actuales y escenarios sintéticos independientes recargados desde TestArmScene. Se simulan estados de entrada y posiciones de contacto; no se simula un jugador con dispositivo físico ni se valida una imagen final.

| Grupo | Resultado |
|---|---|
| Puerta sin detector de documentos activo | Adquisición equivalente |
| Mano entrando lentamente / cruce rápido | Equivalentes en ambos tipos |
| Fuera de alcance / pared independiente / fuera de pantalla | Rechazo equivalente |
| Puerta y marco propio / pomo sin trigger / fuera de zona | Comportamiento equivalente |
| Contacto a 2 px del borde de pantalla | Adquisición equivalente en ambos tipos |
| Varios candidatos de cada tipo | Mismo candidato cercano |
| Puerta rechazada por motor y siguiente candidato válido | Mismo fallback |
| Contacto simultáneo puerta/documento | Puerta adquiere, documento no |
| Agarre de puerta con mano separada visualmente | Se mantiene |
| Puerta sale de alcance | Cancela y conserva estado lógico |
| Puerta bloqueada, final de intento y liberación | Sin reintento hasta soltar; permite intento posterior |
| Puerta compatible sin binding previo | Descubierta y adquirida |
| Documento mantenido alejándose más allá del alcance inicial | Se mantiene; devolución y estado lógico correctos |
| Liberación de documento con dt=0 | Cancelación equivalente |
| Documento con detector de puertas deshabilitado / eliminado | Antes no adquiría; ahora adquiere en ambos casos |
| Escalado 720/1080/1440 y tamaño de historial | Matemática correcta; cambio de tamaño o mano invisible reinicia barrido |
| Proyección actual y orden de ejecución | Proyección numérica en pantalla del Editor y órdenes 220/225 conservados |

Las 611 regresiones originales añaden cobertura de las 32 combinaciones de dedos, reconocimiento/acciones, modos, movimiento/retorno, ciclo de vida, agarres, eventos y referencias de renderizado. Sus comprobaciones estructurales del pulgar no acreditan píxeles finales.

En la prueba de resolución NO se cambia Game a 720/1080/1440: se prueban las conversiones de tolerancias y el tamaño del historial. La proyección utiliza la pantalla real del Editor, con tolerancia de roundtrip de 0,25 px. Los intentos previos que sustituían resolución por pixelRect se descartaron como método de validación; no motivaron cambios en el algoritmo de producción.

## Eficiencia medida

Contadores de llamadas de consulta, no tiempos de CPU:

| Escenario sintético | Antes → después |
|---|---|
| Puerta fuera de alcance, zona o sin pomo | Raycasts 1 → 0 por ciclo |
| Documento fuera de alcance | Raycasts 1 → 0 por ciclo |
| Dos puertas elegibles | Proyecciones de mano 4 → 1; raycasts 2 → 2 |
| Rechazo y selección de puerta siguiente | Proyecciones de mano 6 → 1; raycasts 3 → 3 |
| Dos pasos de cruce rápido de puerta | Proyecciones 6 → 2; raycasts 2 → 1 |

No hay caché persistente. Cada selección comparte su muestra de mano y sincroniza Physics al necesitar el primer raycast. Se conserva RaycastAll para no introducir límites de capacidad ni otro sistema de buffers.

## Comprobación manual pendiente en TestArmScene

No ejecutar Prepare ni recapturar para probar esta entrega: la escena ya contiene la configuración migrada. No hay un helper nuevo que añadir como componente.

1. Probar las nueve puertas reales: contacto lento/rápido, marco propio y pared ajena, alcance de 1,5 m, bloqueo y temblor completos, apertura manual/automática, liberación y necesidad de soltar antes de reintentar. Comprobar ángulos/tiempos particulares de cada instancia.
2. Deshabilitar Hand3DDoorInteractor en Play. Recoger los tres documentos, mover libremente el brazo, alejarse del alcance inicial, soltar y comprobar devolución. Rehabilitarlo al terminar la prueba. Verificar eventos, papel ampliado y texto localizado.
3. En documentos, revisar el overlay existente con Full Arm, Thumb Mask y Thumb Overlay. Confirmar visualmente brazo detrás, papel/texto en medio y solo pulgar delante, siguiendo su animación. No se ha validado este resultado visual en esta fase.
4. Repetir contacto en centro/bordes y cruce rápido en Game a 720p, 1080p y 1440p, y distinto aspecto. Registrar resolución/FPS y contacto percibido; el escalado matemático aprobado no garantiza equivalencia perceptiva.
5. Probar candidatos solapados y prioridad real; pasar a HandEdit, soltar en pausa, Alt-Tab, cancelar/deshabilitar durante agarre. Comprobar restauración de dedos, muñeca, locomoción y cursor con ratón y mando.

Limitaciones conocidas: futuras modificaciones de cámara/capas/offset de puerta ya no actualizan automáticamente documentos; hay que configurar ambos contextos. Para candidatos descartados antes de física, el diagnóstico muestra el rechazo temprano y no investiga posibles obstáculos. No hay otros cambios de gameplay intencionados ni regresiones automatizadas pendientes. La aceptación visual queda pendiente de estas pruebas. Fin de fase 1.
