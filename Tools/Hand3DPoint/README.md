# Point ambiental del brazo 3D

Solo TestArmScene. Se conserva Legacy y Universal, sin modificar input, reconocedor, ejecutor de acciones, rig, movimiento/IK ni ningún script de interactuables compartidos.

## Comportamiento original comprobado

FirstPersonArmController selecciona ArmPose.Point desde su acción SelectPoint. Mientras ArmMode está activo, ArmAction.WasPressedThisFrame llama PerformArmAction: reinicia el estado visual Point de ArmView, proyecta su HandPoint UI, hace Physics.SyncTransforms y un raycast de interacción desde ese píxel, con alcance general 2 m, capas configuradas y triggers Collide. No utiliza el centro fijo de cámara. El componente del collider recibe IHandActionInteractable.TryPerformHandAction(Point, cámara del jugador, RaycastHit); después se invoca onPointAction.

| Objeto | Regla y efecto original, conservados |
|---|---|
| BellInteractable | Activo/inicializado, Point, collider cuyo GetComponentInParent de la interfaz sea ese objeto y distancia cámara→hit.point ≤1,8 m. Ring inicia/reinicia oscilación local X: amplitud 45°, frecuencia 1,2 Hz, damping 0,4, duración 6 s y retorno exacto. Repetir golpe conserva ángulo y refuerza dirección; no hay cooldown propio. PlayOneShot si AudioSource/clip están asignados. NotifyBellRung sobre su puzzle. |
| LightSwitchInteractable | Mismas validaciones de Point/collider/cámara y 1,8 m. Rechaza mientras moving. SetState alterna isOn, sincroniza controlledLights, anima el rocker, reproduce clip opcional e invoca OnTurnedOn/Off. No se duplica estado en el brazo. En TestArmScene moveDuration=0,01 s, off=(0,0,0), on=(180,0,0), una luz controlada. El default del código es 0,08 s: se conserva el valor de escena. |
| BellSequencePuzzle | Avanza por identidad de campana; fallo vuelve a 1 si se repitió la primera, o a 0 en otro caso. Al completar: Unlock/OpenAutomatically de targetDoor y OnSolved una vez. Después no reemite solución. |

Los scripts compartidos permanecen idénticos por SHA-256. Se usa su método validado, no Ring/Toggle directamente. ArmPose es el enum exigido por la interfaz compartida; no se referencia ningún controlador 2D nuevo desde runtime.

## Flujo 3D

El Input existente sigue leyendo L2/LMB una sola vez. El binding de combinación 29 contiene TestArmPointClick (prioridad 0) y TestArmPointWorld (prioridad 10). El primero mantiene duración/amplitud 0,18/0,18 existentes. El segundo es instantáneo, Exploration/World/RequiresArmControl y delega en Hand3DPointInteractor del mismo coordinador. No crea Run, lease, pose Grab ni lectura de dispositivos.

Condiciones del adaptador: Point reconocido y máscara lógica actual 29; Exploration sin transición; R2/RMB activo; rig/movimiento/input/adaptador activos; coordinador libre y sin restauración. HandEdit solo permite el clic visual. Puertas/documentos siguen conservando su orden y propiedad; Point no puede ejecutarse durante ocupación/restauración ni restringe movimiento libre.

CanBegin consulta el contacto. Begin vuelve a consultarlo porque un evento previo puede mover/desactivar un objetivo o cancelar la solicitud. No se reutiliza un candidato de otro frame. El token es la identidad del contexto de solicitud, por adaptador; incluso dos definiciones ambientales o desactivación/reactivación dentro del callback producen como máximo una llamada al objetivo. Un rechazo del objeto elegido no activa otro en la misma pulsación. La protección de clic activo de PoseActions permanece; no se añade cooldown ambiental.

La activación es instantánea: cambiar gesto después cancela el clic, no deshace una campana golpeada o una luz ya alternada. Una cancelación/cambio de gesto anterior a Begin impide la activación. No se programa un golpe diferido.

## Contacto y selección

Hand3DContactDetection y Hand3DScreenProjection se reutilizan para cámara del brazo/RT/RawImage, visibilidad, cámara del mundo/RawImage opcional, distancia y obstáculos. No se mide distancia entre malla ViewModel y objeto real. TryHitTarget convierte el píxel visible a un rayo de cámara del mundo y obtiene un RaycastHit real del collider, incluyendo triggers y filtro de capas. HasObstacle se usa con Collide para Point; su firma antigua sigue usando Ignore para puertas/documentos. Se mantienen las sincronizaciones necesarias por ciclo.

