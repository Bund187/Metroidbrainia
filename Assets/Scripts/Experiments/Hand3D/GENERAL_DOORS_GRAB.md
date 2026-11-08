# Grab ampliado y múltiples puertas — TestArmScene

## Puertas

Se conserva la proyección de cámaras/RawImage, radio visual de 55 px a 1080p, barrido entre fotogramas, alcance y obstáculos. Test Door queda oculto por compatibilidad serializada y ya no filtra candidatas.

Hand3DDoorBinding registra instancias activas de la misma escena. El interactor descubre motores sin binding al inicializar, al comenzar a mantener el control y periódicamente (Door Refresh Interval, 1 s). No busca globalmente puertas cada Update. Las puertas preparadas se registran inmediatamente al activarse; desactivar/eliminar las excluye.

Se descartan puertas fuera de pantalla, alcance o visibilidad. Se prioriza contacto actual sobre barrido, menor separación visual y, en empate, distancia física. Si el motor original rechaza comenzar por sus reglas particulares se prueba la siguiente candidata. Una vez adquirido Grab, la puerta queda fija hasta terminar o cancelar.

DoorHandInteractable conserva bisagra, sentido, límites, bloqueo, cancelación y apertura automática. Grip Local Offset modifica solo el punto visual relativo a InteractionPoint; Grab Wrist Euler Offset añade orientación por pomo. Las comprobaciones físicas y la geometría de apertura conservan InteractionPoint.

TestArmScene tiene nueve bindings preparados: FrontDoor, Right, Left, ClosedDoor, BellDoorEntrance, Kitchen, Dormitory, Studio y Bathroom. ClosedDoor conserva su bloqueo; sus motores y puntos existentes no se modificaron.

Para nuevas instancias: **GameObject > Metroidbrainia > Test Hand 3D > Prepare Scene Doors**, fuera de Play. Usa Undo, evita duplicados y solo actúa en TestArmScene. Repara estructuras conocidas únicamente cuando es seguro e informa de las incompletas. No aplica cambios a assets prefab. Revisar Door, Door Root (solo esa puerta y marco), InteractionPoint y las reglas del motor. Para puertas creadas en runtime preparar el binding en una variante independiente, especialmente su Door Root; sin binding también se descubren periódicamente, usando la bisagra como raíz por defecto.

## Captura ampliada de Grab

En el WRAD real, forearm.r tiene como hijos wrist.r y forearm.Twist0.r; Twist1 cuelga de Twist0. El antebrazo principal pertenece al IK y no debe incluirse en Additional Grab Bones. El editor también rechaza huesos ajenos y dedos duplicados. Elegir wrist.r y, si hace falta, Twist0/Twist1; no es obligatorio usarlos todos.

1. Fuera de Play, con la postura neutra correcta, añadir esos Transform a **Additional Grab Bones** de Hand3DInteractionPose.
2. Pulsar **Capture Additional Neutral (uncaptured bones only)** ANTES de rotarlos. Guarda rotaciones locales y no reemplaza neutrales ya capturadas. **Preview Additional Neutral** las recupera.
3. Pulsar **Preview Grab / Flexed Base** y preparar dedos y articulaciones adicionales.
4. Pulsar **Capture Grab**: serializa dedos y huesos adicionales, conservando datos de OK/Point.
5. Recuperar la previsualización neutra/de exploración, guardar la escena y entrar en Play. No aplicar cambios al FBX.

La lista adicional comienza vacía para conservar Grab existente. Cambiar el Transform de una entrada exige recapturar neutral y Grab. Los Euler son editables en Inspector. Transition Duration controla la transición suave, incluido el offset del pomo.

Hand3DArmMovement conserva el IK. Hand3DRig escribe la rotación final de muñeca, dedos y twists: pose lógica y Visual Overrides, después Grab temporal con prioridad y offset de puerta. La muñeca se compone con la referencia del movimiento y los twists recuperan la neutral capturada. La corrección de contacto ocurre tras componer la pose. Al soltar se recupera la combinación lógica ACTUAL, sus overrides y orientación Exploration. No se cambian estados de dedos ni se capturan posiciones globales.

## Depuración y pruebas

**Debug Contact** conserva círculos/radio, distancias, alcance, entrada, visibilidad y rechazo; añade cantidad de puertas, candidata y puerta agarrada. Log Interactions registra eventos, no cada frame.

Validación aislada en Unity 6000.6.3f1: 126 comprobaciones superadas con entrada simulada, incluyendo adquisición, selección múltiple, altas/bajas dinámicas, bisagras, bloqueo, captura mediante el editor real, restauración y guardado/reapertura de Grab. Una copia de TestArmScene reabierta resuelve los nueve bindings; repetir preparación añade cero componentes. Compilación de scripts comprobada: cero errores. Esto NO equivale a validar interacción real en Game con el rig y controles completos.

Pruebas manuales pendientes:

- Puerta original y otra sin asignar controlador; dos pomos cercanos y objetivo estable durante Grab.
- Bisagras izquierda/derecha, sentidos y ClosedDoor bloqueada.
- Rechazo por distancia y pared ajena; permitir la propia hoja/marco.
- R2/RMB mantenido antes del contacto y cruce rápido de la zona.
- Captura de muñeca/twists, guardar y Play: transición, deformación y contacto visual.
- Desde OK y Point, cancelar antes del umbral y completar hasta apertura automática: recuperar círculo, orientación y clic.
- HandEdit durante Grab: cancelación segura, modos y dedos persistentes.
- Añadir, activar, desactivar y destruir puertas en runtime: contador y candidatas correctos.

Prototype, prefabs compartidos, DoorHandInteractable original, controles y movimiento experimental no se modifican con esta ampliación.
