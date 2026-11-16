# Prueba independiente de mano 3D

Uso exclusivo en `TestArmScene`. No requiere cambios en `Prototype`, assets WRAD, cámaras, materiales ni RenderTextures. Los ajustes de movimiento se guardan únicamente en esta escena.

## Montaje

1. Abre `TestArmScene`. Deshabilita únicamente su instancia de `FirstPersonArmController`, que actualmente no tiene `ArmView`. No apliques ese cambio al prefab de PlayerCapsule.
2. Selecciona la instancia de escena `armsFBX`, por encima de los huesos de la mano elegida. Usa `GameObject > Metroidbrainia > Test Hand 3D > Add Experimental Controllers`. Esta operación explícita, con Undo, añade y conecta los componentes experimentales locales; no aplica nada al asset importado.
3. En `Hand3DInputController`, asigna el `PlayerInput` y `StarterAssetsInputs` de PlayerCapsule en esta escena. Mouse Sensitivity = 1 y Gamepad Sensitivity = 1 son los valores iniciales; ambos se ajustan en este componente.
4. En `Hand3DArmMovement`, asigna la cámara que renderiza `RT_PixelArm` (en el archivo actual se llama `ArmCamera`), los tres huesos de la cadena Upper Arm > Forearm > Hand y opcionalmente un Elbow Hint. WRAD contiene `shoulder.r`, `forearm.r` y `wrist.r`: verifica esa jerarquía y usa el mismo lado en todo el montaje. Los huesos `.l` son la alternativa izquierda.
5. Añade Hand3DModeController mediante el botón del Inspector de Hand3DArmMovement. Conserva la cadena de huesos; no hace falta reparentarla ni añadir pivotes. Sigue la configuración de modos descrita abajo. La cámara y la RenderTexture no se modifican.
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

## Modos y configuración visual

En la instancia `armsFBX` de TestArmScene, pulsa **Add Mode Controller (preserve finger data)** en `Hand3DArmMovement`. El botón añade `Hand3DModeController` con Undo y captura la exploración actual. También puedes reutilizar el menú **Add Experimental Controllers**, que conserva componentes y datos existentes. Si falta el componente al iniciar Play se crea uno temporal, pero avisa de las capturas pendientes: para conservar configuraciones, añádelo fuera de Play y guarda la escena.

`Exploration` es el modo inicial. `RB` o cualquiera de las teclas Shift alternan con `HandEdit` una sola vez por pulsación. El Inspector muestra el modo actual durante Play. Transition Duration controla la duración del cambio; alternar a mitad de transición empieza desde la postura mostrada.

### Capturar las referencias visuales

1. Fuera de Play y con dedos neutros, coloca la instancia y sus articulaciones para reproducir la imagen 1: brazo desde abajo a la derecha, mano hacia centro-izquierda y palma hacia la izquierda. Pulsa **Capture Exploration**. Se guardan posición/rotación del modelo respecto a ArmCamera, rotaciones locales de hombro/antebrazo y orientación de muñeca respecto a cámara. No se capturan dedos ni se asumen valores cero.
2. Coloca el brazo para la imagen 2: antebrazo elevado, palma hacia la cámara y mano en una posición frontal estable. Pulsa **Capture HandEdit / Right Limit**. Esta postura es el extremo derecho, ángulo cero, no el centro del recorrido. No cambies escalas ni longitudes para lograrla.
3. Pulsa **Preview HandEdit / Right Limit**, gira solo el antebrazo sobre el eje longitudinal que une codo y muñeca hasta mostrar el dorso (imagen 3) y pulsa **Capture Left Inspection Limit**. La captura calcula el ángulo y el sentido usando el eje real del hueso; avisa si detecta flexión lateral en lugar de un giro longitudinal. Usa ambas previsualizaciones para comprobar los extremos. El valor inicial de -180 grados es orientativo hasta calibrarlo con este paso.
4. Pulsa **Preview Exploration**, comprueba los dedos y guarda TestArmScene. Estas herramientas respetan Undo y marcan la escena como modificada; no cambian el FBX ni aplican overrides al asset importado.

