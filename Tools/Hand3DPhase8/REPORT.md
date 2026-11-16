# Fase 8 — limpieza y consolidación final

## Resultado

El flujo habitual es ahora **Pose Editor → guardar biblioteca → seleccionar IDs → revisar → confirmar publicación selectiva → asignar expresamente si el ID es nuevo → probar en Universal**. Se amplían la ventana y el exportador existentes; no se crea otra ventana, componente, clase de producción ni sistema arquitectónico.

El runtime no se reescribe: sus 24 fuentes C# se conservan byte a byte, incluidos solver, quaternions, acciones, física y máscara optimizada. Legacy sigue disponible. Universal es el procedimiento documentado para desarrollar poses nuevas, sin cambiar automáticamente el backend guardado en TestArmScene ni migrar el gameplay del usuario.

## Revisión de arquitectura

Inventario definitivo por cada uno de los 35 scripts, con responsabilidad, dependencias, uso, duplicación, datos Legacy, elementos obsoletos y decisión: **INVENTORY.md**. `script-references.json` registra GUID/tipos y referencias YAML; las herramientas automáticas de Editor y utilidades C# sin referencia YAML también se han revisado. Se consultaron los informes de fases 0, 0.5 y 1–7, además de QuaternionFix, y sus fixtures.

Se confirma una única autoridad lógica de dedos, reconocimiento separado de acciones, un único commit Universal, Input ajeno a tipos de interactuables, detector común sin dependencia runtime documentos→puertas, acciones que aportan contribuciones y no escriben huesos, y exclusividad/coordinación central. La captura Legacy y Universal representan fuentes alternativas; no se ejecutan dos backends de poses simultáneamente.

No se halló otra eliminación runtime segura que justificase cambiar serialización o respaldo. Los descartes/sincronizaciones de contacto y la actualización de geometría animada ya se revisaron en fase 7; se preservan. Los campos inertes concretos y las rutas aún activas están distinguidos en el inventario. No se utiliza «no tiene llamadas C#» como criterio de eliminación.

## Qué se consolida y qué se retira

- La revisión y el commit selectivos comparten **BuildSelected**. La revisión crea un candidato descartable y no modifica el destino. El commit vuelve a validarlo antes de registrar Undo y guardar solo ese asset.
- Se elimina la vía global de copiar todos los perfiles no asignados, que podía incluir pruebas sin selección individual. Se retiran `includeUnassignedRuntime` (preferencia de la ventana) y los argumentos `includeUnassigned` de CreateCopy/ExportDialog, y la rama correspondiente. Solo estaban utilizados por ese botón de Editor; los fixtures usan la copia funcional sin ese argumento. No son campos de una escena/biblioteca ni listeners de UnityEvent.
- La copia inicial de 13 perfiles funcionales permanece en un apartado avanzado. Los perfiles adicionales siempre requieren selección explícita. Los nombres OK_Test_Capture, Exploration_FullBody_Test y Partial_Capture_Test no se seleccionan automáticamente; un perfil de prueba válido sí puede publicarse cuando el usuario lo marque.
- Los Inspectores distinguen capturas de respaldo Legacy del procedimiento Universal. Los límites/velocidad/signo de inspección, estados/pesos/offsets de interacción y nombres/combinaciones/eventos se identifican como compartidos. No se ocultan ni retiran botones todavía usados.
- Se corrige el aviso desactualizado de Pose Editor que afirmaba que Play conservaba siempre el runtime Legacy.

No se elimina ningún archivo, componente, dato de captura, método de captura Legacy ni campo serializado runtime. No se elimina una dependencia del runtime fingiendo que estaba muerta; el enlace 2D previo en DoorInteractor sigue delimitado y sin ampliaciones.

## Publicación y conservación de datos

El destino debe existir bajo Assets/Experiments/Hand3D/RuntimePoses y la fuente debe ser una biblioteca Editor guardada. La revisión muestra ADD, UPDATE o UNCHANGED para cada ID seleccionado; el botón de confirmación presenta de nuevo la revisión y permite Cancel. No se publica durante Play ni se crea/sobrescribe un destino ajeno.

La operación valida IDs únicos, rig y referencias persistentes, cámara/cadena IK reales, catálogo de joints, canales/máscaras, valores/espacios, neutral de muñeca, contexto y completitud de las asignaciones. Nuevos perfiles vacíos/sin influencia se rechazan; los no-op funcionales ya migrados se conservan como placeholders. Las bases relativas de muñeca y los grupos de companions configurados deben ser compatibles; perfiles/asignaciones ausentes se rechazan antes del commit. No se inventan dependencias por nombre ni se copian companions sin selección.

El merge preserva IDs, orden y contenido de perfiles no seleccionados, asignaciones, endpoints, gestos y grupos de interacción. Solo añade al catálogo joints realmente referenciados por la selección, validados contra el rig; no migra en bloque toda la biblioteca Editor. Los IDs nuevos quedan inactivos hasta asignarlos explícitamente en el Inspector runtime o en un profileId de acción. Esto evita que publicar datos modifique gameplay automáticamente.

