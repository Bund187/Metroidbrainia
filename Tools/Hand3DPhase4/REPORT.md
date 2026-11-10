# Resultado de fase 4 — Editor universal

**Fase 4 completada.** Menú: **Tools → Metroidbrainia → Hand 3D → Pose Editor**. Ejecución final completa: `Results/b4c2d386f47b45d6a033c781824812d4/`, Unity 6000.6.3f1. **3.685 comprobaciones aprobadas**, sin fallos: 611 + 254 + 668 + 1.212 anteriores y 940 de Editor, estas últimas distribuidas en 12 grupos.

Confirmación de las 940 pruebas de Editor sobre las fuentes finales exactas: `Results/91e7c689eacb4cf68a2d3ff52b18de74/`. Su manifiesto se contrasta también con los archivos realmente importados en la copia de Unity. Las regresiones anteriores corresponden a la batería completa indicada arriba; no se suman dos veces las comprobaciones de Editor.

El runtime sigue usando exclusivamente las poses antiguas. No se inicia fase 5.

## Archivos

Creados en Assets/Editor/Experiments/Hand3D, junto con metas generados por Unity temporal:

- Hand3DPoseLibrary.cs: colección persistente opcional de Editor, importación de copia de datos y guardado acotado.
- Hand3DPosePreview.cs: escenario temporal de Unity, copia del modelo sin scripts funcionales y operaciones de Preview/Capture.
- Hand3DPoseEditor.cs: ventana única, Undo/Redo y lifecycle de edición.

Modificado únicamente `Assets/Scripts/Experiments/Hand3D/Hand3DPoseProfile.cs`, un archivo de datos que el runtime actual no consume: displayName, permiso opcional allowSegmentPosition (false por defecto) y validación asociada de espacio/longitud. No se modifica su meta ni se eliminan campos.

Creados bajo Tools/Hand3DPhase4: runner, arnés, inventario de hashes previo, copia anterior del archivo de datos, README, este informe y evidencias Results. No se crean bibliotecas de poses en Assets de producción durante la implementación; las bibliotecas de prueba se guardan únicamente en la copia temporal.

**TestArmScene es byte a byte idéntica**, incluidas todas las capturas, curvas, referencias y UnityEvents. El inventario protege todos los archivos previos excepto la adaptación explícita del formato. Los editores antiguos y Hand3DPoseConversion conservan sus hashes, incluida la protección de migración aislada. Prototype, sistema sprite, controladores originales, motores compartidos, prefabs, Starter Assets, paquetes y ProjectSettings permanecen intactos. No se realizan operaciones de Git.

## Flujo común

Una biblioteca contiene el formato universal y múltiples perfiles; no hay una clase, componente o asset por gesto. El editor filtra por Exploration, HandEdit, Interaction o Action. IDs/nombres son datos. Puede crear perfiles vacíos o duplicar uno para preservar una operación especial y darle otra identidad.

Preview se ejecuta en una copia de la jerarquía, materiales de lectura y renderers en PreviewSceneStage. No copia scripts de runtime ni escribe Transform fuente. Los huesos de la piel apuntan a la copia. Las escalas y longitudes se validan, sin llamar al solver. Cerrar/cancelar/cambiar perfil o abandonar Edit Mode destruye esa copia. La reversibilidad no depende de recuperar transformaciones del original porque nunca se cambian.

Capture modifica solo el perfil principal. Se seleccionan FullBody, ArmOnly, WristOnly, FingersOnly o Custom, y Rotation/Position/Both. Conserva contribuciones y canales no seleccionados y los demás perfiles; repetir captura no añade duplicados. Los perfiles parciales no se completan automáticamente durante Preview/Import. Registrar articulaciones requiere seleccionar nodos existentes del rig.

Se conservan espacios y referencias de fase 3. Cámara no se trata como localRotation. Las muñecas relativas exigen una base explícita con muñeca en cámara; neutral-delta conserva su fórmula. La selección lógica de dedos usa las referencias de extremos y después overrides. La captura aplica las operaciones inversas correspondientes sin sobrescribir los originales.