Los campos de ambas configuraciones son editables en el Inspector. Las posiciones se expresan en el espacio de ArmCamera y las rotaciones de huesos en sus espacios locales reales. Por eso permanecen relativas a pantalla al caminar o girar. El seguimiento de cámara no se suaviza; solo se suaviza el movimiento voluntario y las transiciones.

### Orientaciones especiales de OK y Point

Estas capturas son independientes de **Capture Visual Overrides** y no reemplazan su contenido:

1. Pulsa **Preview Exploration** en el controlador de modos.
2. Previsualiza la combinación y los overrides ya guardados de OK en el reconocedor.
3. Gira únicamente la muñeca/mano para que la O mire al jugador, como en la imagen 4.
4. En la entrada OK pulsa **Capture Exploration Wrist (separate from finger overrides)**. Esto activa Use Exploration Wrist y guarda un offset respecto a la muñeca neutra de Exploration.
5. Repite desde Preview Exploration para Point: previsualiza sus dedos, alinea la mano con el antebrazo y orienta el dorso/índice como en la imagen 5. Captura en la entrada Point.
6. Usa **Preview Exploration Wrist**, ajusta Wrist Transition Duration / Wrist Transition Easing y guarda. Para desactivar una orientación, desmarca Use Exploration Wrist. Las poses nuevas admiten los mismos campos y herramientas.

No se incluyen rotaciones universales para OK/Point ni se cambian sus capturas anteriores: calibra las nuevas orientaciones sobre el modelo de tu escena. Captura siempre Exploration antes de las orientaciones de poses; si cambias después la orientación neutra, revisa esos offsets.

En HandEdit las orientaciones adicionales de Exploration se desvanecen hacia identidad. Los overrides de dedos siguen funcionando. Al salir se recupera automáticamente el offset de la pose actual. Si una pose deja de reconocerse, vuelve suavemente a identidad, usando la duración anterior. El clic de Point sigue componiéndose sobre el índice.

## Controles

| Función | Gamepad | Teclado / ratón |
|---|---|---|
| Alternar Exploration / HandEdit | RB | Shift |
| Pulgar | R3 | E |
| Índice | D-Pad Up | R |
| Corazón | D-Pad Right | T |
| Anular | D-Pad Down | F |
| Meñique | D-Pad Left | G |
| Movimiento en Exploration | R2 + Right Stick | RMB + Mouse Delta |
| Giro longitudinal en HandEdit | Left Stick horizontal | A / D |
| Acción en ambos modos | L2 | LMB |

Mantener RB/Shift no repite cambios. No hay giro vertical W/S. R2/RMB no mueve el brazo en HandEdit. Tras un cambio con R2/RMB mantenido debes soltarlo antes de iniciar un nuevo movimiento libre. Los dedos y acciones no requieren modo de movimiento. RMB no ejecuta acciones. Soltar R2/RMB en Exploration inicia el retorno suave, sin cambiar la combinación de dedos.

Shift queda reservado al cambio de modo: se elimina su binding de Sprint solo en la copia privada de acciones del experimento (también se conserva la exclusión de L2/Sprint). No se modifican los assets de Input System compartidos.

## Responsabilidad de cada transformación

| Transform / capa | Responsable | Función |
|---|---|---|
| Instancia armsFBX | Hand3DArmMovement | Sigue a ArmCamera con el offset capturado; transición entre configuraciones |
| Upper Arm y Forearm | Hand3DArmMovement | Postura de modo, IK de pantalla y giro longitudinal alrededor de codo-muñeca |
| Wrist / Hand | Hand3DRig | Una única escritura final: orientación del modo, offset de pose y override visual de muñeca si existe |
| Huesos de dedos | Hand3DRig | Extended/Flexed, Visual Overrides y clic de Point |
| Hand3DModeController | Sin escrituras de Transform en runtime | Estado, configuraciones capturadas, transición y límites de inspección |

El movimiento se ejecuta después de Starter Assets/Cinemachine (orden 100); el rig compone después la muñeca/dedos (orden 200). Los giros no se acumulan sobre el resultado anterior. La inspección usa el eje real entre antebrazo y muñeca; por eso la muñeca permanece en el mismo punto al girar. No se reparenta ni escala el modelo.

## Bloqueo del jugador

