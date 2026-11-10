# Fase 2: sesiones de interacción de la mano 3D

Solo modifica cinco scripts del brazo experimental. No cambia escenas, campos serializados, capturas, poses, IK, controles, renderizado, bindings ni motores compartidos. Las fases 0, 0.5 y 1 se conservan intactas. Las pruebas manuales satisfactorias de fase 1 comunicadas por el usuario son la referencia; no se presentan como validación manual de fase 2.

## Responsabilidades y problemas comprobados

Antes, `Hand3DInputController.Start/Update/EndArmMode` buscaba, consultaba y cancelaba directamente los detectores de puertas y documentos. Un tercer tipo requería añadir otra dependencia a Input. `Hand3DInteractionCoordinator` guardaba solo un owner y comprobaba el peso de pose con búsquedas de componente en cada consulta. `Hand3DDoorInteractor.Cancel` y `Hand3DDocumentInteractor.Cancel` gestionaban por separado la limpieza y la liberación visual. El booleano privado de agarre de puerta duplicaba la propiedad de sesión.

Input bloqueaba acciones nuevas consultando ambos tipos, pero `Hand3DPoseActions.TryExecute` no conocía la ocupación y podía invocarse directamente. Tampoco había cancelación de PointClick al adquirir un Grab. Esto es un conflicto comprobable en código: `Hand3DRig.UpdateRig` aplica PointClick al índice antes de mezclar la pose temporal. No afirmamos que la diferencia fuera perceptible en todos los pesos.

## Solución mínima

Se amplía el coordinador existente. No se añade ninguna clase de producción, componente, asset ni archivo de runtime. En el mismo archivo se declaran una interfaz de dos métodos, `IHand3DInteractionSession`, y un enum de estado. La interfaz permite devolver control al adaptador activo sin conocer su clase: limpiar su objeto y consumir movimiento cuando corresponda.

| API del coordinador | Uso |
|---|---|
| Owner / Owns / IsOccupied | Propiedad exclusiva actual; IsOccupied indica interacción activa, no mezcla visual residual |
| State | Free, Door, Document, Other o Restoring; se deriva de owner y peso de pose, sin temporizador nuevo |
| IsRestoring | No hay propietario, pero la capa visual aún tiene peso |
| CanExecuteGestureAction | Libre, coordinador habilitado y sin restauración en curso |
| IsMovementRestricted / ProcessMovement | La sesión de puerta consume movimiento; documento permite movimiento libre |
| CanAcquire / TryAcquire | Exclusión, referencias locales, componentes habilitados y espera a restauración |
| Release / CancelSession | Limpieza específica una vez, liberación visual y del contacto, segura ante repetición y callbacks reentrantes |
| EndArmSession | Lo anterior más notificación local de fin del control a puerta, incluso si su intento ya había terminado |

`ArmControlEnded` es un callback C# local del mismo coordinador, no un registro global ni un bus. Es necesario porque una puerta puede haber liberado la mano y conservar feedback/intento consumido hasta soltar R2. Puerta se suscribe en su ciclo de vida y se desuscribe al deshabilitarse; documentos no necesita esa notificación adicional.

El coordinador cachea pose, acciones, movimiento y rig al inicializar. Su comprobación LateUpdate valida solo una sesión activa ante desaparición/desactivación del propietario o de los componentes que la aplican. No recorre objetos ni busca componentes por fotograma. El adaptador sigue validando las condiciones específicas de su objeto.

Input conserva sus bindings, bloqueo de controles, modo y locomoción. Solo expresa fin del control y movimiento integrado al coordinador. No referencia clases de puertas/documentos. PoseActions consulta una sola política de ocupación, también para llamadas directas a TryExecute. Puertas y documentos solicitan al coordinador el tipo visual y conservan la detección de fase 1.

## Política de acciones y restauración

Al adquirir una sesión se cancela PointClick, se reinician su peso y progreso y se solicita GrabDoor o GrabDocument. No se reanuda automáticamente el clic interrumpido. Se bloquean acciones nuevas durante la sesión y la mezcla de salida. **Diferencia intencionada:** antes podía sobrevivir un clic previo o iniciarse una acción durante la restauración; ahora se evita esa superposición. No se cambia el ejecutor ni las capturas/curvas ni la lógica de acciones normales cuando no hay interacción.

Al liberar se pone owner a null antes de llamar al adaptador y se bloquea la adquisición durante ese callback. Un finally solicita la salida de la capa visual y libera el contacto de movimiento de puerta. El método no escribe dedos: el rig vuelve a la combinación lógica vigente. Si el usuario cambia los dedos mientras sostiene un objeto, conserva esos cambios al soltar.

