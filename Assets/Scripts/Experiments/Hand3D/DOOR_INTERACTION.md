# Puertas con la mano 3D — TestArmScene

Consultar también [GENERAL_DOORS_GRAB.md](GENERAL_DOORS_GRAB.md) para múltiples puertas y captura ampliada de Grab.

## Contacto corregido: imagen final de Game + alcance del jugador

El ViewModel y el mundo se renderizan mediante cámaras independientes. La coincidencia visible ya no exige que sus meshes ni sus profundidades físicas coincidan.

La detección anterior hacía `Physics.OverlapSphere` alrededor de una posición calculada con la profundidad del ViewModel y exigía menos de 0.06 m hasta el pomo. Una coincidencia perfecta en pantalla a otra profundidad ni siquiera llegaba a TryBeginInteraction. Además, su raycast rechazaba la propia hoja/marco si aparecía antes que el trigger. Ambos rechazos se reprodujeron en pruebas aisladas; el Debug permite distinguir qué filtro falla en una partida concreta.

Ahora se proyectan HandGripPoint con ArmCamera y el InteractionPoint del pomo con MainCamera. La mano pasa por el RawImage REAL que muestra RT_PixelArm: RectTransform, Canvas/CanvasScaler, escalado, desplazamiento, rotación, UV Rect y recorte RectMask2D. La resolución de la RenderTexture (320 × 180) no se confunde con los píxeles de Game. Los FOV y aspectos distintos quedan incluidos en las proyecciones de cada cámara.

Se compara la separación en píxeles de Game con Screen Contact Radius (55 px a 1080p) y también se evalúa el segmento recorrido desde el fotograma anterior para capturar cruces rápidos. De forma independiente se comprueba la distancia de MainCamera al pomo (máximo inicial 1.8 m). Se rechazan puntos detrás de cámara, fuera de su área visible, fuera del alcance o bloqueados por obstáculos ajenos. El raycast va desde la cámara del mundo hasta el pomo, ignora jugador/ViewModel y los colliders de la puerta objetivo. No continúa detrás del pomo. No se utiliza un raycast central para sustituir la mano.

## Configuración en TestArmScene

Las referencias de PixelArmDisplay y de la raíz de la puerta de prueba ya están conectadas en la escena existente. Las capturas de dedos, modos, Grab y orientaciones no se cambian.

En Hand3DDoorInteractor:

| Campo | Uso |
|---|---|
| World Camera | MainCamera del mundo |
| Arm Display | RawImage PixelArmDisplay que muestra RT_PixelArm |
| World Display | Dejar vacío cuando MainCamera dibuja directamente en Game. Solo asignar si también muestra su imagen mediante un RawImage |
| Hand Grip Point | Punto fijo de la palma, hijo directo de la muñeca |
| Door Refresh Interval | Descubrimiento de nuevas puertas sin binding cada 1 s; las preparadas se registran al activarse |
| Hand3DDoorBinding / Door Root | Raíz de cada puerta y su marco, nunca de la habitación o mansión |
| Screen Contact Radius | 55 px de referencia a 1080p; se convierte con Screen.height / 1080 |
| Grip Screen Offset | Ajuste visual del punto percibido de contacto en px de referencia a 1080p; Y positivo hacia arriba |
| Límites anteriores de 18/80 px | Se conservan ocultos por compatibilidad de serialización; ya no deciden adquisición ni cancelación |
| Maximum Reach | 1.8 unidades del mundo desde MainCamera al pomo |
| Grip Snap Duration | 0.08 s inicialmente; transición suave al punto de agarre |
| Interaction Layers | Capas de paredes y obstáculos físicos. La jerarquía de jugador/ViewModel y la puerta objetivo se excluyen aparte |
| Debug Contact | Panel y marcadores en Game |
| Log Interactions | Solo eventos de inicio y apertura automática; no mensajes por fotograma |

Se eliminan World Units Per ViewModel Unit y las tolerancias antiguas en metros entre mano visual y pomo: ya no hay que calibrar profundidades entre cámaras. Si cambias Test Door, revisa Target Door Root; el menú Prepare Door Interaction conecta la raíz del prefab de la puerta seleccionada cuando contiene una sola puerta. Sin raíz configurada, se ignora la propia hoja/bisagra; los marcos fuera de esa jerarquía deben identificarse mediante Target Door Root.

Para preparar otra instancia selecciona armsFBX fuera de Play y usa **GameObject > Metroidbrainia > Test Hand 3D > Prepare Door Interaction**. Conserva referencias existentes, crea los componentes/punto que falten con Undo y selecciona una puerta existente. Los parámetros originales se editan como overrides de ESA instancia, nunca aplicándolos al prefab compartido.

## Debug en Game

Debug Contact está activado en TestArmScene para la próxima prueba. Puedes desactivarlo después en el Inspector.