La fuente de importación es el JSON convertido. Se crea una copia en memoria y no se invoca la migración de producción. Guardar requiere una acción explícita y el destino `Assets/Editor/Experiments/Hand3D/PoseProfiles/`. Se usa SaveAssetIfDirty sobre esa biblioteca, sin SaveAssets global ni SaveScene. Una nueva biblioteca no puede sobrescribir otra existente. Undo/Redo modifica el documento o los huesos de la copia, nunca los campos antiguos.

## Pruebas ejecutadas

| Grupo | Cobertura aprobada |
|---|---|
| Abrir/cerrar sin editar | Ventana real de Unity, sin cambios en lista de assets, componentes antiguos, Transform ni dirty fuente |
| Máscaras y perfiles genéricos | 22 FullBody, 6 ArmOnly, 1 WristOnly, 15 FingersOnly; creación genérica, duplicación con operación especial e IDs repetidos rechazados |
| Preview local/pesos | Rotaciones de los quince dedos, peso 0,5 y copia sin MonoBehaviours |
| Cámara y muñeca | Posición/rotación de modo, offset de OK, neutral de GrabDoor, falta de referencia y referencia a sí mismo rechazadas; offset de Exploration rechazado en contexto HandEdit |
| Captura completa | 22 articulaciones, espacios adecuados y comparación de todos los perfiles no seleccionados |
| Captura parcial/repetida | Solo dedos, canales disjuntos, contribuciones restantes intactas, sin acumulación y rechazo de muñeca en fingersOnly |
| Geometría | Posiciones IK omitidas por defecto, habilitación explícita válida y rechazo de estiramiento sin escribir datos |
| Captura relativa | Roundtrip de offset de muñeca y de neutral-delta |
| Undo/Redo | Datos del ScriptableObject y Transform temporales mediante APIs reales de Unity |
| Guardado/recarga | Captura nueva guardada, asset descargado de memoria y recargado desde disco; nombre/valores conservados; colisión y destino protegido rechazados |
| Lifecycle de ventana | Cambio de perfil, Cancel, Close y salida de Edit Mode eliminan el escenario temporal |
| Referencias/catálogo | Huesos ausentes, referencias duplicadas, contexto inválido, ampliación con un nodo real y rig ajeno sin configuración |

Cada grupo comprueba además la serialización de todos los MonoBehaviours del rig, los Transform de referencia y el estado dirty de la escena fuente. La mayor parte del número de aserciones son comparaciones de esas invariantes; no son 940 acciones distintas de interfaz.

Las regresiones de fases 0–3 conservan sus expectativas y montaje de fase 2. Las bibliotecas nuevas no se conectan a ellas. Los logs/JSON están en el directorio final, con tested-files.json e integrity.txt. Los intentos previos se conservan: el primero detectó una inicialización incompleta de PreviewSceneStage, corregida llamando al método base antes de crear objetos. No se cuenta como validación aprobada. -EditorOnly se usó durante la iteración; el resultado final incluye todo el conjunto.

La validación usa escenarios, ventana, assets, descarga/recarga y Undo reales en un proyecto separado. La ejecución de menús/gestos de ratón y el aspecto final de Scene View no se han revisado manualmente. No se afirma que la vista se haya validado visualmente por un usuario.

## Elementos antiguos conservados y complejidad

Siguen operativos todos los botones/Inspectores de Extended/Flexed, Visual Overrides, muñeca, Exploration/HandEdit, Grab y HandHold. No se distribuyen nuevos botones equivalentes por componentes. El nuevo procedimiento central admite futuras poses por datos y catálogo, sin añadir código por gesto.

Se reutilizan los helpers de ámbito/configuración y el formato de fase 3. Las rutinas antiguas que escriben capturas/Transform fuente no se reutilizan para hacerlo aquí: se reemplaza esa escritura por una copia descartable y una biblioteca aparte. Tres tipos de Editor corresponden a datos, escenario e interfaz; no hay manager, event bus ni MonoBehaviour nuevo.

No se reduce aún la cantidad de editores legacy porque deben mantenerse hasta validar fase 5. La simplificación es un flujo común de selección, máscara, captura, preview y guardado, con un solo contrato de datos y sin poses codificadas por nombre. No se afirma una mejora de rendimiento del runtime: el editor no añade trabajo por fotograma en Play.

## Límites y ajustes específicos

