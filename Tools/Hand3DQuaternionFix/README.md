# Corrección numérica de fase 5 — quaternions

No se inicia la fase 6. La única modificación de runtime está en `Hand3DPoseComposition.cs`. Se conservan la escena actual, sus referencias, el backend seleccionado por el usuario, capturas, biblioteca Editor, copia runtime, IK, controles y componentes compartidos.

## Causa comprobada y límites de reproducción

`Frame.UpdateWorld` construía `Matrix4x4.TRS` directamente con `rotations[i]`, sin garantizar longitud unidad. Tampoco normalizaba `worldRotations[i] = parentWorld * local`. El IK reutiliza esos valores: `FromToRotation(...) * worldRotation`, seguido de `SetWorldRotation`, que vuelve al espacio local mediante `Inverse(parentWorld) * desiredWorld`. La muñeca se reconstruye además como `Inverse(parentWorld) * cameraRotation * wristCamera`. Todas estas operaciones en float pueden conservar o amplificar desviaciones; ninguna establecía un límite común de normalización antes de TRS.

Las capturas funcionales ya tienen redondeo normal de punto flotante: máximo medido `abs(normSquared-1) = 1,817145385e-7`. Esto no demuestra una captura corrupta ni justifica reescribirla. El primer cruce de `1e-6` observado en el estrés original fue una rotación mundial de `wrist.r` dentro de `UpdateWorld`, llamado por `SetWorldRotation` desde la segunda corrección de `SolveUniversal` (forearm). La traza está en el informe de reproducción.

La prueba natural de 6.000 frames por backend detectó desviaciones, pero **no reprodujo por sí sola el mensaje de consola**. Para reproducir el fallo concreto se introdujo, únicamente en memoria dentro de la copia aislada, el quaternion redondeado registrado en Game `(-0.008593, -0.204440, 0.078868, 0.975667)` en la entrada de rotación mundial del buffer; después se ejecutaron solver, commit y movimiento. Su norma al cuadrado es aproximadamente `1,00001581`.

La reproducción con código anterior produjo **80 errores Quaternion To Matrix**, máximo desvío previo a TRS `4,71139e-5` y desvío mundial `9,43480e-5`. Legacy no produjo ese error en el estrés. Es una reproducción numérica del dato observado, no una afirmación de haber repetido la entrada exacta de ratón del usuario en Game.

El fallo pertenece al contrato de todo el buffer, no a una captura o un único dedo. La muñeca es especialmente sensible por la conversión local/mundial repetida. No hace falta que se acumulen miles de frames: el compositor reconstruye la referencia cada frame y la desviación puede aparecer dentro de una misma evaluación. El número exacto de frames y valores depende de la trayectoria, pose y redondeo de operaciones.

## Corrección

- `Frame.UpdateWorld`: límite único de validación/normalización para padre externo, cámara y orientación objetivo de muñeca; normaliza cada rotación local **antes de TRS** y cada producto mundial antes de que el solver lo reutilice.
- `NormalizeRotation`: comprueba norma al cuadrado en double; rechaza NaN, Infinity y norma al cuadrado inferior a `1e-12`. Normaliza valores finitos recuperables, incluso cuando el cálculo de norma en float desbordaría. No usa `Quaternion.identity` como sustitución.
- Constructor: valida las rotaciones y neutrales activos de los perfiles antes de Slerp/Inverse, sin modificar el asset.
- Entradas de perfiles, endpoints y offsets: comprueban finitud/magnitud antes de interpolar, también si un script contamina datos después de inicializar. Esta comprobación no normaliza ni modifica capturas; evita que una interpolación enmascare un NaN antes del límite común.
- `Frame.SetWorldRotation`, `Copy`, `Reset`, `Apply` y `Endpoints`: conservan el origen del dato para el diagnóstico. No cambian fórmulas de poses ni resolución IK.

Una rotación irrecuperable lanza un error con articulación/ruta, fase, origen y asset. La ruta de rechazo ya existente en Hand3DRig restaura y vuelve a Legacy; no se añade un logger cada frame. El test de contaminación runtime comprueba diagnóstico único y fallback. La normalización cambia la magnitud del quaternion, no la orientación que representa; su efecto numérico en posiciones/orientaciones se comprueba también con las regresiones.

## Pruebas

Runner existente ampliado solo para pruebas: `Tools/Hand3DPhase5/Run.ps1`.

Reproducción anterior:

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase5/Run.ps1 -Phase5Only -QuaternionAudit -ReproduceQuaternionError`

Validación corregida y regresiones:

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase5/Run.ps1 -QuaternionAudit -BuildPlayer`

La instrumentación se inyecta exclusivamente en la copia temporal; no hay callbacks de auditoría en el runtime de producción. La reproducción anterior usa `Before/Hand3DPoseComposition.cs.txt` únicamente en esa copia. No instala assets ni cambia la escena original.

`QuaternionValidation.cs` ejecuta 6.000 frames por backend, movimientos horizontal/vertical/diagonal con diferentes magnitudes, objetivos extremos, cambios de las 32 combinaciones, ambos modos, GrabDoor/GrabDocument, liberaciones, retorno y 20 ciclos de ida/vuelta. Comprueba finitud, longitud de segmentos, escalas y orientación de muñeca. Inspecciona cada rotación local antes de TRS y cada mundial; exige desviación inferior a `1e-6` tras la corrección. Añade replay del quaternion registrado, rechazo de NaN/Infinity/cero/casi cero, normalización de entrada finita extrema y diagnóstico único con retorno a Legacy.