Undo/Redo recupera el runtime completo. Publicar guarda el destino; después de Undo/Redo debe guardarse de nuevo con el botón del Inspector runtime, como se explica en la guía. No se ejecuta SaveAssets global. HandPoses.asset, TestArmRuntimePoses.asset y la escena originales **no se han modificado** durante la entrega: las pruebas publican sobre copias nuevas exclusivamente en TEMP.

## Estado preciso de Universal y Legacy

La biblioteca runtime funcional conserva 13 perfiles, 22 referencias y todas las asignaciones actuales: extremos de cinco dedos, OK/Point, bases Exploration/HandEdit, offsets de muñeca y grupos GrabDoor/GrabDocument. Los placeholders parciales vacíos originales son intencionales. Las acciones temporales y sus parámetros conservan la configuración de fase 6; no se añaden mecánicas.

La prueba altera antes del arranque valores de Extended/Flexed, overrides, rotaciones/posición de modos, neutrales/agarres y duraciones Legacy; Universal produce el mismo recorrido en 32 máscaras, dos modos y estados de agarre/movimiento. **Esto demuestra independencia de evaluación respecto de esos valores, no independencia completa del arranque.** Rig.Initialize todavía exige bindings, referencias y flags válidos para preparar Legacy; una prueba negativa confirma que retirar extendedCaptured hace fallar la inicialización incluso seleccionando Universal.

Por eso se mantienen esas estructuras y todas las herramientas de recuperación. Los componentes mixtos no se etiquetan como «solo Legacy» cuando también producen estado, peso, límites o geometría compartidos. La inicialización independiente, retirada de fallback y migración definitiva quedan pendientes de autorización y pruebas específicas, detalladas en INVENTORY.md. No se inicia otra refactorización para eliminar esa frontera durante esta fase.

## Simplicidad antes/después

| Medida/responsabilidad | Antes | Después |
|---|---|---|
| Scripts C# runtime | 24 | 24, mismos bytes |
| Scripts C# Editor | 11 | 11, cinco modificados |
| Clases/componentes/ventanas de producción nuevos | — | 0 |
| Publicar una edición | Exportar nueva copia funcional y reasignar, o edición manual de datos | Selección/revisión/confirmación sobre asset existente con Undo |
| Incluir perfiles no asignados | Casilla global en copia nueva | Selección individual de IDs |
| Fuente de verdad de captura Universal | Pose Editor + biblioteca | Igual, con publicación selectiva integrada |
| Capturas Legacy | Conservadas, mensajes ambiguos | Conservadas, límites de uso explícitos |
| Campos serializados runtime retirados | — | 0 |
| Preferencias de ventana retiradas | — | 1, casilla global antigua |
| Parámetros de API retirados | — | 2 bool de exportación masiva; llamadores revisados |
| Dependencias/frameworks/managers nuevos | — | 0 |

Los cinco scripts Editor pasan de 913 a 1.056 líneas (+143; detalle en code-size.json). No se presenta como reducción de código: se añade validación y publicación reversible que antes no existía. La simplificación está en el trabajo del desarrollador y en impedir dos vías incompatibles de incluir perfiles adicionales. No se hacen cambios de formato ni refactors cosméticos del runtime.

## Archivos entregados

Modificados:

- Assets/Editor/Experiments/Hand3D/Hand3DRuntimePoseExport.cs.
- Assets/Editor/Experiments/Hand3D/Hand3DPoseEditor.cs.
- Assets/Editor/Experiments/Hand3D/Hand3DEditors.cs.
- Assets/Editor/Experiments/Hand3D/Hand3DModeEditors.cs.
- Assets/Editor/Experiments/Hand3D/Hand3DDoorEditors.cs.
- Assets/Scripts/Experiments/Hand3D/README.md, guía principal actualizada.

Creados exclusivamente bajo Tools/Hand3DPhase8: Run.ps1, Phase8Validation.cs, README.md, REPORT.md, INVENTORY.md, MANUAL.md, manifiestos before-files/historical-files, inventario script-references, code-size, snapshots Before y resultados/logs/manifiestos por ejecución. Las clases del arnés viven fuera de Assets original.

Retirados: ningún archivo. Solo la preferencia/argumentos/rama masiva indicados. No se crea ni se edita ninguna meta, escena, prefab, biblioteca o perfil de producción.

## Configuración y ampliación en Unity

La guía principal contiene el procedimiento completo de gesto/combinar dedos, perfiles completos/parciales, contexto HandEdit, preview, captura, guardado, publicación, asignaciones y acciones. También detalla cómo añadir un adaptador automático usando ContactDetection/Coordinator/IHand3DInteractionSession y cómo distinguirlo de una acción de gesto sobre un objetivo. No se introduce otro servicio de selección ambiental ni una dependencia a Input por cada tipo nuevo.

Abrir Tools → Metroidbrainia → Hand 3D → Pose Editor, asignar rig y HandPoses.asset; editar/capturar la copia; Save Library; elegir target y marcar IDs; Validate and review; Confirm selective publication y confirmar el diálogo; asignar IDs nuevos si procede; validar el runtime; elegir Universal y entrar en Play. Los originales existentes no requieren Prepare, recaptura ni publicación para seguir funcionando.

