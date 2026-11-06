# Prueba independiente de mano 3D

Uso exclusivo en `TestArmScene`. No requiere cambios en `Prototype`, assets WRAD, cámaras, materiales ni RenderTextures. No se ha editado ninguna escena automáticamente.

## Montaje

1. Abre `TestArmScene`. Deshabilita únicamente su instancia de `FirstPersonArmController`, que actualmente no tiene `ArmView`. No apliques ese cambio al prefab de PlayerCapsule.
2. Selecciona la instancia de escena `armsFBX`, por encima de los huesos de la mano elegida. Usa `GameObject > Metroidbrainia > Test Hand 3D > Add Experimental Controllers`. Esta operación explícita, con Undo, añade y conecta seis componentes locales; no aplica nada al asset importado.
3. En `Hand3DInputController`, asigna el `PlayerInput` y `StarterAssetsInputs` de PlayerCapsule en esta escena. Mouse Sensitivity = 0.003 y Gamepad Sensitivity = 1 son los valores iniciales; ambos se ajustan en este componente.
4. En `Hand3DArmMovement`, asigna la cámara que renderiza `RT_PixelArm` (en el archivo actual se llama `ArmCamera`), los tres huesos de la cadena Upper Arm > Forearm > Hand y opcionalmente un Elbow Hint. WRAD contiene `shoulder.r`, `forearm.r` y `wrist.r`: verifica esa jerarquía y usa el mismo lado en todo el montaje. Los huesos `.l` son la alternativa izquierda.
5. No hacen falta componentes ni pivotes adicionales. Se utiliza el antebrazo existente como pivote en el codo. Ajusta los parámetros de traslación, orientación y reset descritos abajo. La cámara y la RenderTexture no se modifican.
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
| Mover brazo | RT mantenido + Right Stick | RMB mantenido + movimiento del ratón |
| Orientar antebrazo y mano | RT mantenido + Left Stick | RMB mantenido + WASD |
| Restablecer brazo | L3, sin requerir RT | Tab, sin requerir RMB |
| Acción | LT, sin requerir RT | Pulsación de LMB |

RMB solamente activa el modo de movimiento; no ejecuta acciones. LMB/LT ejecutan una acción por pulsación. R3 es independiente de los ejes del stick. Los dedos y las acciones funcionan sin ArmMode. Soltar RMB/RT conserva la postura visible; Tab/L3 es el único retorno explícito. Una entrada de movimiento u orientación nueva cancela un reset en curso; soltar el modo no lo cancela.

`Hand3DPoseActions`: Point Click Duration = 0.18; Point Click Amplitude = 0.18. La amplitud es una mezcla hacia la captura Flexed del índice, no grados en un eje fijo. Mientras un clic está activo se ignoran otros; cambiar de pose lo cancela. OK y las combinaciones no reconocidas no ejecutan acciones por defecto.

El mapa de acciones `TestHand3D` se crea y elimina en runtime. El PlayerInput utiliza una copia privada de sus acciones durante la prueba; LT no activa Sprint, pero Shift se conserva fuera de ArmMode. Mientras controlas el brazo se bloquean Look/Move/Jump/Sprint y al soltar se restauran sus estados previos. No se edita ningún archivo InputActions.

## Movimiento 3D

Se amplía el solver de dos segmentos de `Hand3DArmMovement`. La traslación se expresa en los ejes derecha/profundidad de la cámara del brazo, tomando como origen la posición inicial real de la muñeca. Arriba extiende, abajo retrae, sin elevar verticalmente el objetivo. Izquierda y derecha añaden extensión; en diagonales inferiores se prioriza la retracción. El antebrazo rota alrededor del codo después de resolver la traslación, de modo que la muñeca describe un arco natural. Los ejes de cámara se convierten al espacio local real del padre del antebrazo: no se asumen los ejes del FBX. Derecha gira hacia la derecha; arriba inclina hacia abajo. No se escribe la rotación local de la muñeca ni de los dedos, conservando overrides y clics.

### Parámetros del Inspector

En `Hand3DInputController`:

- **Mouse Sensitivity** (0.003): entrada integrada por píxel. Mouse Delta ya es desplazamiento; no se multiplica por deltaTime.
- **Gamepad Sensitivity** (1): multiplicador del stick. Ambos sticks se integran con deltaTime; WASD usa las velocidades de rotación con deltaTime, independientemente de la sensibilidad del gamepad.

En `Hand3DArmMovement`:

