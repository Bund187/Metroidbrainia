# Fase 6 — Acciones por gesto

Esta fase conserva Legacy, las capturas, los perfiles funcionales y la escena guardada. No comienza fases posteriores. La aceptación visual comunicada por el usuario corresponde a la corrección de quaternions anterior; las pruebas de esta fase no se presentan como validación visual de Game.

## Arquitectura

`Hand3DPoseRecognizer` sigue identificando combinaciones. No ejecuta acciones. Sus antiguos campos Action/OnAction permanecen como datos de compatibilidad y no se borran ni migran automáticamente.

`Hand3DPoseActions` resuelve asociaciones explícitas por combinación, comprueba disponibilidad/modos, inicia las acciones, mantiene sus ejecuciones y concentra cancelación/restauración. No contiene ramas por nombres como OK o Point. Una asociación explícita con lista vacía desactiva deliberadamente las acciones de ese gesto. Cuando no existe asociación, un adaptador runtime interpreta el antiguo Action enum y conserva sus parámetros/eventos: no modifica datos serializados.

`Hand3DGestureAction` es una definición ScriptableObject reutilizable. `Begin(context)` devuelve una ejecución `Run` con estado propio o null para una acción instantánea. `Run.Tick(dt)` devuelve true al terminar; `End(cancelled)` permite limpiar efectos. Las definiciones no guardan relojes compartidos entre manos. No hay nuevos MonoBehaviours obligatorios, managers ni buses.

El contexto ofrece gesto, combinación lógica actual, modo, origen Mouse/Gamepad/Unspecified, control del brazo, coordinador, propietario de interacción si existe, perfiles runtime y referencias de cámara/jugador. Durante una acción normal el coordinador suele estar libre: no se inventa un objetivo ambiental ni se ofrece la puerta más cercana sin validarla.

Acciones incluidas:

- **Point Click:** reproduce exactamente `sin(clamp01(elapsed/duration)*PI)*amplitude` y finaliza con peso cero. Aplica Flexed al índice con las capturas vigentes, sin ejes fijos. Legacy consume el peso por su ruta anterior; Universal lo recibe como contribución ordenada.
- **Configured Event:** invoca `Bindings → On Event` del ejecutor. Es instantánea y no entra en la lista de actualizaciones. El evento se invoca como máximo una vez por solicitud, incluso si se repite esa definición en la lista.
- **Profile Pulse:** contribución temporal de un perfil universal, con duración y amplitud. Permite dedos, muñeca, hombro y demás canales válidos del perfil. Requiere Universal. No crea ni modifica capturas; el ID debe existir en la biblioteca runtime asignada al rig.

## Composición y prioridades

El ejecutor ordena establemente las acciones por Priority: valores mayores se aplican después; a igualdad, manda el orden explícito del Inspector. No depende de Update/LateUpdate entre componentes distintos. Una acción con peso parcial mezcla sobre las anteriores; con peso 1 prevalece en sus canales. Las máscaras del perfil conservan los demás canales.

El compositor resuelve primero la orientación especial del gesto y luego recibe las contribuciones de acciones, antes del objetivo y solver IK. Esto permite que una rotación absoluta de muñeca de una acción conserve su espacio de referencia. La adquisición de una interacción cancela las acciones; además, sus contribuciones se atenúan por `1 - GrabWeight` para proteger mezclas residuales. El IK conserva su solver, orientación final y preferencia derivada de la referencia.

No hay escrituras de huesos en ninguna acción. `Hand3DRig` sigue siendo quien realiza el commit. Se conservan íntegros `Frame.UpdateWorld`, `NormalizeRotation`, `RotationNorm` y las comprobaciones recientes de fuentes inválidas antes de interpolar. Las nuevas contribuciones pasan por esos mismos límites de normalización. El ejecutor rechaza pesos NaN/Infinity.

Los perfiles de acciones deben usar espacios locales/cámara explícitos. El adaptador especial `ExplorationWristOffset` de la migración no se interpreta como un perfil genérico de acción: se rechaza con diagnóstico. No se cambian longitudes, escalas ni restricciones de posiciones de segmentos.

## Configuración en Unity

PointClick existente funciona sin hacer cambios: la escena no se reescribe y conserva sus duraciones, amplitud, referencias y UnityEvents.

