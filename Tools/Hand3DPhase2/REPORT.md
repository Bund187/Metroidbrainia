# Resultado de fase 2

**Fase 2 completada.** Ejecución final: `Results/6881531ced0842b68e8ba08ddfbe806a/`, Unity 6000.6.3f1, proyecto temporal completo marcado. Se han superado **1.533 comprobaciones automatizadas: 611 de fase 0, 254 de fase 1 y 668 de sesiones**, estas últimas en 38 grupos. Ningún fallo. No se inicia fase 3.

Los resultados están en phase0-results.json, phase1-results.json y phase2-results.json, junto con logs, hashes tested-files.json e integrity.txt. Los scripts de producción coinciden por hash con los de la copia probada. Los otros dos intentos de esta fase también terminaron sin fallos; el último incluye la eliminación del estado duplicado de puerta y amplía las pruebas de desactivación de Input, liberación ajena y cambio de dedos durante agarre.

## Cambios comprobados

| Antes | Ahora |
|---|---|
| Input conoce dos detectores para movimiento, acciones y cancelación | Input conoce solo el coordinador local para movimiento y fin del control |
| Input decide bloqueo de gestos consultando cada tipo | PoseActions consulta la política del coordinador, incluso en llamadas directas |
| Coordinator guarda solo owner y busca pose al consultar disponibilidad | Guarda la sesión, cachea referencias y deriva restauración del peso existente |
| Puerta mantiene booleano grabbing además de owner | IsGrabbing consulta la propiedad del coordinador; booleano eliminado |
| Cada adaptador solicita su pose y gestiona salida visual/contacto | El coordinador solicita y libera GrabDoor/GrabDocument y contacto |
| PointClick puede seguir progresando tras adquisición | Adquisición cancela el clic; acciones bloqueadas durante sesión y restauración |

La interfaz nueva tiene solo limpieza específica y procesamiento de movimiento. No hay clases de producción nuevas ni componentes adicionales: la interfaz y el enum se declaran en el archivo del coordinador existente. No hay bus global, manager, registro de interactuables ni ejecutor de acciones nuevo. El callback ArmControlEnded es local y permite limpiar un intento de puerta ya finalizado, sin hacer que Input vuelva a conocer puertas.

La limpieza pasa por Release una sola vez para el propietario. Borra owner antes del callback, bloquea reentrada y solicita restauración en finally. Cancelar de nuevo o liberar con otro propietario no cambia la sesión. El coordinador no escribe huesos ni cambia dedos lógicos; esa composición sigue en los componentes actuales.

Los adaptadores mantienen la detección de fase 1 y las diferencias de comportamiento: contacto, pomo y apertura en puerta; adquisición y devolución en documento. El coordinador dirige movimiento solo al propietario que lo restringe. Documento mantiene movimiento libre. Restoring representa la mezcla visual de mano; la devolución del papel sigue su transición propia sin prolongar la exclusividad, igual que antes.

## Archivos y conservación

Cinco archivos modificados, todos bajo Assets/Scripts/Experiments/Hand3D:

1. Hand3DInteractionCoordinator.cs
2. Hand3DInputController.cs
3. Hand3DDoorInteractor.cs
4. Hand3DDocumentInteractor.cs
5. Hand3DPoseActions.cs

Solo se crean archivos de herramientas/documentación/resultados bajo Tools/Hand3DPhase2. No hay archivos de producción creados ni eliminados y no se editan metas. **TestArmScene es byte a byte idéntica al comienzo de esta fase**, incluidos todos sus campos serializados, referencias, UnityEvents, poses, curvas y bindings.

El inventario before-files.json verifica todos los archivos anteriores de Assets, Packages y ProjectSettings, y los de las fases 0, 0.5 y 1, exceptuando únicamente los cinco scripts autorizados. No admite assets inesperados. Prototype, sistema sprite, FirstPersonArmController, ArmView, motores compartidos de puerta/documento, prefabs, Starter Assets y configuración global permanecen intactos. No se usa Git.

## Comportamientos conservados y diferencias explícitas

Se conservan prioridad puerta/documento, distancias y tolerancias de escena, barridos, adquisición automática, pomo y restricciones de puerta, apertura manual/automática, bloqueo/temblor y requisitos de soltar. Se mantienen GrabDocument, movimiento libre, papel ampliado, localización, overlay actual, devolución y combinación lógica vigente. Documentos no vuelve a depender de puertas.

La política nueva evita conflictos de acciones: un PointClick previo se cancela al adquirir, no se reanuda, y las acciones nuevas esperan al fin de la mezcla de salida. Esto cambia expresamente la superposición antes permitida, sin modificar poses ni curvas. Las acciones ordinarias fuera de interacción siguen con su ciclo de vida anterior.

Deshabilitar el coordinador cancela su sesión; deshabilitar propietario, rig, movimiento o pose impide mantener/adquirir sesión. No se fuerza una mezcla cuando su rig está detenido. Las rutas de R2, modo, foco, pausa y restauración del control de jugador conservan el orden anterior. En particular, la actualización del bloqueo de locomoción durante foco/pausa espera al Update válido o a OnDisable: no se introduce otra política de pausa en esta fase.

