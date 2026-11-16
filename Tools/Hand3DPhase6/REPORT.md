# Informe de fase 6

## Problema y resultado

Antes, Hand3DPoseActions reconocía el enum PointClick, poseía su reloj, calculaba su seno y disparaba el UnityEvent. Añadir otro comportamiento temporal exigía ampliar ese controlador. Solo había un booleano de ejecución y un canal de peso, sin asociaciones múltiples ni permisos por acción. El coordinador ya cancelaba PointClick al adquirir una interacción; no se duplica ese mecanismo.

Ahora el mismo componente ejecuta definiciones reutilizables mediante un contrato Begin/Tick/End y una lista de asociaciones por combinación estable. El reloj y la amplitud de PointClick pertenecen a su ejecución; desaparecen del flujo del ejecutor las condiciones de ejecución específicas de ese comportamiento. Los dos campos serializados antiguos permanecen para inicializar su adaptador de compatibilidad, sin reescribir la escena. El antiguo booleano privado clicking se elimina: IsRunning se deriva de las ejecuciones activas.

No se cambia el algoritmo de reconocimiento, las 32 combinaciones, los bindings de controles, los objetos interactuables ni la resolución IK. El reconocedor sigue guardando sus datos antiguos, pero solo identifica el gesto; la ejecución y los permisos están en PoseActions.

## Archivos de producción

Modificados:

- `Assets/Scripts/Experiments/Hand3D/Hand3DPoseActions.cs`: asociaciones, contexto, ejecuciones, permisos, prioridades, cancelación y compatibilidad de eventos/capturas actuales.
- `Assets/Scripts/Experiments/Hand3D/Hand3DPoseComposition.cs`: peso de capa opcional al aplicar un perfil y recepción ordenada de contribuciones, después de resolver la orientación especial del gesto y antes del IK. Las funciones de normalización y el Frame permanecen exactamente iguales a la referencia corregida.
- `Assets/Scripts/Experiments/Hand3D/Hand3DInputController.cs`: comunica el origen de la intención de acción y solicita cancelación genérica ante foco/pausa/desactivación, incluso si R2 no estaba activo. No conoce tipos de acción, puertas ni documentos.

Creados, con metas generados por Unity en la copia aislada:

- `Assets/Scripts/Experiments/Hand3D/Hand3DGestureAction.cs`.
- `Assets/Scripts/Experiments/Hand3D/Hand3DPointClickAction.cs`.
- `Assets/Scripts/Experiments/Hand3D/Hand3DEventAction.cs`.
- `Assets/Scripts/Experiments/Hand3D/Hand3DProfileAction.cs`.

Creados bajo `Tools/Hand3DPhase6/`: runner, fixtures, probe para player temporal, documentación, referencias iniciales y resultados. No se crean assets de acciones de gameplay ni asociaciones persistentes en TestArmScene. La demostración combinada y los perfiles sintéticos existen solo en fixtures/copia aislada.

No se modifica Hand3DRig, ArmMovement/IK, ModeController, Recognizer, FingerState, InteractionCoordinator, detectores, puertas/documentos compartidos, herramientas de captura, renderizado ni máscara del pulgar. No se modifica ninguna escena, prefab, biblioteca de poses, perfil funcional, paquete ni configuración global.

## Contrato y configuración

El asset contiene parámetros, modos y prioridad. Cada Begin devuelve una Run propia, o null para acciones instantáneas. El ejecutor retira la Run al finalizar o cancelar; no restaura huesos desde acciones. Las contribuciones se reconstruyen cada actualización y pasan por el único compositor/commit existente.

Las asociaciones explícitas se configuran en Hand3DPoseActions.Bindings; los eventos nuevos con referencias de escena están allí. Las definiciones de acciones se guardan fuera de Editor y pueden reutilizarse entre gestos. No hay clase por gesto. Un gesto adicional requiere datos de reconocimiento, su binding y su asociación visual Universal, no cambios a los controladores.

El adaptador antiguo usa referencias de definiciones internas solo durante runtime. Conserva los UnityEvents originales y los invoca una vez al aceptar la solicitud. Una lista explícita vacía significa ninguna acción. El nuevo evento de binding es otro evento, invocado a lo sumo una vez por solicitud; no copia ni modifica listeners anteriores.

PointClick y Event funcionan en Legacy y Universal. Profile Pulse requiere Universal; se rechaza en Legacy sin escribir huesos por una vía alternativa. Una solicitud con acciones independientes ejecuta las elegibles; no es un secuenciador ni permite retriggers mientras alguna de sus acciones temporales siga activa.

La prioridad alta se compone después, con su máscara y peso; a igual prioridad se usa el orden configurado. La interacción conserva prioridad: adquisición cancela las acciones y bloquea nuevas durante ocupación/restauración. Como protección adicional se atenúa la contribución por el peso residual de Grab. No se cambia el orden Rig/detectores ni su posible latencia de un frame al adquirir.

## Políticas y diferencias explícitas