Para añadir asociaciones explícitas:

1. En Project, usar `Create → Metroidbrainia → Hand 3D → Actions → Point Click`, `Configured Event` o `Profile Pulse`. Guardar los assets nuevos fuera de Editor; por ejemplo `Assets/Experiments/Hand3D/Actions/`.
2. Seleccionar `PlayerCapsule/Capsule/armsFBX → Hand3DPoseActions`.
3. Añadir un elemento en `Bindings`. Seleccionar la combinación de dedos: OK = Thumb + Index (3); Point = Thumb + Middle + Ring + Little (29).
4. En `Actions`, asignar una o varias referencias a los assets. La lista vacía significa ninguna acción. Las combinaciones duplicadas se rechazan, en vez de escoger una accidentalmente.
5. Configurar duración/amplitud y Priority en el asset reutilizable. `Allowed Modes` admite Exploration, HandEdit o Both.
6. Para Configured Event, configurar `On Event` en ese mismo binding, donde pueden referenciarse objetos de escena. El asset no necesita referencias persistentes a objetos de escena.
7. `Invoke Original Gesture Event` conserva por defecto el antiguo OnAction, una sola vez al aceptar la solicitud. Si se reutiliza el mismo callback en On Event, desactivar esa opción o retirar la duplicación intencionada; son dos eventos distintos, no una migración automática.

Los antiguos campos PointClick Duration/Amplitude del componente se usan solo cuando no hay un binding explícito. No es necesario copiar esos valores a un asset para seguir jugando como antes. Las herramientas antiguas de captura siguen operativas.

Para Profile Pulse, indicar el ID exacto de un perfil de la copia runtime. No editar `HandPoses.asset` ni sobrescribir perfiles funcionales para experimentar: usar una copia nueva explícita y asignarla deliberadamente al rig, según el flujo de fase 5. En Legacy se omite esa acción; si es la única elegible, TryExecute devuelve false y muestra el motivo. PointClick y eventos siguen disponibles en Legacy.

Si se exporta un perfil nuevo desde el Pose Editor, activar su opción de incluir perfiles no asignados en la copia runtime: la exportación funcional predeterminada solo copia los 13 perfiles migrados. Incluir perfiles adicionales no los activa como gestos. Exportar a un asset nuevo, asignar esa copia al rig y escribir deliberadamente su ID en Profile Pulse; no reemplazar OK ni Exploration Default por nombres de prueba.

## Modos y mundo

Las acciones visuales pueden ejecutarse en los modos que permita su asset. No cambian permanentemente la postura base de HandEdit.

`Affects World` bloquea por defecto los efectos en HandEdit. `Allow World In Hand Edit` permite una excepción explícita, además de Allowed Modes. En Exploration las acciones marcadas World requieren control del brazo (R2/RMB). `Requires Arm Control` permite exigirlo también a otra acción. HandEdit no activa el movimiento libre por R2; una excepción World en ese modo no exige R2 salvo que Requires Arm Control esté marcado.

La acción de evento es una invocación configurable, no un detector de objetos. No prueba contacto, alcance ni obstáculos y no debe presentarse como una campana/interruptor implementado. Para una futura acción que actúe sobre un objetivo, su `CanBegin` debe usar `Hand3DContactDetection` con las referencias de su adaptador local y comprobar objetivo, contacto, alcance, visibilidad y obstáculos; el coordinador continúa gobernando la ocupación. No se reutilizan campos privados de los detectores de puertas/documentos ni se duplican sus algoritmos. Esta fase no crea un selector ambiental nuevo sin una mecánica que lo necesite.

## Cancelación

Se cancela la solicitud completa y se retiran todas sus contribuciones cuando cambia el gesto, cambia el modo, se adquiere puerta/documento, se pausa (`timeScale <= 0` o pausa de aplicación), se pierde foco, se desactiva el ejecutor/Input, se desactiva rig/movimiento/reconocedor o se cambia de backend durante una ejecución. No se reanuda automáticamente.

R2 soltado cancela las acciones dependientes del control o World. PointClick visual no depende de R2 y conserva su ejecución independiente, como antes. El coordinador mantiene el bloqueo durante una interacción y su restauración. Los dedos lógicos no se restauran inventando otra combinación: permanecen en el estado actual del jugador.

