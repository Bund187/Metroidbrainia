# Entrega — Point sobre campanas e interruptores

## Resultado

En TestArmScene, Point (máscara 29) + R2/RMB + L2/LMB ejecuta PointClick y solicita la acción al objeto bajo la punta visible del índice cuando hay alcance, visibilidad, capas, contacto y coordinador libre. HandEdit mantiene el clic visual y bloquea acciones del mundo. La mano no se adquiere ni se inmoviliza y no cambia permanentemente su combinación.

La explicación original 2D, reglas exactas de campanas/interruptor, flujo 3D, selección, configuración y ampliación están en README.md. El protocolo de aceptación visual pendiente está en MANUAL.md.

## Código reutilizado y adaptaciones

- Input, FingerState, PoseRecognizer, PoseActions, ActionContext, PointClick, Coordinator, Rig, Composition, Movement/IK y publicación de perfiles **sin modificaciones**.
- Una nueva definición Hand3DPointWorldAction utiliza el contrato CanBegin/Begin de fase 6; no lee input y no conserva Run temporal.
- Un único Hand3DPointInteractor en el rig, con entradas serializables de receiver/collider/aim/alcance/tolerancia. Funciona con cualquier IHandActionInteractable; no contiene comprobaciones de tipo Bell o LightSwitch ni estado de puzle/luz. El enum ArmPose.Point es el exigido por esa interfaz compartida, no una llamada al controlador 2D.
- Hand3DContactDetection añade muestreo por offset, obtención de hit real desde píxel del mundo y opción de triggers en obstáculos. Su API anterior mantiene Ignore y los mismos cálculos/sincronización para puertas/documentos. Hand3DScreenProjection sigue siendo la única conversión RT/RawImage/pantalla.
- Hand3DPointEditor prepara una configuración nueva y conserva datos existentes al repetir el comando. El ajuste de punta se calcula en Editor con geometría distal; runtime solo sigue el Transform/offset. No hay nueva jerarquía de adapters por objeto, servicio global ni segundo detector/ejecutor.

Una solicitud selecciona un objetivo; el propio método original puede rechazarlo por su bloqueo actual. No se prueba después otro objeto con efectos laterales. Los empates son por orden serializado. CanBegin y Begin consultan de nuevo por seguridad ante eventos reentrantes, movimiento/desactivación o cambio de gesto entre ambos; no hay caché de candidato entre frames. El token por contexto evita dobles invocaciones incluso con definiciones duplicadas y desactivación/reactivación en un callback.

## Montaje concreto

Se reutilizan las cuatro BellInteractable y una LightSwitchInteractable ya presentes, con sus colliders; no se crean réplicas ni se modifican prefabs. Cinco bindings, alcance propio 1,8 m, general 2 m y tolerancia adicional 0, según la configuración original. La comparación de contactos se realiza mediante las dos cámaras/displays, nunca por distancia física entre ViewModel y objeto.

La punta usa finger_index3.r de la mano derecha, con offset local `(-0.000716347946, 0.001929491758, 0.000419403805)`. Se puede ajustar o sustituir por un Transform hijo PointFingertip con offset cero. La comparación automatizada con el extremo skinned en 32 combinaciones × 4 pasos × 2 backends registra máximo **0,00000131671 unidades**. Esto acredita seguimiento geométrico; la oclusión y percepción de píxeles finales requieren Game.

TestArmScene no tenía BellSequencePuzzle configurado: las cuatro referencias puzzle eran nulas. Se añade un GameObject Hand3DPointBellSequence con la **misma clase compartida original**, secuencia BellHinge 1→2→3→4 y target `Mansion/Floor 1/Entrance/Wall_Door_ClosedDoor/DoorHinge`, leídos de Prototype en la copia temporal. Se añaden las cuatro referencias de campana a esta instancia. OnSolved experimental queda disponible sin trasladar sistemas de mapa desde Prototype. El componente original efectúa Unlock/OpenAutomatically y emite su evento; no se replica su contador/estado en el brazo.

Se preservan el interruptor, su luz, duración de escena 0,01 s, estados y UnityEvents. Los clips de audio originales están sin asignar en la escena guardada. No se inventan clips ni efectos: cuando se configuren AudioSource/clip, la misma llamada original ejecuta PlayOneShot. Los callbacks de mapa nulos preexistentes no se sustituyen ni se trasladan desde Prototype.

## Archivos

Modificados:

- Assets/Scripts/Experiments/Hand3D/Hand3DContactDetection.cs.
- Assets/Scenes/TestArmScene.unity.

Creados en Assets (y sus metas generados por Unity):

- Assets/Scripts/Experiments/Hand3D/Hand3DPointInteractor.cs.
- Assets/Scripts/Experiments/Hand3D/Hand3DPointWorldAction.cs.
- Assets/Editor/Experiments/Hand3D/Hand3DPointEditor.cs.
- Assets/Experiments/Hand3D/Actions/TestArmPointClick.asset.
- Assets/Experiments/Hand3D/Actions/TestArmPointWorld.asset.
- Meta de la nueva carpeta Actions.

Creados bajo Tools/Hand3DPoint: runner, preparación/instalación controlada, fixtures, referencia inicial por hash y snapshots, guías, auditorías y resultados/logs por ejecución. No se retira código histórico ni ningún archivo de producción.

La instalación de escena se revisó por bloques YAML: **siete bloques modificados** (cuatro referencias puzzle, un binding Point, una adición de componente al rig y SceneRoots) y **cuatro objetos YAML nuevos** (adaptador y GameObject/Transform/componente del puzle). Los demás bloques son byte a byte iguales. No se reemplazó toda la escena por la reserialización de Unity; InstallPrepared.ps1 verifica cada diferencia permitida y rechaza ediciones ajenas.