Con Tolerance Pixels=0 se exige collider bajo la punta visible; 0,01 px de referencia se utiliza únicamente como epsilon de reproyección numérica. No se inventa un radio artístico de 55/100 px para campanas. La tolerancia adicional configurable permite apuntar al punto de interacción si la punta está dentro de ese margen y el collider/alcance/obstáculos siguen siendo válidos. Se escala a la altura real de pantalla.

Entre candidatos admisibles se elige el punto de interacción proyectado más cercano al dedo; por defecto es bounds.center del collider. Si ese centro está recortado se usa la separación del hit visible. Un empate dentro de 0,001 px se resuelve por orden serializado de Targets. La preparación ordena por ruta de jerarquía. Obstáculos y reglas originales tienen prioridad sobre cercanía. Interaction Point es un punto de selección/aim, no sustituye al collider de contacto.

## Configuración entregada en TestArmScene

- Rig: PlayerCapsule/Capsule/armsFBX, con un único Hand3DPointInteractor nuevo.
- Punta: arms/root/shoulder.r/bicep.r/forearm.r/wrist.r/finger_index1.r/finger_index2.r/finger_index3.r.
- Offset local inicial: (-0,000716347946; 0,001929491758; 0,000419403805). Se obtuvo del centro del extremo distal de vértices con peso ≥0,99 sobre index3, leyendo la malla en Editor. El pivot del último hueso no se presupone punta real. El fit se realiza solo al preparar, sin BakeMesh adicional por frame en runtime.
- El adaptador permite sustituir Point Fingertip por un Transform hijo creado manualmente y poner offset a cero, o ajustar el offset actual. Revisar la punta en Game con la RT, no solo por posición mundial en Scene View.
- Detection copia una vez cámara del mundo, displays y capas existentes; HandPoint se vincula a Point Fingertip al consultar. Maximum Distance general=2 m. Cinco entradas: cuatro campanas y un interruptor ya existentes, cada una con su collider real, reach=1,8 m y tolerancia=0.
- Assets nuevos: Assets/Experiments/Hand3D/Actions/TestArmPointClick.asset y TestArmPointWorld.asset. Las demás asociaciones, OnAction y poses permanecen intactos.
- Objeto nuevo Hand3DPointBellSequence: instancia del componente compartido BellSequencePuzzle, con las campanas existentes BellHinge 1→2→3→4, tomadas por ruta del puzzle original de Prototype. Target: Mansion/Floor 1/Entrance/Wall_Door_ClosedDoor/DoorHinge. Solo se añaden las cuatro referencias puzzle antes nulas; no cambian sus ajustes de oscilación/audio/colliders.

No se añaden copias de puertas ni se tocan prefabs: se reutiliza la instalación de campanas/interruptor existente. El puzle nuevo usa exactamente la clase original. No se trasladan sistemas de mapa de Prototype ni sus callbacks a objetos ausentes: OnSolved del montaje experimental queda disponible y la apertura se ejecuta desde targetDoor como en el original.

Los clips de campanas e interruptor están sin asignar en la escena guardada; no se ha inventado sonido. Con clips configurados el mismo API original ejecuta audio. Los listeners nulos de sistemas de mapa que ya existían en el interruptor se preservan; esta tarea no reconstruye esos sistemas.

## Añadir instancias futuras

**Descubrimiento automático actual:** no es obligatorio registrar objetos en Targets. La acción proyecta el índice visible al mundo y consulta el primer collider relevante, incluidos triggers según las reglas originales. Un collider no interactuable, un receptor desactivado o de otra escena bloquean la interacción con lo que haya detrás. No hay búsquedas globales por fotograma.

Targets conserva únicamente overrides opcionales de alcance para una pareja Receiver/Collider exacta; prevalece la primera entrada coincidente y nunca amplía el alcance general. Las API originales conservan sus límites propios (1,8 m para campanas e interruptor). Los campos Interaction Point y Tolerance Pixels permanecen serializados como metadatos antiguos: no desplazan el rayo ni autorizan activar objetos junto al dedo. Esta restricción sustituye la antigua selección por tolerancia visual. Varias entradas o acciones ambientales no producen activaciones adicionales.