- Preview es una postura estática de la geometría en Scene View. Edita/preserva timing y curvas, pero no reproduce una línea temporal ni la composición de Game, pixelado, documento o pulgar.
- Exploration y los perfiles de interacción pueden contener cuerpo completo como referencia futura. No se aplica IK ni se fija una mano al pomo. GrabDocument no activa una restricción nueva sobre movimiento.
- HandEdit base puede contener cuerpo; un gesto HandEdit debe usar fingersOnly y selección FingersOnly para garantizar que no modifica muñeca/brazo. Capturar dedos en un perfil previamente completo no borra su cuerpo: hay que elegir un perfil apropiado o ajustar sus influencias explícitamente.
- Posición de segmento requiere permiso y validación; cambiar escala o longitud se rechaza. Se capturan posiciones de segmento en espacio local. Mantener Rotation para el uso ordinario.
- Las combinaciones de un offset de muñeca y un neutral-delta requieren pesos relativos 1. Más capas relativas del mismo tipo o fracciones ambiguas se rechazan. Un offset de cámara genérico no soportado exige otro perfil/operación explícita; no se aproxima.
- Importación requiere la misma identidad de rig/escena y referencias reales. No hay retargeting automático ni relajación del conversor protegido.
- Los perfiles nuevos no registran gestos ni acciones de gameplay y todavía no controlan Play.

## Protocolo manual en Unity

Todas las operaciones de creación/edición siguientes son en **Edit Mode**:

1. Abrir TestArmScene y la ventana por el menú indicado. Asignar Hand3DRig de PlayerCapsule/Capsule/armsFBX.
2. Pulsar Import Phase 3 JSON y seleccionar la exportación de fase 3 o la copia de los resultados finales. Verificar los 13 fragmentos; no se supone que sean poses completas.
3. Elegir Exploration / Mode.exploration y Preview Pose. Comprobar el breadcrumb del escenario temporal y que sus objetos no tienen scripts funcionales. Usar Restore para volver a la escena.
4. Elegir Gesture.0.OK.Overrides, base Mode.exploration y companion Gesture.0.OK.Wrist. Verificar la postura. Repetir Point y los agarres según la tabla de README. La parte visual requiere esta revisión manual.
5. Crear un perfil con nombre e ID nuevo, contexto Exploration, selección FullBody y Rotation. Begin Manual Pose; seleccionar shoulder o un dedo en la jerarquía temporal, rotar y usar Ctrl+Z/Ctrl+Y.
6. Capture Pose, Restore y Preview del perfil nuevo. Comprobar las articulaciones, máscaras y espacios en los detalles plegables. Verificar que los perfiles anteriores no cambiaron.
7. Crear otro perfil HandEdit, activar Fingers-only mask, usar FingersOnly/Rotation y capturar los dedos. Previsualizar con Mode.handEdit como referencia: la base mantiene su cuerpo.
8. Probar Custom con dos articulaciones; capturar una segunda vez y comprobar que no aparecen duplicados. Probar Position/Both solo donde corresponda; un estiramiento IK debe producir diagnóstico y no guardarse.
9. Indicar un nombre de biblioteca y Save Library. Confirmar que solo aparece su asset y metas bajo la carpeta exclusiva de Editor. Cerrar, reabrir y cargar ese asset en Pose library; volver a Preview. Probar Undo/Redo después de Capture y después de editar datos.
10. Durante una previsualización, cambiar de perfil, Restore, cerrar o compilar. Comprobar que se vuelve a la fuente intacta, sin quedar un modelo duplicado ni marcar TestArmScene dirty. Si hay datos sin guardar, comprobar Save/Discard/Cancel del documento.
11. Entrar en Play: la copia debe cerrarse y la ventana mostrar edición deshabilitada. Recorrer Exploration/HandEdit, dedos, puertas y documentos. Debe continuar exclusivamente el sistema antiguo, con su papel/pulgar y controles actuales.

Para abrir/cerrar sin cambios: hacerlo también con una biblioteca cargada, no tocar Capture/edición, cerrar y comprobar ausencia de cambios de archivos. La prueba automática cubre apertura/cierre vacíos e invariantes de la fuente durante los demás casos; esta variante con navegación visual completa queda en el protocolo manual.

No ejecutar Prepare ni las capturas legacy como parte de esta prueba. No es necesario modificar componentes ni asignar la biblioteca al runtime. Fin de fase 4.