- Círculo cian: punto visible de HandGripPoint más Grip Screen Offset. Ajusta el offset si el marcador no cae sobre la palma/dedos que percibes como contacto.
- Círculo amarillo pequeño: InteractionPoint del pomo. El círculo grande muestra el radio real de adquisición. La línea cian muestra el recorrido entre los dos últimos fotogramas válidos.
- El panel muestra ambos puntos, distancia actual y distancia mínima al recorrido, radio real, distancia del jugador, R2/RMB físico y modo de brazo efectivo por separado, Exploration/transición, alcance, raycast, collider y capa del obstáculo, máscara de capas, trigger, candidato válido, Grab fijado y motivo de rechazo.
- Mantén R2/RMB para evaluar la interacción. El panel sigue mostrando medidas sin pulsarlo, pero indica que falta el modo de movimiento. En HandEdit informa de que la interacción está desactivada.
- Un RawImage incorrecto, punto recortado, mano detrás de cámara, separación excesiva, puerta lejana o pared interpuesta generan motivos distintos. Si todos los filtros pasan pero el motor original rechaza la captura, se identifica expresamente que TryBeginInteraction devolvió false: revisa su estado/alcance original en esa instancia.

Los marcadores son una superposición de Debug; no cambian RT_PixelArm ni sus materiales. No hay logs continuos en Console.

## Seguimiento, Grab y apertura

Tras aceptar el contacto se proyecta el pomo a Game y se convierte ese punto al RawImage/cámara del brazo en sentido inverso, manteniendo la profundidad que tenía HandGripPoint al iniciar el agarre. La profundidad del pomo no mueve el mesh del ViewModel dentro del mundo. El seguimiento usa la IK y la capa Grab existentes; se mantienen los estados lógicos de dedos y se descuentan los offsets de la palma al resolver el contacto.

Que el mesh de un ViewModel se cruce con una pared en Scene no equivale a contacto físico válido ni es ya un requisito de la detección. Esta corrección no añade un sistema de colisiones del mesh visual con el mundo.

DoorHandInteractable sigue siendo el único motor de bisagras. Conserva selección de sentido que aleja el pomo del jugador, tangente de pantalla, umbral manual de 10°, apertura automática hasta 125°, cierre al cancelar, estado abierto persistente, Unlock, OpenAutomatically y temblor del pomo bloqueado. No se modifica ese script en esta corrección.

Grab sigue siendo una capa temporal independiente. Para ajustarla usa **Preview Grab / Flexed Base**, coloca los dedos alrededor del pomo y pulsa **Capture Grab (preserve logical poses)**. No necesitas recapturar el Grab ya guardado para corregir este contacto.

Una vez adquirido, Grab queda fijado: la distancia visual de la mano ya no se usa para cancelarlo ni se repite la adquisición. Continúan comprobándose soltar/cambiar de modo, validez del pomo, alcance físico y obstáculos. Antes de adquirir se comprueba continuamente la posibilidad mientras R2 siga mantenido; un rechazo inicial no consume la pulsación. Una cancelación transitoria tampoco bloquea todos los reintentos. Solo finalizar la apertura o el intento de puerta bloqueada deja el intento consumido hasta soltar, evitando feedback repetido.

Al superar el umbral se libera Grab y la puerta continúa automáticamente. Soltar antes cancela y el motor original cierra suavemente. Una puerta bloqueada muestra su intento/temblor una sola vez por pulsación mantenida. Cambiar a HandEdit cancela antes de la transición. OK, Point, sus Visual Overrides y las orientaciones especiales se recuperan sin cambiar la combinación lógica.

## Pruebas en Play

1. Mantén R2/RMB y alinea cian/amarillo cerca de una puerta: dentro de tolerancia y 1.8 m debe entrar en Grab sin necesitar igualar las profundidades de los meshes.
2. Desde más de 1.8 m, alinea los mismos puntos: debe rechazar por alcance. Comprueba también el Max Interaction Distance del componente original.
3. Interpón otra pared/objeto sólido: debe rechazar e identificar el collider. La hoja y marco de Target Door Root no deben bloquear por sí solos.
4. Mueve el pomo lentamente durante la fase manual: los puntos deben seguir unidos, sin llevar el brazo a la profundidad real de la puerta. Supera 10°: liberación y apertura automática. Cancela antes: cierre y recuperación de la pose previa.
5. Prueba puerta bloqueada, reintento soltando R2, OK/Point activos y cambio a HandEdit agarrando. Comprueba que no hay Grab residual ni cambios en dedos.
6. Cambia tamaño/aspecto de Game y, en una prueba reversible, escala/desplaza el RawImage o ajusta su UV Rect: los marcadores deben coincidir con la imagen que realmente se dibuja. Un punto recortado o detrás de cualquiera de las cámaras se rechaza.
7. Desactiva Debug Contact al terminar y comprueba la regresión visual del brazo. Prototype y los prefabs compartidos permanecen intactos.


## Regresión de adquisición y límites de validación

Consulta [DOOR_ACQUISITION_VALIDATION.md](DOOR_ACQUISITION_VALIDATION.md) para los ocho escenarios reproducibles, resultados automatizados y las pruebas manuales pendientes en TestArmScene.