El experimento mantiene su mapa de entrada independiente y una copia privada de PlayerInput.actions. En HandEdit deshabilita Look, Move, Jump y Sprint de esa copia, limpia StarterAssetsInputs y suspende temporalmente el FirstPersonController de la instancia para evitar movimiento residual y gravedad durante la inspección. Guarda y restaura su estado enabled anterior. No desactiva PlayerInput ni las acciones de mano.

En Exploration, fuera del movimiento de brazo, restaura los controles anteriores. Durante R2/RMB bloquea las mismas acciones compartidas para que ratón/sticks no muevan simultáneamente cámara o jugador. El bloqueo se conserva al pasar de movimiento a edición y se libera al salir. Perder foco o pausar no reinicia dedos; los tiempos de transición no avanzan con deltaTime cero. No se modifican scripts de Starter Assets.

## Parámetros útiles

- **Exploration / HandEdit**: configuraciones completas capturables, sin coordenadas universales.
- **Transition Duration**: cambio entre modos.
- **Minimum Inspection Angle**: extremo izquierdo; **Maximum Inspection Angle** permanece en cero porque el inicio es el extremo derecho. **Inspection Axis Sign** lo calcula la captura del límite.
- **Inspection Speed / Smoothing**: grados por segundo y suavizado del giro longitudinal.
- **Movement Speed**: 900 unidades de referencia/s por defecto para el stick.
- **Movement Smoothing**: 0.08 s por defecto; cero es inmediato.
- **Reference Resolution**: 1920 × 1080, independiente de RT_PixelArm.
- **Horizontal / Vertical Screen Limits**: límites normalizados de la muñeca (0.06–0.94 por defecto); reserva margen para los dedos.
- **Return Smooth Time**: 0.18 s por defecto; menor es más rápido.
- **Mouse / Gamepad Sensitivity**: en Hand3DInputController. El stick se integra con deltaTime; Mouse Delta ya es desplazamiento.
- **Use Exploration Wrist / Exploration Wrist Euler / Wrist Transition Duration / Easing**: por pose, independientes de las capturas de dedos.

La IK tiene longitudes fijas: puede no alcanzar todas las esquinas de pantalla. Ajusta límites y postura inicial al alcance real; no hay estiramiento artificial. La orientación exacta de las cinco imágenes requiere calibración con las herramientas anteriores.

## Pruebas en Unity

1. Camina y gira en Exploration: el brazo conserva su posición relativa a la cámara sin vibrar. Prueba R2/RMB, diagonales, límites y retorno suave.
2. Entra en HandEdit con el brazo extendido y con dedos flexionados. La cámara y el personaje deben quedar inmóviles; no debe saltar ni correr. Mantén RB/Shift para comprobar que solo alterna una vez.
3. Comprueba palma en el extremo derecho inicial; A/Left Stick izquierdo muestran lateral y dorso sin mover la muñeca. D vuelve al límite inicial y no lo sobrepasa. W/S no orientan la mano y R2/RMB no la desplaza.
4. Alterna los cinco dedos y prueba OK/Point en ambos modos. Los overrides del círculo y el clic con L2/LMB deben conservarse durante giros y transiciones.
5. Sal de HandEdit con OK activo: la configuración Exploration y la orientación especial de OK deben reaparecer suavemente, sin reiniciar dedos. Repite con Point y una combinación desconocida.
6. Cambia de pose durante movimiento/transición y ejecuta clics mientras gira la muñeca. Alterna RB/Shift rápidamente: sin saltos ni deriva.
7. Cambia de modo con R2/RMB mantenido: no debe activar movimiento libre al salir hasta soltar y volver a pulsar. Comprueba retorno al soltar en Exploration.
8. Pausa/reanuda durante transición, pierde/recupera foco y comprueba restauración de controles al deshabilitar el experimento. No debe avanzar la transición durante la pausa.
9. Guarda, vuelve a abrir y verifica configuraciones, extremos, orientaciones de pose y capturas anteriores. Comprueba renderizado pixelado y verifica aparte Prototype, sin editarlo.

## Puertas con mano 3D

Configuración, calibración física, captura de Grab y pruebas: [DOOR_INTERACTION.md](DOOR_INTERACTION.md).

