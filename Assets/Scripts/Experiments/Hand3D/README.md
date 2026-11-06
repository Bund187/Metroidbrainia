# Prueba independiente de mano 3D

Uso exclusivo en `TestArmScene`. No requiere cambios en `Prototype`, assets WRAD, cámaras, materiales ni RenderTextures. Los ajustes de movimiento se guardan únicamente en esta escena.

## Montaje

1. Abre `TestArmScene`. Deshabilita únicamente su instancia de `FirstPersonArmController`, que actualmente no tiene `ArmView`. No apliques ese cambio al prefab de PlayerCapsule.
2. Selecciona la instancia de escena `armsFBX`, por encima de los huesos de la mano elegida. Usa `GameObject > Metroidbrainia > Test Hand 3D > Add Experimental Controllers`. Esta operación explícita, con Undo, añade y conecta seis componentes locales; no aplica nada al asset importado.
3. En `Hand3DInputController`, asigna el `PlayerInput` y `StarterAssetsInputs` de PlayerCapsule en esta escena. Mouse Sensitivity = 1 y Gamepad Sensitivity = 1 son los valores iniciales; ambos se ajustan en este componente.
4. En `Hand3DArmMovement`, asigna la cámara que renderiza `RT_PixelArm` (en el archivo actual se llama `ArmCamera`), los tres huesos de la cadena Upper Arm > Forearm > Hand y opcionalmente un Elbow Hint. WRAD contiene `shoulder.r`, `forearm.r` y `wrist.r`: verifica esa jerarquía y usa el mismo lado en todo el montaje. Los huesos `.l` son la alternativa izquierda.
5. No hacen falta componentes ni pivotes adicionales. Conserva la cadena y ajusta los parámetros de movimiento en pantalla descritos abajo. La cámara y la RenderTexture no se modifican.
6. En `Hand3DRig`, asigna opcionalmente el Animator de esta instancia si escribe sus huesos. El rig lo suspende durante la prueba y restaura su estado al deshabilitarse.

## Capturar dedos

En `Hand3DRig > Fingers` hay cinco entradas. En cada una asigna tantos Joints como necesites. Para la mano derecha, por ejemplo, `finger_index1.r`, `finger_index2.r`, `finger_index3.r` corresponden al índice; existen cadenas equivalentes thumb, middle, ring y pinky.

1. Pon manualmente los huesos del dedo en posición extendida, mediante sus Transform en Scene/Inspector.
2. Pulsa **Capture Extended** para ese dedo.
3. Flexiona manualmente sus huesos y pulsa **Capture Flexed**.
4. Usa **Preview Extended / Preview Flexed** para comprobar ambas capturas.
5. Repite con los cinco dedos y termina con **Preview All Extended**. Guarda la escena de prueba.

No se asume ningún eje. Las capturas son rotaciones locales absolutas de cada articulación. Todas las articulaciones deben tener ambas capturas antes de Play. Transition Duration = 0.2 segundos. No añadas el mismo hueso a dos dedos.

## Poses y ajustes especiales

`Hand3DPoseRecognizer` incluye:

- OK: Thumb + Index flexionados, Action = None.
- Point: Thumb + Middle + Ring + Little flexionados, Action = PointClick.

Las otras 30 combinaciones no tienen reconocimiento especial. Para otra pose, añade una entrada con nombre, máscara y acción. EventOnly ejecuta su UnityEvent On Action, sin modificar el controlador de entrada ni el reconocedor.

Para corregir visualmente OK:

1. En el Inspector del reconocedor, pulsa **Preview Base Finger Combination** para OK.
2. Las articulaciones configuradas en el rig se detectan automáticamente al capturar si difieren de la combinación base (más de 0.05 grados). Para otros huesos, añádelos explícitamente a `Visual Overrides`.
3. Ajusta esos huesos manualmente hasta aproximar las puntas.
4. Pulsa **Capture Visual Overrides**. Comprueba el número de entradas y el mensaje `[Hand3D Capture]`. Una captura vacía genera una advertencia. Las entradas existentes se vuelven a capturar, por lo que puedes seguir editándolas.
5. Guarda la escena, vuelve a abrirla y usa **Preview Base Finger Combination** seguido de **Preview Captured Visual Overrides** para comprobar que las rotaciones persisten. El rig interpolará hacia ellas al reconocer OK y volverá a los estados individuales actuales al abandonarla, incluso si cambias de combinación durante una transición.