## Validación final

Ejecución completa **Results/abfc5928a245482d8d5b87b7e03d57ef/** sobre las fuentes definitivas. Se conserva la batería de fases anteriores, sin cambiar sus expectativas ni los fixtures originales en disco; solo las adaptaciones de montaje ya usadas en fases 6–8 se realizan en TEMP.

| Batería | Comprobaciones | Resultado |
|---|---:|---|
| Fases 0–6 | 151.602 | PASS |
| Fase 7 | 3.472 | PASS |
| Comparación histórica de trazas | 2.816 | PASS |
| Fase 8 con el componente añadido al snapshot | 11.945 | PASS |
| Point, 57 grupos | **610** | **PASS** |
| Total acumulado | **170.445** | **PASS** |
| Auditoría adicional de quaternions | 26.032.513 aserciones numéricas | PASS |
| Windows Development Build | Succeeded, 0 errores | PASS |
| Probe standalone | PointClick/eventos, dos modos y dos backends | PASS |

La referencia anterior era 169.834. La diferencia de una comprobación en fase 8 corresponde al nuevo componente serializado incluido en su snapshot; no se ha eliminado ninguna aserción para aceptar la integración. Los contadores son aserciones por estado/frame/hueso, no 170.445 escenarios diferentes. Las repeticiones intermedias no se suman de nuevo.

Los casos Point cubren campana e interruptor con intenciones Mouse/Gamepad; una activación por solicitud pese a duplicados; clic visual sin objetivo; alcance, obstáculos sólidos y triggers, capas, dedo invisible, varios candidatos y desempate; 32 máscaras; HandEdit y control R2; movimiento rápido y objetivo que cambia entre validación/ejecución; callbacks/cancelación/cambio de gesto; adaptador desactivado/reactivado dentro de callback; ocupación Door/Document; puzle correcto/incorrecto y apertura/evento; geometría de punta animada en ambos backends. Otra prueba desplaza rig y cámara ViewModel 100 unidades mientras mantiene el contacto visual y el objetivo en el espacio del jugador: no exige coincidencia mundial de mano y objeto.

Auditoría final: **12.737.250 entradas TRS**, cero Quaternion To Matrix y cero errores por backend; máximas desviaciones de norma local `9,19855338565867e-8` y mundial `9,881951257284527e-8`. La traza anterior/posterior conserva máximo error de posición 0 y ángulo 0,0000115812° al comparar quaternions serializados.

El probe standalone verifica ejecución/compilación runtime de acciones y eventos, no dispositivos reales ni interacción ambiental visual en Game. Los tests ambientales usan los componentes originales y raycasts reales en fixtures de Unity Editor. La continuidad de animación/audio en tiempo real y la alineación final de RT se reservan al manual; no se presentan pruebas sintéticas como aprobación visual o sonora. No se reclama mejora de FPS ni cifra de GC: el contador de memoria del arnés sigue N/D tras fallar su control positivo.

## Incidencias de desarrollo conservadas

Los intentos iniciales de compilación detectaron referencias a miembros que ActionContext no expone y a un método interno de PointClick no accesible desde Editor; se resolvieron usando el coordinador/contexto existente y SerializedObject, sin cambiar el núcleo ni abrir APIs innecesarias. La prueba ampliada c4158aa97bf94aa8a0feff475380184c encontró un acceso de diagnóstico a LastTarget después de que OnDisable lo limpiara en un callback. Se conserva el nombre antes de invocar gameplay y el token atraviesa la reactivación. La ejecución final aprueba ese caso; el intento fallido no cuenta como PASS. Las ejecuciones 0d6200c0a5654327bfdd664741057602 y 96c4c03037b740f79af22039fd8d8510 son referencias intermedias, no la entrega final.

## Pendiente de revisión humana

Aplicar MANUAL.md en TestArmScene con ratón/mando, Legacy/Universal, puntos fuera del centro de pantalla, campanas próximas, secuencia y puerta, interruptor, HandEdit y ocupación por puerta/documento. Revisar visualmente offset/RT durante movimiento/clic y ajustar solo la calibración de instancia si fuera necesario. Audio es N/A con los clips actuales vacíos; asignar clips originales en una instancia de prueba si se desea comprobar sonido audible.

No se prueba ni se integra el brazo en Prototype. No se añaden sistemas de mapa, menús, configuración global, mecánicas nuevas ni framework de interactuables. El alcance copiado de cada target debe mantenerse sincronizado si el usuario cambia su máximo original; la implementación original sigue siendo la autoridad final. No hay descubrimiento dinámico por frame: las futuras instancias se añaden a Targets en Inspector.

La comprobación por hash final confirma Prototype, brazo 2D, FirstPersonArmController, ArmView, BellInteractable, LightSwitchInteractable, BellSequencePuzzle, DoorHandInteractable, DocumentHandInteractable, prefabs, Starter Assets, bibliotecas/perfiles, paquetes y ProjectSettings intactos. El cambio autorizado está limitado al detector experimental, las adiciones Point y los bloques de TestArmScene indicados. No se realizaron operaciones de Git.

`final-integrity.json` confirma **2.014 archivos originales sin cambios**, exactamente dos originales modificados (detector/escena), once archivos nuevos de Assets incluidos metas y **706 archivos históricos de herramientas/evidencias intactos**. Incluye hashes completos de los originales protegidos y comprueba que las fuentes/escena/assets entregados coinciden con los del ensayo final. `scene-installation.json` conserva los siete IDs de bloque modificados, cuatro IDs nuevos y SHA-256 de la incorporación revisada.
