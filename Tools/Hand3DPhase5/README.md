# Phase 5 — Universal runtime poses

Solo brazo experimental de TestArmScene. Legacy permanece disponible y es el valor guardado en la escena. No se inicia ninguna fase posterior.

## Configuración en Unity

1. Seleccionar `PlayerCapsule/Capsule/armsFBX`, componente `Hand3DRig`.
2. `Pose System`: `Legacy` o `Universal`. Puede compararse en Play; los cambios de Play no se guardan automáticamente.
3. `Runtime Poses`: `Assets/Experiments/Hand3D/RuntimePoses/TestArmRuntimePoses.asset`.
4. El Inspector muestra el backend efectivo en Play. Una configuración inválida muestra el motivo y utiliza Legacy; corregir la referencia y alternar el selector para reintentar.

La escena conserva sus valores anteriores y añade únicamente esos dos campos al Rig. No es necesario añadir componentes ni cambiar cámaras, bindings, IK, renderizado o controles.

## Copia explícita y edición

En `Tools → Metroidbrainia → Hand 3D → Pose Editor`, seleccionar el rig y la biblioteca de fase 4. El botón `Export explicit functional runtime COPY` crea un asset nuevo en `Assets/Experiments/Hand3D/RuntimePoses`. No sobrescribe assets existentes ni asigna automáticamente la copia al rig.

La exportación funcional copia 13 perfiles. Los cinco perfiles de prueba/no asignados de la biblioteca actual quedan excluidos por defecto. La opción de incluir perfiles no asignados solo los copia: no los activa. En el asset runtime, las asignaciones por ID y combinación lógica son explícitas; un nombre como `OK_Test_Capture` nunca sustituye automáticamente una pose funcional.

El contenedor y los tipos de perfil están en scripts runtime. La biblioteca original, su tipo de Editor y `HandPoses.asset` siguen separados e intactos. Reexportar después de editar la biblioteca y asignar deliberadamente el nuevo asset permite comparar o revertir sin perder capturas. No se sincronizan automáticamente ambas copias.

Para probar una captura completa nueva, duplicar/exportar a otro asset runtime y asignar su ID deliberadamente a la capa adecuada. Una base de modo necesita canales de raíz y rotaciones de upper arm, forearm y wrist; una captura parcial no satisface por sí sola esos requisitos. Los gestos de HandEdit deben limitarse a dedos. El validador rechaza cambios de offsets de segmentos que modificarían la cadena IK.

## Composición

`Hand3DRig` selecciona exclusivamente un backend. En Universal crea un `Hand3DPoseComposition`, calcula el frame y realiza el único commit final de las articulaciones registradas. No escribe en huesos ajenos al catálogo.

Orden de composición: referencia del modo e inspección/transición → estados Extended/Flexed → perfiles del gesto → PointClick → interacción temporal → objetivo y solver IK → commit. Los perfiles parciales conservan los canales sin influencia. Las referencias se reconstruyen desde el estado original, sin acumular offsets sobre el frame anterior. Las curvas, neutrales y espacios importados conservan su significado; las orientaciones especiales de muñeca no se convierten indiscriminadamente a localRotation.

`Hand3DArmMovement` conserva entrada de movimiento, límites, retorno y solver analítico. En Universal opera sobre el buffer del compositor: deriva el codo de la postura activa, adapta bicep/forearm al destino y conserva la orientación mundial de muñeca. No escala ni alarga segmentos. En Legacy conserva la ruta anterior de aplicación.

`Hand3DModeController` aporta inspección y transiciones a una referencia externa. `Hand3DInteractionPose` sigue aportando intención, peso y liberación; Universal usa los tiempos del grupo asignado. Coordinador, detectores, Input, reconocedor, acciones y renderizado no se sustituyen. PointClick aporta su peso al compositor, sin un ejecutor de acciones nuevo.

GrabDoor usa el objetivo de contacto existente. GrabDocument mantiene movimiento libre. Ambas capas temporales restauran los estados lógicos existentes. Se mantiene el orden de adquisición: el detector posterior al Rig puede iniciar la representación de Grab en el frame siguiente; no se reordenan los detectores en esta fase.

## Prueba manual pendiente

Realizar en Game, primero con Legacy y luego Universal, usando la misma escena y controles:

1. Confirmar los cinco dedos y las 32 combinaciones; OK y Point en Exploration.
2. Pasar a HandEdit; comprobar dedos, OK, Point e inspección manual sin mover la postura principal.
3. Recorrer horizontal, vertical, diagonales y extremos de pantalla. Soltar y comprobar retorno; observar especialmente la flexión del codo.
4. Alternar rápidamente gestos y modos; ejecutar/cancelar PointClick y adquirir un objeto durante la acción.
5. Probar las nueve puertas: contacto, apertura manual/automática, bloqueadas, liberación de R2, cancelación y repetición.
6. Probar los tres documentos: adquisición, movimiento lejos del alcance inicial, devolución, texto, tamaño y pulgar delante del papel.
7. Pausar, perder foco, desactivar/destruir un objetivo y comprobar liberación/restauración del control del jugador.
8. Volver a Legacy y repetir. Comparar también un cambio de backend durante una transición: la selección reconstruye el compositor y no promete conservar la fase visual intermedia.
9. Con una copia runtime de prueba, asignar intencionadamente una captura completa y comprobar que cambia la referencia del brazo, conserva el gesto y sigue alcanzando destinos sin saltos perceptibles.

No se ha aprobado visualmente la animación en Game. Las métricas numéricas y el ejecutable automatizado no sustituyen estas pruebas, especialmente codo, cambios de backend y overlay del pulgar.

## Reproducción automatizada

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase5/Run.ps1 -BuildPlayer`

Requiere la copia aislada marcada utilizada por las fases anteriores. No abre ni compila el proyecto de producción. `-Phase5Only` omite las regresiones anteriores. `-InstallRuntimeCopy` es una acción de instalación explícita para la primera copia; rechaza sobrescribir un asset diferente y limita la escena a los dos campos nuevos. No se requiere para repetir pruebas.

Los resultados por ejecución, logs, build, importación y hashes están en `Results/<runId>/`. El probe del jugador se introduce únicamente en la copia temporal, nunca en la escena de producción.
