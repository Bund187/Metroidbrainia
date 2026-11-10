# Fase 1: detección de contacto 3D

Ámbito: únicamente el brazo experimental de TestArmScene. No se cambia el sistema de poses, IK, controles, bindings, motores compartidos ni composición del pulgar.

## Arquitectura

Una clase serializable nueva, `Hand3DContactDetection`, sin MonoBehaviour nuevo. Agrupa configuración de consulta, proyección mediante `Hand3DScreenProjection`, muestra visual de mano con offset, geometría de contacto y recorrido entre fotogramas, distancia física, tolerancias relativas a 1080p y raycast de obstáculos con exclusiones de jerarquía. Sus tres estructuras de datos representan muestra, recorrido y resultado; no son componentes.

Cada interactor conserva su propio historial de recorrido, colección, selección, adquisición y cancelación. Puertas conserva su desempate por contacto/distancia y reintentos sobre candidatos rechazados; documentos conserva su puntuación estricta original. No se fuerza una política común de gameplay. `Hand3DInteractionCoordinator` mantiene la exclusión y el orden de ejecución sigue siendo puerta 220, documento 225.

Puertas mantiene sus campos serializados originales y los transmite a la utilidad. Documentos tiene su propio bloque `detection`: cámara, punto de mano, RawImages, offset y capas. El bloque de TestArmScene se migra desde sus referencias reales de puerta, no desde defaults. El campo antiguo `contact` queda oculto y sin lecturas de runtime, como MonoBehaviour, para conservar su referencia serializada sin una dependencia de tipo al detector de puertas.

El preparador de Editor puede copiar una configuración inicial de puerta, pero no la necesita y no sobrescribe una configuración independiente existente. Las futuras modificaciones de cámara/capas/offset de puertas **no se propagan automáticamente** a documentos; configurar ambos en Inspector cuando corresponda. El display obtiene la proyección del propio interactor de documentos: es su único cambio visual, una línea que elimina la segunda dependencia de puertas.

## Comportamiento conservado

- Valores de escena: puertas 1,5 m / 100 px; documentos 0,9 m / 55 px. Los defaults históricos del código no sustituyen estos valores.
- Nueve bindings de puerta, tres de documento, todos sus datos y eventos intactos. Descubrimiento de puertas compatibles sin binding intacto.
- Puertas: pomo/trigger, exclusión de marco propio, seguimiento, mantenimiento de alcance/visibilidad/obstáculos, apertura manual/automática, bloqueo y necesidad de soltar tras ciertos intentos.
- Documentos: adquisición automática, alcance solo inicial, movimiento libre en HandHold, devolución, restauración lógica, sprite/texto/eventos y overlay actual.
- Prioridad de puertas cuando ambos tipos son elegibles en el mismo ciclo.

Cambio funcional solicitado: documentos pueden adquirirse con el detector de puertas deshabilitado o eliminado.

## Optimización y límites

Se obtiene una muestra de mano por ciclo de selección y se reutiliza entre candidatos y rechazos. Se descartan candidatos inválidos o sin alcance/visibilidad/contacto antes del raycast. La sincronización de Physics se realiza al necesitar la primera consulta del ciclo; no existe caché persistente. RaycastAll y sus exclusiones originales se conservan, sin buffers de capacidad limitada nuevos.

La información de depuración de un candidato ya descartado puede mostrar la causa barata de rechazo sin investigar obstáculos. Eso no cambia la admisibilidad del candidato. No se presentan los contadores de consultas como medición de tiempo de CPU o FPS.

Añadir otro tipo permite reutilizar las consultas sin depender de puertas, pero sigue requiriendo su binding/comportamiento/selección específicos. No hay un registro global, framework de acciones ni contrato universal nuevo. Se elimina duplicación de cálculo y dependencia funcional, no solo se trasladan decisiones de gameplay a otra clase.

## Ejecución reproducible

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase1/Run.ps1
```

Requiere Unity 6000.6.3f1, licencia y la copia completa marcada de fase 0 en `%TEMP%/MetroidbrainiaPhase0-20261109` (o `-ValidationProject`). Nunca apunta a producción como proyecto Unity. No usa Git ni instala paquetes.

El runner comprueba los hashes de los archivos protegidos de la referencia de 1.981 archivos y conserva las fases 0 y 0.5. Solo permite cuatro scripts modificados, el helper y su meta nuevo, y la escena. En la escena exige igualdad textual con la referencia al retirar exclusivamente el bloque nuevo de configuración. La meta del helper procede de la importación de Unity temporal, no se escribe manualmente.

La migración se guarda primero en una copia de escena temporal. Solo se incorpora su bloque `detection` a la escena original, manteniendo los demás campos/fileIDs. No ejecutar Prepare ni recapturar poses para aplicar esta fase.

Se ejecuta el arnés original de 611 comprobaciones sin cambiarlo. Después se comparan los detectores actuales con snapshots anteriores en escenas sintéticas. Los snapshots solo cambian de nombre y redirigen consultas a contadores transparentes; se adjuntan únicamente en la copia temporal. Las pruebas no entran en Assets de producción.

Cada intento deja su log bajo `Results/<runId>`. Un fallo de compilación o del arnés no es una regresión aprobada. Consultar REPORT.md para la ejecución final y el alcance de sus resultados.

## Archivos

Modificados:

- `Assets/Scripts/Experiments/Hand3D/Hand3DDoorInteractor.cs`
- `Assets/Scripts/Experiments/Hand3D/Hand3DDocumentInteractor.cs`
- `Assets/Scripts/Experiments/Hand3D/Hand3DDocumentDisplay.cs` (proveedor de proyección)
- `Assets/Editor/Experiments/Hand3D/Hand3DDocumentEditors.cs`
- `Assets/Scenes/TestArmScene.unity` (solo configuración independiente añadida)

Creados:

- `Assets/Scripts/Experiments/Hand3D/Hand3DContactDetection.cs` y `.cs.meta` generado por Unity.
- `Tools/Hand3DPhase1/`: runner, arnés comparativo, contadores de snapshots, cuatro snapshots `Before/*.cs.txt`, documentación y evidencias `Results/`.

No se elimina ningún archivo de producción. No se inicia fase 2.
