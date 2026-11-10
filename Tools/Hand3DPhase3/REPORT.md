# Resultado de fase 3

**Fase 3 completada, de forma aditiva.** Ejecución final: `Results/691828105b3442dca24f219a65b4201e/`, Unity 6000.6.3f1. **2.745 comprobaciones aprobadas: 1.533 regresiones anteriores y 1.212 comprobaciones de migración**, sin fallos. Estas últimas se agrupan en ocho casos. No se inicia fase 4 ni se aplica el formato nuevo al runtime.

Evidencias en ese directorio: phase0-results.json, phase1-results.json, phase2-results.json, phase3-results.json, phase3-profile-export.json, cuatro logs de Unity, tested-files.json e integrity.txt. Los intentos iniciales con errores de compilación de APIs antiguas de identificadores se conservan y no cuentan como pruebas aprobadas. La implementación final usa referencias persistentes y no depende de GetInstanceID ni de casts obsoletos de EntityId.

## Resultado de la conversión real

- 13 perfiles parciales, con 79 contribuciones.
- 22 referencias reales: raíz, shoulder, bicep, forearm, wrist, dos twists y 15 articulaciones de dedos.
- Quince Extended y quince Flexed con flags independientes; seis overrides de OK y sus dos máscaras de contexto; Point conserva su combinación y por ella el índice Extended.
- Dos offsets especiales de muñeca, con su espacio/operación y timing específicos.
- Dos configuraciones de modo con posición/rotación de raíz y orientación de muñeca relativas a cámara, y bicep/forearm locales.
- Quince capturas GrabDoor, quince HandHold y tres AdditionalBone de GrabDoor, incluidos sus neutrales, referencias y flags.
- Los perfiles opcionales de posiciones y Additional HandHold permanecen vacíos. Shoulder no recibe una captura inventada. No hay influencia de canales inexistentes.

No hay errores de conversión de las capturas actuales. Los 79 valores proceden de datos serializados existentes. El catálogo de Transform de referencia no equivale a haber capturado una pose completa. Las pruebas de formato completo y campos opcionales usan datos sintéticos aislados, que no entran en la exportación real.

## Qué se ha añadido

Ningún archivo preexistente modificado ni eliminado. Dos fuentes nuevas:

1. `Assets/Scripts/Experiments/Hand3D/Hand3DPoseProfile.cs`: perfil, colección, catálogo, máscaras y validación de datos. No contiene un aplicador.
2. `Assets/Editor/Experiments/Hand3D/Hand3DPoseConversion.cs`: lectura explícita de fuentes, conversión, exportación y recuperación del respaldo. Sin lifecycle hooks ni herramientas de captura.

Sus dos `.meta` son generados por Unity en el proyecto temporal. Se crean además `Tools/Hand3DPhase3/Run.ps1`, `Phase3Validation.cs`, `before-files.json`, README.md, MIGRATION_MAP.md, este informe y los resultados. La exportación queda fuera de Assets de producción y no requiere un ScriptableObject por pose.

Todos los archivos anteriores de Assets/Packages/ProjectSettings y de Tools de fases 0/0.5/1/2 conservan su hash respecto al comienzo de esta fase. **TestArmScene se mantiene byte a byte idéntica**, junto con sus componentes, referencias, eventos, nueve bindings de puertas y tres de documentos. El runtime funcional completo sigue intacto y no referencia los perfiles nuevos.

Prototype, sistema original de brazo 2D, FirstPersonArmController, ArmView, DoorHandInteractable, DocumentHandInteractable, prefabs compartidos, Starter Assets, paquetes y configuración global permanecen intactos. No se han hecho operaciones de Git.

## Pruebas de migración

| Caso | Comprobación y resultado |
|---|---|
| No destructiva, esquema e idempotencia | 13/22/79 valores esperados; dos conversiones y dos exportaciones iguales; los seis componentes fuente permanecen iguales; Transform locales/escala/referencias intactos |
| Valores, espacios, máscaras y neutrales | Comparación contra SerializedProperty de cada contribución; flags y referencias padre/cámara/neutral correctos; ningún shoulder/posición opcional fabricado |
| 32 máscaras frente al rig actual | Las 15 rotaciones locales resultantes del rig existente coinciden con Extended/Flexed + overrides convertidos en las 32 combinaciones: 480 comparaciones de rotación |
| Duraciones y curvas | Duraciones originales; claves, tangentes, pesos, weightedMode y wrap modes; evaluación muestreada y curva ponderada sintética; persistencia JSON |
| Recuperación tras recarga | Se serializa el resultado, se recarga la escena temporal y se alteran capturas; RestoreLegacy recupera perfiles, todas las referencias y toda la serialización de los seis componentes, incluidos eventos y ajustes no pertenecientes a una pose |
| Datos inválidos | Rechazo de hueso ausente, canal duplicado, máscara incompatible, peso inválido, referencia de padre/cámara/neutral incorrecta y máscara de muñeca incompatible con HandEdit; un export fallido no sobrescribe el anterior |
| Opcionales y capturas pendientes | Posiciones neutral/hold habilitadas y deshabilitadas; flags Extended/Flexed independientes; modo sin captura inactivo; neutral ausente notificado y sin influencia |
| Cuerpo completo e IK: representación | El formato acepta los 22 roles reales como referencia rotacional y pesos parciales; admite Action; limita el perfil de gesto HandEdit a dedos y declara los dos huesos del solver y preservación de muñeca |