Para diagnosticar Play, activa `Log Pose Changes` en el reconocedor y `Log Visual Transitions` en el rig. `[Hand3D Recognition]` muestra la máscara lógica y la pose; `[Hand3D Application]` muestra la pose aplicada, el número de overrides y la duración. OK requiere exactamente Thumb + Index: si otro dedo sigue flexionado, no coincide. Los logs solo aparecen al cambiar estados, no en cada fotograma. El rig realiza la escritura final de los dedos en LateUpdate, incluyendo la capa de clic; el estado y el reconocedor no escriben Transform. Se rechazan huesos duplicados y articulaciones compartidas con el movimiento del brazo.

Regresión manual: entra en OK con E/R desde extendidos; pulsa F para salir y F otra vez para volver desde otra combinación; alterna R durante la interpolación. Comprueba entrada y salida suaves, sin residuos. Después extiende todo y activa Point con E/F/T/G: verifica su clic con LMB o LT y el movimiento habitual del brazo. Repite la captura y reconocimiento con una pose nueva para comprobar que no depende del nombre OK.

Point admite la misma configuración opcional. No incluyas Upper Arm/Forearm en los overrides: los controla el movimiento. Si ajustas la muñeca, deja su posición de reposo correcta antes de Play. Ninguna previsualización cambia los estados lógicos.

## Controles

| Función | Gamepad | Teclado / ratón |
|---|---|---|
| Pulgar | R3 | E |
| Índice | D-Pad Up | R |
| Corazón | D-Pad Right | T |
| Anular | D-Pad Down | F |
| Meñique | D-Pad Left | G |
| Mover mano en pantalla | RT mantenido + Right Stick | RMB mantenido + Mouse Delta |
| Acción | LT, sin requerir RT | Pulsación de LMB |

Cada pulsación alterna el estado de un dedo. Las acciones y los dedos funcionan sin modo de movimiento. RMB no ejecuta acciones. Soltar RMB/RT inicia el regreso automático. Se eliminan la orientación por WASD/Left Stick, la profundidad manual y el reset por Tab/L3.

El mapa `TestHand3D` es local y se crea en runtime. El PlayerInput utiliza una copia privada de las acciones. Mientras controlas el brazo se bloquean Look/Move/Jump/Sprint; al soltar se restauran sus estados previos. No se editan InputActions compartidas. LT no activa Sprint en esta instancia.

## Movimiento 3D

La referencia es `ArmView` y `FirstPersonArmController` del sistema 2D, que se conservan sin modificaciones: objetivo acumulado en pantalla, límites y retorno con SmoothDamp. La mano ahora asciende al mover arriba, desciende al mover abajo y combina ambos ejes en diagonal. El objetivo se proyecta mediante la cámara del brazo a la profundidad inicial de la muñeca.

La IK analítica de dos segmentos adapta el hombro y el codo al objetivo. Solo gira articulaciones: no cambia escalas, longitudes ni offsets del esqueleto para extenderlo. Un objetivo más lejano que el alcance real se limita a la distancia físicamente alcanzable. Por eso puede no alcanzar todas las esquinas del recorrido 2D; ajusta los límites de pantalla a la cobertura real del modelo. No hay control manual de profundidad ni estiramiento artificial.

La orientación inicial de la mano se conserva respecto a la cámara mediante una compensación calculada por el movimiento y compuesta por `Hand3DRig` al escribir la muñeca. El rig sigue siendo el único escritor de la rotación final de muñeca y dedos. Las transiciones se calculan en el espacio de pose, antes de esa compensación; un Visual Override de muñeca se combina con ella, sin acumulación. Ningún retorno cambia estados de dedos, reconocimiento, overrides ni acciones. Las configuraciones ya capturadas se conservan.