## Validación automatizada

Las 865 comprobaciones previas se repiten. Los arneses aceptados siguen intactos. Solo se adapta su montaje temporal a los campos privados eliminados y al coordinador antiguo de los snapshots; no cambian las aserciones ni sus expectativas. Ver README para las sustituciones exactas.

Las 668 comprobaciones nuevas usan contactos y entradas efectivas sintéticas con los componentes reales. Cubren, para puerta y documento:

- Adquisición, estado/owner, exclusividad y rechazo de liberación por un adaptador ajeno.
- Movimiento restringido/libre y bloqueo de acciones durante propiedad y restauración.
- Cancelación idempotente y repetición de adquisición/liberación tres veces.
- Salida por R2, modo, foco, pausa de aplicación y guard de Update en pausa.
- Desactivación/destrucción del objeto y del adaptador; desactivación de Input, coordinador, movimiento, pose y rig.
- Conservación del estado lógico, incluido cambiar los dedos a máscara 17 mientras el Grab está activo.
- PointClick iniciado con Point real, con peso intermedio inyectado para simular progreso: adquisición limpia el clic y su peso. No es una prueba de imagen rasterizada.
- Cancelación reentrante desde onPickedUp del documento: no quedan owner, solicitud de pose ni papel oculto residual. En puerta el grupo homónimo ejercita cancelación ordinaria; puerta no tiene ese evento de recogida.
- Paso de restauración a Free cuando el rig termina la mezcla; documento restaura renderer/collider tras devolución.

Las comprobaciones aceptadas adicionales prueban candidatos múltiples, prioridad, fallback de puerta, intentos bloqueados, apertura automática, alcance/obstáculos y documentos independientes del detector de puertas. Las 611 originales incluyen referencias visuales, las 32 combinaciones, acciones, modos, movimiento, entrada privada y restauración del control compartido.

La prueba de modo llama a las intenciones reales de salida y cambio, y la de R2 llama a EndArmMode; no simulan eventos de dispositivo. Pausa se comprueba mediante stepping de Editor y guards efectivos, no con un bucle Game completo. Desactivaciones invocan explícitamente los callbacks cuando corresponde para no depender del ciclo de ejecución de componentes sin ExecuteAlways en Editor. Los resultados no sustituyen las comprobaciones manuales.

## Orden, mantenimiento y rendimiento

Input -50, rig 200, puertas 220 y documentos 225 se conservan. Coordinator valida sesión en 215. La prueba comprueba peso cero inmediatamente después de adquirir y peso aplicado tras avanzar el rig. El Grab sigue esperando al siguiente ciclo del esqueleto; también puede quedar la deformación de clic ya aplicada antes de la adquisición durante ese fotograma. Es un desfase previo comprobado estructuralmente, con relevancia visual pendiente de Game. No se aplica dos veces el rig ni se reorganiza la composición.

La mejora principal es un único punto de ocupación/liberación y política de acciones. Añadir otro adaptador no obliga a editar Input para ocupación o cancelación. Desaparecen sus dos dependencias concretas, las solicitudes/salidas visuales duplicadas en adaptadores y el booleano de agarre redundante. Las reglas de objeto siguen separadas de la sesión.

El peso de restauración no se duplica con otro temporizador/estado persistente. Las referencias de componentes se resuelven una vez y la validación por fotograma solo consulta la sesión activa. No se introducen búsquedas continuas ni colecciones de candidatos nuevas. No se afirma una mejora de FPS ni de tiempo CPU: no se ha realizado un benchmark.

## Prueba manual pendiente de fase 2

No hay que añadir componentes, ejecutar Prepare ni recapturar. Abrir la TestArmScene actual y probar:

1. Las nueve puertas: contacto, agarre, apertura manual/automática, bloqueadas y temblor completo, liberación y reintento tras soltar; candidatos solapados y prioridad frente a papel.
2. Los tres documentos: recogida, movimiento libre, tamaño/texto/idioma, pulgar y devolución. Repetir con el detector de puertas deshabilitado.
3. Iniciar PointClick y tocar puerta/documento antes de acabar. Comprobar que no continúa el clic ni se ejecutan acciones nuevas durante Grab/salida; revisar perceptivamente el primer fotograma de transición.
4. Cambiar dedos mientras se sostiene cada objeto, soltar y verificar la combinación vigente y sus rotaciones. Repetir desde OK y Point.
5. R2/RMB, cambio a HandEdit con botón mantenido, pausa/reanudación y Alt-Tab durante agarre/devolución. Comprobar espera a soltar y recuperación de controles/cursor con ratón y mando.
6. Desactivar objeto, adaptador, Input y coordinador durante uso; después reactivar y comprobar ausencia de lease/contacto/UI residual. Si se desactiva el rig, revisar la restauración al reactivarlo.

No se afirma validación visual real de esta fase. Las capturas completas + IK y la corrección del orden final de composición quedan fuera de esta entrega. Fin de fase 2.