La recuperación compara también GlobalObjectId de todas las referencias y un evento persistente sintético después de recargar. La serialización completa se compara ignorando exclusivamente IDs transitorios de JSON; las referencias se verifican por separado de forma persistente. Una referencia irresoluble se rechaza antes de escribir.

Tolerancias: posiciones y Euler fuente, menos de 0,000001 unidades/grados; comparación de quaternions, menos de 0,05 grados para absorber la representación flotante del rig/Unity. Las curvas comparan valores de claves exactamente y evaluación con tolerancia 0,000001. No se comparan directamente quaternions de espacios distintos: cada valor se compara en su espacio fuente, y el delta de neutral se contrasta con la misma referencia local válida.

Las 1.533 regresiones utilizan las expectativas aceptadas de fases 0 a 2; solo se conservan los ajustes de montaje temporal ya documentados en fase 2. Los arneses aceptados no se editan. El runner actualiza la copia con todos los Assets guardados actuales antes de ejecutar. No se vuelve a ejecutar la prueba geométrica de 39.821 checks de fase 0.5: el solver no cambia; sus conclusiones se usan para representar la propiedad de articulaciones, sin afirmar una nueva integración IK aplicada.

## Qué no se aplana a una pose

No se aproxima inspección, PointClick, retorno del brazo, offset dinámico de pomo, composición de muñeca ni solución IK mediante capturas ficticias. Son procedimientos. Sus parámetros serializados se conservan en el respaldo o en la escena intacta. Los estados transitorios de Play no son capturas y no se exportan.

La muñeca especial conserva O dentro de la composición cámara/rotación deseada/reposo local. AdditionalBone de muñeca conserva `R * inverse(Neutral) * Grab`; los twists conservan el destino absoluto local. Aplanar cualquiera de ellos a localRotation cambiaría su semántica. Las fórmulas y correspondencias están en MIGRATION_MAP.md.

El caso sintético de override de gesto con captured=false se rechaza expresamente porque el código antiguo lo aplica pese al flag. Un neutral inválido de AdditionalBone se conserva inactivo con diagnóstico, como su Captured antiguo. En la escena real no aparece ninguno de esos problemas.

El respaldo recupera las capturas fuente en la misma escena/raíz y con los componentes antiguos disponibles. No es una conversión inversa de perfiles arbitrarios futuros a tipos antiguos incapaces de representarlos. No se retargetea automáticamente a otro modelo o GUID de escena.

## Complejidad y siguientes retiradas posibles

Hay un solo formato de contribuciones para dedos, cuerpo, offsets y posiciones, más un catálogo compartido. No se añade una clase por gesto, un componente por perfil, managers, un ejecutor ni un sistema genérico de eventos. El archivo de respaldo de Editor existe para reversibilidad, no como una segunda animación ni como duplicación de datos por fotograma.

Esta fase no reduce todavía líneas del runtime: conserva deliberadamente todos los formatos originales. La simplificación habilitada consiste en que las futuras herramientas y el compositor puedan usar un solo formato. No se considera terminado el reemplazo del motor actual ni se afirma una mejora de FPS.

Tras implementar y validar las fases posteriores se podrán retirar los campos de captura especializados Joint.Extended/Flexed, Visual Overrides y muñeca de HandPose, ArmConfiguration capturada, LocalBonePose de Grab/HandHold, AdditionalBone y FingerPosition. Antes hay que reproducir sus flags, referencias y reglas exactas. Reconocimiento y eventos, estado lógico, mapa físico de dedos, modos/inspección, sesiones, adaptadores, movimiento/IK, ArmFrame y renderizado se conservan según su responsabilidad; no deben borrarse automáticamente por compartir datos con perfiles.

## Comprobación manual

No hay componentes nuevos que configurar, ni que ejecutar Prepare o recapturar. Para esta fase:

1. Abrir la escena original y repetir un recorrido breve de Exploration/HandEdit, dedos, OK/Point, acciones, puertas y documentos. Debe seguir usando exactamente el sistema anterior.
2. Revisar `phase3-profile-export.json` y MIGRATION_MAP.md: rutas de los quince dedos, seis overrides de OK, combinación Point=29, dos muñecas, modos, 15+15 agarres y tres AdditionalBone.
3. Confirmar que el catálogo incluye shoulder solo como referencia y que los opcionales no generan valores activos. Revisar las duraciones y curvas de muñeca contra el Inspector original.
4. Para reproducir conversión/recuperación usar exclusivamente Run.ps1 y la copia marcada. No aplicar el export a producción ni interpretar las pruebas como un motor de poses nuevo.

No se valida visualmente la reproducción de perfiles nuevos porque no existe todavía su aplicador. Permanecen pendientes la captura unificada de fase 4 y la composición/máscaras/IK de fases posteriores, incluida su validación perceptiva. Esta entrega se detiene en fase 3.