1. Añadir/configurar el objeto con su IHandActionInteractable, collider y comportamiento originales; no modificar Input/Recognizer/Rig/PoseActions.
2. No añadir Targets para el uso normal. El collider debe resolver el receptor mediante GetComponentInParent; puede estar en un hijo del objeto que implementa la interfaz. Revisar capas y cámara.
3. Añadir una entrada Receiver/Collider solo si hace falta limitar más su alcance. El objeto valida además su propio alcance. Se exige contacto físico proyectado exacto, también con entradas manuales.
4. Mantener el binding Point con una acción visual y una ambiental. La marca protege duplicados, pero no se necesita duplicar definiciones para cada objeto.
5. Para otra API, usar un adaptador pequeño que implemente el contrato compartido; no añadir casos de tipo al núcleo. No hay una lista de Bell/LightSwitch en runtime.

El menú GameObject → Metroidbrainia → Test Hand 3D → Prepare Point Actions prepara un rig no configurado. Si ya existe el adaptador, conserva calibración, tolerancias, targets y bindings y no reconstruye datos del usuario. Para otras APIs sin campo maxInteractionDistance, configurar sus entradas manualmente en Inspector; el runtime no usa reflexión.

## Reproducir pruebas y preparación

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPoint/Run.ps1 -PointOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPoint/Run.ps1 -PointOnly -CaseFilter 'reentrant_single_activation_*'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPoint/Run.ps1 -QuaternionAudit -BuildPlayer
```

El runner actual escribe todas las ejecuciones nuevas en `Tools/Hand3DLocalResults/Point/<runId>/`, ignorado por Git. Los resultados históricos permanecen intactos. `-PrepareOnly` también exporta sus intermediarios a esa ubicación.

`-PointOnly` sin build ejecuta Point en un solo Editor sobre la escena y biblioteca runtime guardadas: no reexporta perfiles ni copia suites 0, 2–4 y 6–8, y no inyecta contadores de rendimiento. Mantiene los fixtures de fases 1 y 5, sus snapshots y el probe necesario para compilarlos. Los arneses históricos no necesarios que existan en la copia aislada se mueven fuera de Assets; una ejecución completa los vuelve a preparar desde las fuentes canónicas.

`-CaseFilter` selecciona nombres de casos completos, sin distinguir mayúsculas, con comodines `*` y `?`. Un filtro sin coincidencias falla; no se interpreta como éxito. Solo se permite con `-PointOnly`, sin preparación ni build. Los nombres disponibles están en `point-results.json`. Una selección parcial no equivale a validar toda la funcionalidad; sin filtro se ejecutan todos los grupos actuales de Point y sus regresiones directas.

Los casos actuales incluyen `auto_*` y regresiones de contacto de puertas/documentos. Los casos antiguos que dependían de una lista de autorización o de proximidad se adaptaron al contacto exacto. En modo enfocado el runner protege por hash todos los archivos actuales de Assets/Packages/ProjectSettings durante la ejecución, admitiendo las integraciones manuales ya guardadas. La ruta histórica completa conserva sus requisitos de baseline originales; el snapshot enfocado no sustituye esa auditoría histórica.

La ruta completa sin `-PointOnly`, con auditoría y build opcionales, conserva la preparación histórica y sus lanzamientos separados. Agrupar esas suites requiere separar sus llamadas a `EditorApplication.Exit` y garantizar la limpieza entre ellas; queda fuera de esta mejora mínima. Los manifiestos históricos siguen siendo obligatorios incluso en modo enfocado: no eliminar ni ignorar sus dependencias. Las pruebas numéricas automatizadas no sustituyen la validación visual en Unity.

La copia temporal marcada de fases anteriores es obligatoria. No se usa Git, no se descargan paquetes y Unity nunca abre producción como proyecto de pruebas. Los arneses anteriores no se modifican en disco; se conservan las adaptaciones privadas de montaje de fases previas. Los contadores de máscara y auditoría se inyectan solo en TEMP. GC del arnés sigue N/D, sin nuevas cifras de FPS.

-PrepareOnly exporta PointConfigured.unity y los assets/metas generados por Unity en la carpeta local indicada; no los aplica al original. InstallPrepared.ps1 hizo una incorporación revisada al comienzo de esta tarea: valida snapshot previo, exactamente siete bloques modificados y cuatro objetos YAML añadidos; conserva byte a byte todos los demás bloques. Rechaza cambios ajenos. No usarlo para sobrescribir ediciones posteriores de escena; no ejecutarlo como parte de las comprobaciones manuales habituales.

Los manifiestos protegen Prototype, scripts compartidos, prefabs, bibliotecas, configuración y evidencias 0–8. La escena actual debe coincidir con el SHA de scene-installation.json. Ver REPORT.md, MANUAL.md y los JSON/logs de Results para resultados, límites y aceptación visual pendiente.
