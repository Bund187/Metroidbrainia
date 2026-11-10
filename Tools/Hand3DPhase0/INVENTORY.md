# Inventario de referencia de TestArmScene

Referencia capturada el 9 de noviembre de 2026. Fuente: escena guardada y carga resuelta en Unity 6000.6.3f1, sobre una copia aislada. Los valores de esta lista son los de escena, no los defaults de las clases.

## Identidad y conservación

- Escena original SHA-256: `E833D177AB203D17213F2155FD50A9E3C9BFA981D63E3491A99854EA6D155490`.
- Prototype SHA-256: `ED18AB1286FEA727940959DB53624BE1F55753F7E926D3DB09102E3F226496AB`.
- Se verifican 1.981 archivos originales. Ninguno se restaura, modifica ni elimina mediante estas herramientas.
- Propietario de los controladores: `PlayerCapsule/Capsule/armsFBX`.
- `Reference/phase0-inventory.json` conserva serialización completa y una tabla de referencias por propertyPath con GlobalObjectId y ruta. Los instanceID del JSON de Editor pueden ser cero o cambiar entre cargas; no usarlos como identidad persistente.
- `Reference/TestArmScene.unity.txt` conserva los fileID/GUID originales. Los hashes identifican también modelos, metas, materiales, fuentes, prefabs, scripts y configuración originales.

## Componentes

| Componente | Referencias / datos conservados |
|---|---|
| Hand3DInputController | PlayerInput y StarterAssetsInputs de esta escena, estado, rig, movimiento, acciones; sensibilidades 1/1. |
| Hand3DFingerState | Cinco bits lógicos; el estado actual de Play no se serializa como captura. |
| Hand3DPoseRecognizer | OK y Point, combinaciones, acciones, eventos, overrides, orientaciones y curvas. |
| Hand3DPoseActions | Reconocedor; clic 0.18 s, amplitud 0.18. |
| Hand3DModeController | Exploration y HandEdit capturados, transición 0.35 s, inspección. |
| Hand3DArmMovement | ArmCamera, bicep.r, forearm.r, wrist.r; Elbow Hint vacío. |
| Hand3DRig | 5 dedos, 15 joints, capturas Extended/Flexed; transición 0.2 s; Animator opcional vacío. |
| Hand3DInteractionPose | GrabDoor (campos Grab) y GrabDocument (campos HandHold), referencias, neutrales y duraciones. |
| Hand3DInteractionCoordinator | Propiedad de interacción local; el dueño actual es estado de ejecución, no una captura serializada. |
| Hand3DDoorInteractor | Cámara del mundo, punto de palma, movimiento/input/pose y RawImage del brazo. |
| Hand3DDoorBinding x9 | Motor original, raíz de exclusión, offset y orientación por pomo. |
| Hand3DDocumentInteractor | Adaptador de contacto actual, movimiento, pose y display. Input vacío en YAML, resuelto por Awake. |
| Hand3DDocumentBinding x3 | Documento original, renderer/collider, sprite, LocalizedString, alcance y evento de descubrimiento. |
| Hand3DDocumentDisplay | DocumentHoldPoint, visualRoot, Paper Grip, Image/TMP, RawImage y overlay. |
| Hand3DThumbOverlay | Renderer WRAD, ArmCamera, RawImage, PixelThumb e índices de 66 triángulos. |

`Hand3DScreenProjection` es una utilidad estática y no aparece como componente serializado.

Herramientas de Editor conservadas: Hand3DEditors, Hand3DModeEditors, Hand3DDoorEditors, Hand3DSceneDoorsEditor, Hand3DDocumentEditors y Hand3DDocumentVisualEditor. No se invocan Prepare/Capture durante las pruebas.

## Capturas

- Cinco dedos derechos, tres articulaciones por dedo, todos con Extended y Flexed capturados. Las 15 referencias y sus Euler completos se conservan en el inventario; ver también `FINGER_CAPTURES.md`.
- OK: combinación 3, Action=None, 6 Visual Overrides, orientación Exploration activada. Euler del offset: `(38.74139, 36.336205, 10.743572)`, duración 0.2 s y curva serializada.
- Point: combinación 29, Action=PointClick, sin Visual Overrides. Offset Exploration `(9.986709, 93.45542, 8.546045)`, duración 0.2 s y curva serializada.
- Los OnAction de ambas entradas no tienen llamadas persistentes configuradas. La prueba EventOnly utiliza una entrada sintética solo en memoria.
- GrabDoor: 15 rotaciones locales, muñeca y dos twists adicionales con neutral/captura, transición 0.12 s.
- GrabDocument/HandHold: 15 rotaciones locales, adicionales vacíos, transición 0.12 s; traslaciones de dedos desactivadas y lista vacía.
- No se recaptura nada. Los resultados de las 32 combinaciones son rotaciones calculadas en Editor y no sustituyen las capturas.