Restoring representa la mezcla de la mano. La devolución gráfica del documento sigue perteneciendo al binding/display existente y puede continuar cuando esa mezcla ya ha terminado; no se prolonga artificialmente la ocupación ni se impide adquirir otra interacción por ese motivo. La disponibilidad del mismo documento sigue bloqueada por su propio estado releasing. Esto conserva la regla anterior, que esperaba solo al peso de pose.

## Interrupciones y comportamiento preservado

- R2/RMB: Input finaliza la sesión y luego el control del brazo. Puerta limpia también feedback/intento; documento inicia la devolución existente.
- Cambio a HandEdit desde Input: finaliza primero el control y conserva la espera a soltar si R2 sigue pulsado. Los cambios directos de modo siguen siendo comprobados por los adaptadores.
- Pausa por dt=0: Input usa su salida existente; la restauración visual queda congelada hasta que el rig vuelva a avanzar. Pausa de aplicación/pérdida de foco conservan waitForRelease.
- El bloqueo de controles del jugador durante pausa/foco mantiene su orden previo: EndArmMode finaliza brazo/sesión, y el bloqueo se actualiza en el siguiente Update válido o se restaura en OnDisable. No se cambia esta regla ambigua para introducir otra política de pausa.
- Deshabilitar Input conserva su restauración de acciones privadas, cursor y controlador. Los motores y los componentes visuales compartidos no se modifican.
- Desactivar/destruir objeto o adaptador cancela la sesión por las rutas existentes y/o la validación del propietario. Deshabilitar coordinador cancela expresamente; deshabilitar pose/movimiento/rig impide mantener o adquirir una sesión. Si el rig está deshabilitado, la mezcla pendiente espera a su reactivación; no se fuerza una animación nueva.
- Se conserva prioridad puerta 220/documento 225, alcance, barridos, apertura manual/automática, bloqueo, intento consumido hasta soltar, documento libre durante sujeción y todas sus referencias visuales.

## Orden de actualización comprobado

Input conserva -50; Hand3DRig conserva 200; el coordinador usa 215 para validar la sesión antes de puerta 220/documento 225. No se reorganizan los demás Update/LateUpdate.

La adquisición sigue ocurriendo después de UpdateRig. Las pruebas comprueban que el peso sigue a cero al adquirir y sube al ejecutar el siguiente paso del rig. Por tanto la aplicación visual del Grab espera al siguiente ciclo, como antes. Cancelar PointClick en adquisición tampoco deshace los huesos ya escritos por el rig de ese fotograma. Esto es un desfase estructural comprobado; su impacto perceptivo requiere Game. Queda para la fase de composición; no se fuerza una segunda aplicación del esqueleto ahora.

## Archivos modificados

- `Assets/Scripts/Experiments/Hand3D/Hand3DInteractionCoordinator.cs`
- `Assets/Scripts/Experiments/Hand3D/Hand3DInputController.cs`
- `Assets/Scripts/Experiments/Hand3D/Hand3DDoorInteractor.cs`
- `Assets/Scripts/Experiments/Hand3D/Hand3DDocumentInteractor.cs`
- `Assets/Scripts/Experiments/Hand3D/Hand3DPoseActions.cs`

Creados únicamente bajo `Tools/Hand3DPhase2/`: runner, arnés, snapshots Before de esos cinco scripts, inventario de hashes anterior a la fase, documentación y resultados. No se elimina ningún archivo de producción. TestArmScene debe permanecer byte a byte igual.

## Repetir la validación

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase2/Run.ps1
```

Requiere la copia completa marcada de fase 0 (`%TEMP%/MetroidbrainiaPhase0-20261109` o `-ValidationProject`), Unity 6000.6.3f1 y licencia. No instala paquetes ni usa Git. Nunca lanza Unity sobre producción. Los archivos before-files.json se comparan antes y después, permitiendo solo los cinco scripts; Assets/Packages/ProjectSettings no admite añadidos inesperados. Las fases anteriores se verifican por hash.

Los arneses aceptados no se editan. En la copia temporal, el fixture de fase 0 sustituye la inyección de dos campos privados eliminados por la del coordinador. El de fase 1 consulta IsGrabbing en lugar del booleano eliminado y adjunta a los snapshots antiguos una copia renombrada del coordinador anterior. No cambian las expectativas ni se añaden excepciones a sus aserciones. Estos ajustes no afectan a producción.

El arnés de sesiones reutiliza las fábricas sintéticas de fase 1 y ejecuta los componentes reales en pasos de Editor; simula entradas efectivas, contacto y finalización de animaciones. Las pruebas de pausa no reproducen un bucle Game completo ni un dispositivo físico. El JSON y los logs distinguen este alcance de validación visual. Consultar REPORT.md para el resultado final.

Se detiene aquí. No comienza fase 3.