| Parámetro | Valor inicial | Efecto |
|---|---|---|
| Horizontal Movement Speed | 0.5 | Velocidad lateral en longitudes iniciales del brazo por segundo de entrada |
| Extension / Retraction Speed | 0.5 / 0.5 | Velocidades independientes en profundidad |
| Horizontal Extension Contribution | 0.25 | Extensión adicional por unidad de recorrido lateral |
| Minimum / Maximum Extension | -0.3 / 0.5 | Offsets de profundidad respecto al inicio, en longitudes de brazo |
| Horizontal Movement Limits | -0.6 / 0.6 | Offsets laterales respecto al inicio, en longitudes de brazo |
| Movement Smoothing | 0.08 s | Constante de suavizado exponencial; cero es inmediato |
| Maximum Stretch | 1.1 | Longitud máxima de cada segmento respecto al original; 1 evita estiramiento |
| Viewport Padding | 0.06 | Margen de seguridad de la muñeca en pantalla; reserva más margen para los dedos si hace falta |
| Horizontal / Vertical Rotation Speed | 100 / 80 grados/s | Velocidad de orientación |
| Minimum / Maximum Horizontal Angle | -100 / 100 grados | Límites respecto a la orientación resuelta sin giro adicional |
| Minimum / Maximum Vertical Angle | -75 / 75 grados | Límites de inclinación |
| Rotation Smoothing | 0.08 s | Suavizado de los ángulos; cero es inmediato |
| Reset Duration | 0.4 s | Duración del retorno exacto a los valores capturados al iniciar Play |
| Reset Easing | Ease In Out | Curva normalizada del retorno; conserva extremos 0 y 1 y una progresión suave |

El solver limita el alcance y el estiramiento. También comprueba el resultado final tras orientar el antebrazo; si la muñeca alcanza el margen o los planos de cámara, limita el avance conjunto de traslación y orientación. Por ello, cerca de los bordes puede limitarse el giro antes de alcanzar el ángulo configurado. El margen incluye la postura inicial para permitir recuperarla exactamente. No garantiza que todos los dedos permanezcan dentro de pantalla: aumenta Viewport Padding según el tamaño visual de la mano. No se cambia ninguna cámara, render target, material ni escala de los dedos.

El reset guarda y restaura las rotaciones y offsets locales originales, aunque sean distintos de cero/identidad. No cambia `Hand3DFingerState`, el reconocedor, los overrides ni las acciones. Al deshabilitar el componente o salir de Play se restaura también la cadena original.

## Verificación en Play

- Al comenzar, cinco dedos extendidos.
- Cada pulsación alterna un dedo; mantener la tecla/botón no repite.
- E + R desde extendidos reconoce OK inmediatamente, aunque la animación siga interpolando.
- E + F + T + G desde extendidos reconoce Point.
- Point + LT sin RT produce un clic y recupera exactamente su postura.
- RT + stick mueve el brazo; R3 sigue alternando el pulgar mientras desplazas el stick.
- RMB permite desplazarlo con el ratón y no ejecuta acciones. LMB ejecuta el clic de Point, incluso sin RMB.
- Prueba izquierda/derecha/arriba/abajo y las cuatro diagonales con ratón y stick: lateral extiende; abajo y diagonales inferiores retraen. Mantén las entradas hasta los límites y comprueba que no hay estiramiento ilimitado.
- Con RT/RMB mantenido, Left Stick/WASD orientan el antebrazo: W/arriba inclinan hacia abajo. Prueba diagonales, palma, dorso y laterales, verificando que el personaje y la cámara no se mueven.
- Suelta RT/RMB: conserva posición y orientación, sin deriva ni retorno. Fuera del modo, WASD/sticks y ratón recuperan el control habitual del jugador.
- Con OK activo, pulsa Tab/L3 tanto dentro como fuera del modo: vuelve suavemente a la postura original y sigue en OK. Suelta RT/RMB durante el reset: debe completarse. Prueba también reset con Point y durante un clic.
- Prueba reset con una postura inicial no nula, sensibilidades distintas, suavizados en cero y una sesión a 30/60/144 fps: el stick debe conservar la velocidad por segundo y el ratón el recorrido por píxel.
- Cambia de combinación durante un clic y repite acciones: no debe quedar rotación residual.
- Comprueba la imagen con PixelArmCamera/ArmCamera y RT_PixelArm sin cambiar su configuración.
- Sal de Play, abre `Prototype` y comprueba su brazo 2D: el experimento no añade componentes allí ni modifica sus scripts o assets.

Las herramientas de captura y montaje rechazan escenas distintas de TestArmScene y assets persistentes. Los componentes que escriben huesos y el controlador de entrada también comprueban la escena en runtime.