Las regresiones de fases 0–5 incluyen PointClick, interacciones reales y comparación Legacy/Universal. Son pruebas matemáticas y de estados ejecutadas con Unity, no una aprobación visual de Game ni una reproducción completa de todas las entradas del jugador.

## Comprobación manual

1. En TestArmScene, limpiar Console y activar Universal en `Hand3DRig → Pose System`, conservando la referencia runtime existente.
2. En Exploration, mantener botón derecho y mover varios minutos: desplazamientos pequeños/lentos, rápidos, horizontales, verticales, diagonales y extremos. La Console no debe mostrar Quaternion To Matrix ni fallback a Legacy.
3. Soltar: comprobar retorno sin deriva ni cambio perceptible de postura. Alternar OK/Point y dedos durante movimiento y ejecutar PointClick.
4. Cambiar a HandEdit y comprobar inspección, giros y gestos; volver a Exploration.
5. Abrir puertas normales/bloqueadas y recoger/mover/devolver documentos. Comprobar especialmente orientación de muñeca, contacto con pomo y alineación del pulgar sobre el papel.
6. Volver a Legacy y repetir el movimiento para comparar. No se han modificado sus fórmulas ni capturas.

La validación visual real sigue pendiente. No se afirma ausencia de diferencias perceptibles sin esa comprobación. Otros errores de posición/escala o datos externos corruptos quedan fuera de esta corrección; no se han observado en el estrés y no se resuelven inventando una pose. Una configuración con rotaciones irrecuperables se rechaza explícitamente.

## Archivos e integridad

Modificado en runtime: `Assets/Scripts/Experiments/Hand3D/Hand3DPoseComposition.cs`.

Modificados para ejecutar pruebas: `Tools/Hand3DPhase5/Run.ps1` y `Tools/Hand3DPhase5/Phase5Validation.cs`. El fixture conserva una instantánea JSON del perfil temporal antes de recargar la escena y lo recrea si Unity descarga el objeto nativo. Así los casos con datos personalizados no caen silenciosamente en el asset predeterminado. No es una modificación de las herramientas de captura.

Creados: esta documentación, `QuaternionValidation.cs`, referencia anterior del compositor/escena y manifiesto `before-files.json` bajo `Tools/Hand3DQuaternionFix/`. Logs y resultados en `Tools/Hand3DPhase5/Results/<runId>/`.

El manifiesto de esta corrección toma como referencia el estado actual del proyecto, no intenta revertir las ediciones del usuario posteriores a fase 5. El runner protege por hash todo Assets/Packages/ProjectSettings salvo el compositor y exige igualdad exacta de la escena actual. No se ejecuta Git.

## Resultados guardados

- Código anterior, replay: `Tools/Hand3DPhase5/Results/9ff7cad6c07244b4b1257c99c2f74730/quaternion-results.json`: 80 errores reproducidos. Los 6.000 frames naturales por sistema no produjeron el aviso; los errores corresponden al replay explícito del dato de Game.
- Ejecuciones intermedias `47ac10197c964fb2bebc2b62498ed279` y `c7ba5424ca494ca68fad2ccf87a84c87`: 0 errores TRS en el estrés y máximo desvío `9,19855e-8`, pero falló la prueba añadida de contaminación tras inicializar. El diagnóstico posterior mostró que la recarga había sustituido la copia temporal por el asset predeterminado. Se corrigió el arnés y el caso aislado terminó con `QUICK PASS`, incluyendo diagnóstico único y fallback. Las ejecuciones intermedias no se consideran una aprobación final.
- En esa ejecución pasan las **59.107 comprobaciones previas de fases 0–5**. El contador del estrés incluye cada rotación local/mundial de cada reconstrucción del buffer; no representa millones de escenarios de gameplay diferentes.
- Comparación Legacy/Universal tras corregir: diferencia máxima de mano `1,36961e-6` unidades, dedos `1,96966e-6`, error factible IK `1,04606e-6`; diferencia angular medida en dedos y muñeca `0` grados. Se mantiene la diferencia de codo documentada en fase 5; no se cambia su preferencia.

### Ejecución final

`64f5c71dc1cf496783e27a52405935ce`: **59.107 regresiones superadas** y estrés numérico superado, incluido el caso de contaminación con diagnóstico único. Se inspeccionaron **15.219.136 entradas TRS**, con **0 errores Quaternion To Matrix**, máximo desvío local `9,19855e-8` y mundial `9,88195e-8`. Se ejecutaron 6.000 frames principales por backend, más retorno y ciclos repetidos. El contador de comprobaciones numéricas de esta ejecución es 30.996.285.

El error máximo del destino factible IK en la batería final es `9,90227e-7` unidades. Los máximos comparativos de mano/dedos y ángulos permanecen como se indica arriba. Esta ejecución usa el arnés corregido para conservar los perfiles personalizados al recargar la escena.

Build Windows Development aislada: `Succeeded`, 0 errores. Probe standalone: `PASS`. Los logs finales no contienen el error Quaternion To Matrix. Verificación final de hashes: el único archivo diferente en Assets/Packages/ProjectSettings es `Hand3DPoseComposition.cs`; escena, selección actual del usuario, bibliotecas, capturas y archivos compartidos permanecen idénticos a la referencia de esta corrección.