## Modos y movimiento

Exploration y HandEdit conservan posición/rotación de modelo relativa a cámara, rotaciones locales de bicep/forearm y orientación de muñeca relativa a cámara. Los números completos están en el JSON y la escena textual; no convertirlos todos a localRotation.

- Transición de modos: 0.35 s.
- Inspección: mínimo -174.91316 grados, máximo 0, signo -1, velocidad 120, smoothing 0.08 s.
- Movimiento: velocidad 900, smoothing 0.08 s, referencia 1920x1080.
- Límites X e Y: 0.06 a 0.94. Retorno: 0.18 s.
- Cadena real: `shoulder.r > bicep.r > forearm.r > wrist.r`.
- UpperArm asignado es **bicep.r**, no shoulder.r. El solver rota bicep y forearm; muñeca/dedos/twists pertenecen al rig.
- La escala del modelo actual se conserva; no se normaliza para facilitar las pruebas.

## Puertas y documentos

Nueve puertas: FrontDoor, Right, Left, ClosedDoor, BellDoorEntrance, Kitchen, Dormitory, Studio y Bathroom. Sus motores originales y raíces se conservan. Los bindings tienen offsets de agarre/orientación a cero; el inventario contiene cada referencia resuelta y configuración del motor original, incluido bloqueo.

- Detector de puertas: radio 100 px a 1080p, alcance 1.5 m, snap 0.08 s, descubrimiento 1 s, offset de pantalla cero; Debug y Log Interactions activados.
- Los límites antiguos de 18/80 px y la puerta de prueba permanecen serializados; no se borran en esta fase.
- Documentos: Elisabeth_BellHintNote (Kitchen), Elisabeth_IconNote (Dormitory) y HabilityMemo (SecretPassage).
- Detector de documentos: radio 55 px a 1080p, alcance 0.9 m; cada binding conserva máximo 1.5 m. Se aplica el mínimo de ambos al adquirir.
- Sprite, estilo, claves de localización y UnityEvents existentes se conservan. Los eventos apuntan a sus acciones MapDiscoveryDiscoverAction; no se ejecutan como parte del inventario.
- Papel: altura 0.58, offset/rotación cero, recogida 0.12 s, devolución 0.18 s. visualScale heredado sigue guardado, sin uso para tamaño actual.

## Cámara y composición

- ArmCamera sigue a la cámara principal por jerarquía. FOV 40, near 0.2, far 500, fondo transparente, culling de capa 6; URP sin postprocesado configurado en esa cámara.
- RT_PixelArm: 320x180, Point, sin mipmaps activos y antialiasing de textura 1. Referencia de asset y formatos se conservan íntegros.
- PixelArmDisplay y PixelThumb usan la misma textura. El material UI se conserva; no se crea un shader nuevo.
- Canvas Overlay: brazo de fondo, papel/texto, máscara/PixelThumb de frente. Los dos últimos arrancan inactivos hasta sujeción.
- 66 triángulos del pulgar, vinculados al renderer original. Runtime actual usa BakeMesh con tratamiento de escala corregido. Full Arm, Thumb Mask y Thumb Overlay permanecen disponibles.
- El test verifica activación, textura, orden y geometría. No verifica los píxeles reales de esta composición en Game.

## Entrada y dependencias externas

Controles actuales: RB/Shift alterna modo; R3/E pulgar; D-pad arriba/R índice, derecha/T corazón, abajo/F anular, izquierda/G meñique; R2/RMB + stick derecho/ratón mueve; stick izquierdo horizontal o A/D inspecciona; LT/LMB ejecuta acción.

Se conserva la copia privada de PlayerInput.actions y las exclusiones de Sprint para Shift/LT. En HandEdit se suspenden controles de locomoción y FirstPersonController; durante movimiento del brazo se bloquean acciones del jugador. Los estados enabled y bindings originales se preservan. El controlador antiguo FirstPersonArmController está deshabilitado y no tiene ArmView asignado.

El inventario incluye referencias al PlayerInput y su asset, cámaras, motor DoorHandInteractable y DocumentHandInteractable. El runtime experimental no se sustituye por stubs. La UI antigua de documentos solo es dependencia de la herramienta inicial de preparación; esa herramienta no se ejecuta.