Al soltar, el objetivo vuelve suavemente al reposo, incluso si la postura inicial está fuera de los límites interactivos. Al terminar se recuperan exactamente las rotaciones y posiciones locales iniciales de la cadena. Si vuelves a mantener RMB/RT durante el retorno, continúas desde la posición actual.

## Inspector

Conserva las referencias existentes de `Hand3DArmMovement`: Arm Camera, Upper Arm, Forearm, Hand y el Elbow Hint opcional. No reasignes ni vuelvas a capturar los dedos. La posición de reposo se toma de la instancia al iniciar Play: para cambiarla, edita la postura inicial del brazo en la escena.

| Componente / parámetro | Valor inicial | Efecto |
|---|---|---|
| Input / Mouse Sensitivity | 1 | Unidades de referencia por píxel de ratón; Mouse Delta no se multiplica por deltaTime |
| Input / Gamepad Sensitivity | 1 | Multiplicador del stick derecho, integrado con deltaTime |
| Movement / Movement Speed | 900 | Unidades de referencia por segundo a máxima entrada del stick |
| Movement / Reference Resolution | 1920 × 1080 | Referencia de control, independiente de RT_PixelArm |
| Movement / Movement Smoothing | 0.08 s | Suavizado del seguimiento; cero es inmediato |
| Movement / Horizontal Screen Limits | 0.06–0.94 | Límites normalizados de pantalla para la muñeca |
| Movement / Vertical Screen Limits | 0.06–0.94 | Límites normalizados de pantalla para la muñeca |
| Movement / Return Smooth Time | 0.18 s | Tiempo de suavizado del regreso; menor significa más rápido |

Los límites se aplican al objetivo de la muñeca. Deja margen para el volumen de los dedos. El alcance está determinado por las longitudes reales de Upper Arm–Forearm–Hand, sin parámetro de estiramiento. El Elbow Hint permite ajustar el lado hacia el que se dobla el codo sin modificar el modelo original.

## Verificación en Play

1. Mantén RT/RMB y recorre horizontal, vertical y diagonalmente la pantalla. Arriba debe subir la mano, sin controlar directamente la profundidad. Comprueba flexión natural, límites y ausencia de cambios de escala/longitud.
2. Prueba los extremos con ratón y gamepad. Al invertir la dirección desde el límite, la mano debe responder sin recorrer un objetivo invisible.
3. Suelta RT/RMB: debe regresar suavemente a la postura inicial. Vuelve a activar durante el regreso: debe continuar desde la posición visible, sin saltos.
4. Activa OK con E/R desde extendidos, mueve la mano y suelta: debe conservar OK y sus overrides durante todo el recorrido y retorno.
5. Activa Point con E/T/F/G desde extendidos, lleva la mano a una esquina y pulsa LT/LMB: debe ejecutar el clic. Repite sin mantener RT/RMB; RMB por sí solo no ejecuta acciones.
6. Alterna dedos mientras mueves y durante el regreso. Comprueba transiciones normales, cambio de gesto, clics y una futura pose con overrides de muñeca si la utilizas.
7. WASD/Left Stick ya no orientan el brazo; Tab/L3 ya no lo restablecen. En modo de brazo, las entradas bloqueadas no deben mover personaje ni cámara; fuera del modo recuperan su funcionamiento normal.
8. Pausa y reanuda, cambia el foco y prueba varias tasas de fotogramas. El stick conserva velocidad por segundo; el ratón conserva recorrido por píxel.
9. Comprueba PixelArmCamera/ArmCamera y RT_PixelArm. Verifica aparte `Prototype` y las interacciones existentes: no se ha modificado ninguno de sus scripts ni assets.

Las herramientas y componentes siguen limitados a TestArmScene. No se añaden paquetes, componentes, pivotes ni sistemas paralelos.