- PointClick conserva duración, amplitud y curva; es visual y no exige R2. Soltar R2 no lo cancela por sí solo.
- Las acciones que requieren control del brazo/World se cancelan cuando termina ese control. World exige R2 en Exploration y opt-in explícito en HandEdit. Las acciones dirigidas a objetos deberán validar su objetivo mediante Hand3DContactDetection dentro de CanBegin; esta entrega no implementa una campana/interruptor ni un selector ambiental.
- Cambiar gesto/modo, adquirir una interacción, perder foco, pausar, desactivar ejecutor/Input/rig/movimiento/reconocedor o cambiar backend cancela la ejecución. No se reanuda automáticamente.
- Antes, pausa con dt=0 podía congelar el clic y algunos cambios de modo/foco fuera de R2 no lo cancelaban. Ahora se retira la acción. El rig mantiene su regla de no realizar commit con dt=0: la imagen pausada puede conservar el último frame hasta reanudar, aunque la contribución ya esté cancelada.
- Las cancelaciones retiran el peso, sin modificar la combinación lógica. La siguiente composición restaura la pose vigente; las transiciones de modo conservan su interpolación propia.
- Los perfiles de acciones utilizan espacios explícitos. No se fuerza el adaptador especial de offset de muñeca de Exploration a actuar como una rotación local absoluta.

## Simplificación y coste

No se añade ningún componente obligatorio, objeto de escena, manager o bus. Hay cuatro tipos ScriptableObject (uno abstracto y tres concretos) en cuatro scripts nuevos. Contando el contexto, contrato Run, dos ejecuciones privadas y dos auxiliares del ejecutor, son diez clases nuevas de C#; no son diez componentes Unity. También hay una estructura de contribución y dos enums de configuración/entrada.

La infraestructura total crece respecto al antiguo clic único: no se presenta como reducción de líneas. La mejora verificable es que reconocimiento, asociación, ciclo de vida y comportamiento dejan de compartir un caso específico en un controlador; tres comportamientos distintos usan el mismo despacho/cancelación. Añadir otra acción instantánea o pulse de otro perfil no requiere código; otro comportamiento propio requiere su definición/Run y asignación, sin cambiar Input ni el ejecutor. No se añade un framework de animación general.

Siguen pendientes el adaptador de datos antiguos, el peso público PointClickWeight necesario para Legacy y la ruta visual Legacy. Se conservan deliberadamente hasta que el usuario apruebe retirarlos. No se inicia esa retirada ni ninguna fase posterior.

## Verificación

El runner toma la referencia de los archivos actuales y protege por hash todo Assets/Packages/ProjectSettings, salvo los tres scripts indicados y los cuatro scripts/metas nuevos. La escena y las capturas se exigen idénticas. En la copia de pruebas, las aserciones históricas que consultaban por reflexión el antiguo booleano privado clicking leen ahora IsRunning, sin cambiar expectativas; los fixtures originales en disco permanecen intactos.

Los casos nuevos ejercitan combinaciones, gestos nuevos por datos, acciones sin duración y con duración, múltiples acciones y reentrancia de eventos, curva numérica original, ambos modos y backends, cancelaciones, permisos, contactos reales puerta/documento, prioridad entre perfiles, muñeca absoluta sobre la orientación especial Point, restauración y 2.000 frames con acciones por backend. La auditoría anterior añade 6.000 frames principales por backend, ciclos de retorno, replay del quaternion inválido e inyección de NaN/Infinity/casi cero.

La build temporal ejecuta cuatro solicitudes PointClick + Event: Exploration/HandEdit × Universal/Legacy. Verifica pulso activo, finalización y exactamente cuatro invocaciones tanto del evento configurado como del original. El probe y Time.captureDeltaTime pertenecen solo a la escena temporal de build; no son configuración de producción.

Las pruebas son numéricas/de estado y la build usa nographics. Las instrucciones manuales completas están en README.md; la apariencia de las transiciones, manos, puertas, documentos y pulgar requiere Game. La aceptación visual previa del usuario no se reutiliza como aprobación visual automática de esta fase.

## Resultado final

Ejecución `Results/dd6aa453f4dd40dbadae78c78af8e2d1/` sobre las fuentes finales:

| Batería | Comprobaciones | Resultado |
|---|---:|---|
| Fase 0 | 611 | PASS |
| Fase 1 | 254 | PASS |
| Fase 2 | 668 | PASS |
| Fase 3 | 1.212 | PASS |
| Fase 4 | 940 | PASS |
| Fase 5 | 55.422 | PASS |
| Fase 6, 14 grupos | 92.495 | PASS |
| Total fases 0–6 | **151.602** | **PASS** |
| Auditoría adicional de quaternions | 30.996.285 aserciones numéricas | PASS |

La auditoría inspecciona 15.219.136 entradas TRS; cero errores Quaternion To Matrix, máximo desvío de norma al cuadrado local `9,19855e-8`, mundial `9,88195e-8`. Los contadores incluyen comprobaciones repetidas por articulación y frame; no representan millones de escenarios diferentes.

Build Windows Development: `Succeeded`, cero errores. Probe standalone: `PASS`; los eventos originales y configurados se invocan exactamente cuatro veces cada uno en cuatro solicitudes, con PointClick activo y posteriormente restaurado. No depende de assemblies de Editor para las acciones.

Los resultados e informes intermedios se conservan; el primer intento detectó que el fixture de Edit Mode debía invocar explícitamente OnDisable, como los fixtures anteriores. El resultado final no reutiliza ese intento. Los arneses históricos solo se adaptan dentro de la copia para sustituir la consulta al campo privado eliminado por IsRunning, manteniendo las aserciones originales.

Integridad final: escena, referencias, UnityEvents, biblioteca original, perfiles migrados, Prototype, componentes antiguos/compartidos, prefabs, paquetes y configuración global permanecen intactos. No se ejecuta Git. La fase se detiene aquí; Legacy continúa disponible.