## Evidencias y límites

Las ejecuciones intermedias se conservan: `371bc0cac49b4b96a64532544e6fb53a` (11.931 comprobaciones iniciales); `3abb150bca52496fac4efa58cde49ce3` (regresión acumulada/auditoría/build antes de retirar la casilla masiva, 11.937 de consolidación); `c86ebdc776e94193bc901dbabab66015` (11.942, añade ejecución real de perfil publicado con Profile Pulse/Event y compatibilidad Legacy). La última ejecución final se identifica al terminar debajo; no se suman todas las repeticiones como casos distintos.

La comparación de trazas de fase 7 conserva 2.816 comprobaciones y tolerancias: máximo error de posición 0 unidades, máximo ángulo 0,0000115812° sobre quaternions serializados. Los contadores de rendimiento conservan las optimizaciones; esta fase no promete mejoras adicionales de FPS. El contador de GC sigue sin ser fiable (control positivo), por lo que GC Alloc sigue N/D.

El probe de build funciona en nographics y no valida oclusión/lectura/clicks de UI. La aceptación visual sigue pendiente en MANUAL.md, limitada a funcionalidades existentes, sin exigir pausa/localización. Hay avisos internos JobTempAlloc al cerrar Unity después de la build, ya documentados en fase 5; el origen no está determinado y no se atribuye a una mejora/regresión medida de esta entrega. La build registra cero errores. El caso negativo de flags Legacy emite deliberadamente el diagnóstico de captura inválida y exige rechazo; no es un fallo inesperado del juego.

La implementación se detiene en fase 8. No se elimina Legacy ni se inicia otra migración.

## Ejecución final y resultados

**Results/33de532165ff48489fd0f5a66c0bf49a/** contiene fuentes definitivas, cinco grupos de consolidación PASS, JSON/logs de todas las fases, auditoría, build, probe y hashes. Las fuentes entregadas coinciden con tested-files.json; `final-integrity.json` confirma el cierre. La comparación de trazas se ha repetido sobre esta misma ejecución con Compare.ps1 histórico, que permanece intacto.

| Batería | Comprobaciones | Resultado |
|---|---:|---|
| Fase 0 | 611 | PASS |
| Fase 1 | 254 | PASS |
| Fase 2 | 668 | PASS |
| Fase 3 | 1.212 | PASS |
| Fase 4 | 940 | PASS |
| Fase 5 | 55.422 | PASS |
| Fase 6 | 92.495 | PASS |
| Subtotal 0–6 | 151.602 | PASS |
| Fase 7, arnés vigente | 3.472 | PASS |
| Comparación de trazas anterior/posterior | 2.816 | PASS |
| Referencia acumulada hasta fase 7, repetida | **157.890** | **PASS** |
| Fase 8 | **11.944** | **PASS** |
| Total acumulado con fase 8 | **169.834** | **PASS** |
| Auditoría adicional de quaternions | 26.032.513 aserciones numéricas | PASS |
| Development Build Windows | Succeeded, 0 errores | PASS |
| Probe standalone | PASS, cuatro eventos originales y cuatro configurados en cuatro solicitudes | PASS |

Los contadores incluyen comparaciones repetidas por hueso/frame y no equivalen a 169.834 escenarios distintos. La batería de fase 8 añade publicación selectiva y copia inicial sin fugas, revisión sin mutación, guardado/recarga, Undo/Redo con recuperación de archivo, preservación de perfiles/asignaciones/eventos/escena, rechazos atómicos, bases/companions, reproducción con Profile Pulse + Event, rechazo de Pulse/aceptación de Event en Legacy, separación de ensamblados Editor/player e independencia de valores Legacy con frontera de inicialización comprobada. El gesto PointClick, 32 máscaras, modos, IK, puertas/documentos, transiciones, cancelaciones, overlay y lifecycle conservan además sus baterías históricas.

Auditoría final: **12.737.250 entradas TRS**, cero errores Quaternion To Matrix y cero errores en ambos backends. Desviación máxima local `9,19855338565867e-8`, mundial `9,881951257284527e-8`. La validación/normalización y sus tolerancias permanecen intactas.

Integridad final: **2.008 archivos originales sin cambios**, 24 scripts C# runtime byte a byte idénticos y exactamente los seis archivos de código/guía autorizados modificados. `final-integrity.json` incluye hashes completos de Prototype.unity, TestArmScene.unity, HandPoses.asset, TestArmRuntimePoses.asset, FirstPersonArmController.cs, ArmView.cs, DoorHandInteractable.cs y DocumentHandInteractable.cs. El manifiesto completo también protege todos los prefabs, metas, Starter Assets, paquetes y ProjectSettings. La verificación histórica protege fases 0, 0.5 y 1–7 y QuaternionFix.

No se realizaron operaciones de Git ni se modificó gameplay. La aceptación visual de MANUAL.md sigue pendiente; la finalización automática y la build no la sustituyen.