La cancelación retira la contribución inmediatamente; el siguiente commit compone la postura vigente. Una transición normal de modo conserva su interpolación propia. La finalización normal de los pulsos llega suavemente a peso cero. Las acciones instantáneas no dejan una ejecución pendiente.

Reentrancia: no se acepta otra ejecución mientras se despachan callbacks. Si un evento cambia el gesto o desactiva/cancela el ejecutor, no se siguen iniciando acciones de esa solicitud. Una pulsación física genera una intención por la ruta WasPressedThisFrame existente; no se cambian bindings de Input.

## Extender

Para una acción nueva, crear un ScriptableObject derivado de `Hand3DGestureAction`, configurar sus campos y asignarlo a un binding. Una acción instantánea ejecuta su efecto en Begin y devuelve null. Una acción temporal devuelve su propia Run; esta aporta `ProfileId/ProfileWeight` (o el canal de compatibilidad del índice) y limpia efectos externos en End. No debe escribir Transform de los huesos.

Para un gesto nuevo, añadir su combinación a los datos del reconocedor, añadir el binding de acciones y, en Universal, su asignación visual explícita en una copia runtime. Esa asignación puede tener listas vacías si solo se usan los estados individuales. No hace falta añadir casos al Input, ejecutor o compositor.

La implementación permite varias acciones independientes en la misma solicitud. No es un secuenciador: no se aceptan nuevos retriggers mientras alguna Run de esa solicitud siga activa. Acciones de mundo que dependan unas de otras necesitan una definición específica que coordine ese comportamiento; no se introduce un framework de habilidades.

## Pruebas y reproducción

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase6/Run.ps1 -QuaternionAudit -BuildPlayer`

Solo usa la copia temporal marcada de las fases anteriores. Protege por hash los originales; no instala assets ni modifica escenas de producción. `-Phase6Only` ejecuta únicamente los casos de esta fase. Los resultados quedan en `Results/<runId>/`. La auditoría de quaternions se instrumenta solo en la copia y no añade callbacks a producción. La build temporal comprueba acciones/eventos con ambos modos y ambos backends, sin aprobación visual.

Los casos nuevos cubren 32 combinaciones, gesto adicional por datos, ninguna/una/varias acciones, eventos instantáneos, curva exacta de PointClick, ausencia de duplicados, reentrancia, finalización/cancelación, permisos, R2, pausa/foco, reactivación, contactos reales de puerta/documento, perfiles de hombro/muñeca, prioridades, restauración e IK y 2.000 frames de movimiento con acciones por backend. Los fixtures de Editor invocan explícitamente los callbacks de ciclo de vida.

## Validación manual pendiente en Game

1. Mantener las referencias actuales y seleccionar Universal en Hand3DRig.
2. Ejecutar PointClick con L2/clic izquierdo en Exploration, con y sin R2. Comprobar recorrido del índice y restauración.
3. Repetir en HandEdit; comprobar bloqueo del jugador/cámara e inspección manual.
4. Cambiar de gesto y modo durante el clic. Pausar, perder foco y desactivar/reactivar el ejecutor. No debe quedar una contribución residual ni reanudarse sola.
5. Agarrar una puerta durante PointClick, incluida una bloqueada. Repetir con un documento; comprobar restauración, movimiento libre del papel, texto y pulgar por delante.
6. Probar las 32 combinaciones, movimiento por pantalla, extremos y retorno; revisar que Console no muestra errores de quaternions.
7. Repetir las comprobaciones principales con Legacy.
8. En una copia temporal de la escena, crear Point Click + Configured Event y asignarlos juntos a Point. Conectar On Event a un receptor de prueba/contador. Cada pulsación debe animar el índice e invocar una sola vez el evento configurado y, si está habilitado, el original.
9. En una copia runtime de prueba, asignar una acción Profile Pulse con un perfil parcial. Revisar canales afectados, Priority y restauración. Revertir las asignaciones de prueba sin guardar cambios sobre la escena funcional.

No se afirma que la animación o el overlay tengan aprobación visual de esta fase hasta ejecutar esas pruebas en Game.
