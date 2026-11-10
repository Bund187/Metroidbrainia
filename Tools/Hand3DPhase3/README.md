# Fase 3: perfiles universales y conversión aislada

Esta fase es aditiva. Ningún componente funcional se modifica ni lee estos datos. No hay aplicación de perfiles al esqueleto, cambios de IK, ventana de captura, migración automática ni herramientas de fase 4. Las pruebas manuales satisfactorias de fase 2 comunicadas por el usuario se conservan como referencia; no se presentan como una validación visual de un nuevo motor.

## Formato

`Hand3DPoseProfile.cs` define dos clases serializables simples: un perfil y su colección con catálogo de articulaciones/referencias. No son MonoBehaviours ni ScriptableObjects. Se exportan como JSON inspeccionable, sin crear assets por pose ni añadir componentes a TestArmScene. Los campos públicos se usan deliberadamente como DTO de serialización, no como controles de un componente.

Cada contribución identifica la articulación por ruta relativa a la raíz y declara:

- Canales almacenados e influencia efectiva: posición y/o rotación; peso 0..1. No existe un canal de escala.
- Espacio: ParentLocal, CameraRelative o ExplorationWristOffset.
- Operación: Absolute, Offset o RestLocalNeutralDelta.
- Posición, quaternion y Euler fuente sin reconstruirlos desde el quaternion.
- Flag de captura, presencia real de ese flag en el formato antiguo y habilitación opcional.
- Neutral, sus Euler, posición, articulación de referencia y flag independiente cuando existen.
- Identificador persistente de cámara/padre y propertyPath fuente para verificar la correspondencia.

El perfil declara contextos, máscara opcional de solo dedos, combinación lógica cuando corresponde y transición: duración, duración de salida, SmoothStep o curva clamped con fallback SmoothStep. La regla de muñeca fuerza el destino al completar t>=1, como el rig actual, y conserva el historial de timing al dejar de reconocer un gesto. No se implementa aquí su evaluador.

El catálogo contiene únicamente raíz, shoulder.r, bicep.r, forearm.r, wrist.r, dos twists y las quince articulaciones derechas presentes. Guarda GlobalObjectId, padre, rol, dedo/índice y los Transform locales de referencia. **Estos valores de referencia no son capturas activas.** Una articulación/canal ausente de un perfil tiene influencia cero. No se genera una pose de shoulder ni se rellenan canales desconocidos con la postura visible.

Las rutas permiten un enlace futuro local al modelo sin serializar referencias Unity transitorias. Los GlobalObjectId sirven para auditoría y recuperación de esta escena; no es un sistema de retargeting automático a escenas/modelos diferentes.

## Máscaras y futura integración

El mismo formato admite un perfil completo de Exploration, un gesto completo, una base de HandEdit, gestos limitados a dedos, GrabDoor, GrabDocument y contribuciones temporales. La validación distingue una base completa de HandEdit de un perfil fingersOnly. No se aplican máscaras ahora.

Se declaran los dos huesos del solver y la muñeca, con la intención de derivar la preferencia de codo de la referencia y preservar orientación final tras IK, según fase 0.5. Esto es representación, no un solver/compositor nuevo. Las contribuciones de bicep/forearm son referencia, no bloqueo del resultado IK. No se admite traslación activa de segmentos/twists ni escala arbitraria; las posiciones de origen se conservan como invariantes. Se permiten posiciones de raíz, hombro y dedos cuando existan datos válidos.

Los perfiles convertidos son fragmentos fieles y parciales. No se duplica una pose completa para cada combinación: los extremos Extended/Flexed son comunes y la combinación del gesto selecciona esos extremos; sus overrides se almacenan aparte. La prueba sintética de formato completo no entra en la exportación real.

## Conversión y reversibilidad

`Hand3DPoseConversion.cs` es una utilidad estática de Editor. Solo se ejecuta explícitamente sobre TestArmScene fuera de Play en el proyecto separado que contiene la marca de validación. No tiene Awake, OnEnable, OnValidate, menú, ventana ni ejecución automática.

1. Lee SerializedProperty de los componentes actuales, sin inicializar el runtime ni tomar nuevas capturas.
2. Construye un resultado nuevo, con IDs estables, perfiles y catálogo. Nunca añade valores a una colección previa.
3. Valida referencias, articulaciones/canales duplicados, máscaras, pesos, quaternions, espacios y referencias de neutral/cámara/padre. Un error impide exportar y deja intacta una exportación anterior.
4. Conserva un archivo de respaldo de Rig, PoseRecognizer, ModeController, InteractionPose, ArmMovement y PoseActions: JSON original y tabla de referencias GlobalObjectId, incluidos UnityEvents.
5. Exporta solo dentro del proyecto de validación. El runner copia el resultado a Tools, nunca a la escena funcional ni a Resources/Assets de producción.

`RestoreLegacy` recupera explícitamente ese respaldo en el mismo proyecto aislado y con la misma identidad de escena/raíz. Resuelve todas las referencias persistentes antes de escribir. Neutraliza los instanceID temporales del JSON y reconstruye las referencias por propertyPath; no interpreta IDs de otra carga. Una referencia no resoluble aborta antes de modificar componentes.

La reversibilidad es recuperar exactamente las capturas fuente del respaldo, no una conversión inversa de cualquier perfil futuro editado al formato antiguo, que es menos expresivo. La prueba recarga la escena, altera datos temporales y verifica recuperación completa, perfiles, referencias y un evento persistente sintético. No guarda esa escena ni toca producción.

Hay dos casos especiales detectados expresamente:

- El código actual de overrides de gesto ignora captured=false al aplicar. Un override con ese flag produce error de conversión: no se inventa una desactivación silenciosa para adaptarlo al nuevo formato.
- Un AdditionalBone sin neutral válido queda sin influencia, conservando todos sus datos y con diagnóstico específico, igual que su propiedad Captured antigua. No se calcula un neutral ficticio.

## Repetir

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase3/Run.ps1
```

Requiere Unity 6000.6.3f1 con licencia y el proyecto completo marcado de fase 0 en `%TEMP%/MetroidbrainiaPhase0-20261109` (o `-ValidationProject`). No instala paquetes, cambia configuración global ni usa Git. Nunca lanza Unity con producción como projectPath.

El runner copia los Assets actuales a la copia separada antes de ejecutar, conservando la resolución local de paquetes del entorno de validación. Ejecuta las 611 + 254 + 668 comprobaciones anteriores con los mismos ajustes de fixture de fase 2, y después la migración. No modifica los arneses aceptados. Verifica todos los archivos previos de Assets, Packages, ProjectSettings y Tools de fases 0/0.5/1/2, antes y después. Solo permite dos fuentes nuevas y sus metas generadas por Unity temporal.

Cada ejecución usa runId propio y conserva logs y JSON. El inventario before-files.json corresponde al inicio de esta fase; TestArmScene y todo el runtime previo deben conservar su hash. Los fallos iniciales de compilación quedan registrados y no son regresiones aprobadas. La exportación es `Results/<runId>/phase3-profile-export.json`.

APIs para pruebas aisladas: `Convert(rig)` devuelve datos/errores/notas; `Export(rig, path)` valida y escribe; `RestoreLegacy(rig, result)` recupera el archivo fuente. No usarlas para migrar producción en esta fase.

## Complejidad y conservación

Se añaden un formato de perfil, una colección y un conversor de Editor; no una clase por gesto ni componentes, assets de pose, managers o ejecutores. Existe duplicación temporal entre los originales y el archivo de migración, únicamente en datos aislados. El runtime no crea esa copia ni incurre en trabajo por fotograma.

Se conservan Input, modos, reconocimiento, sesiones, adaptadores/bindings, movimiento/IK, poses funcionales y renderizado. Los candidatos futuros a retirar, tras validar herramientas y composición posteriores, son los campos especializados Extended/Flexed, LocalBonePose visual/grab/hold, datos AdditionalBone/FingerPosition y campos de captura ArmConfiguration y muñeca de HandPose. No se retiran aún. El mapa de dedos, el estado lógico, las acciones/eventos, los controles de modo e inspección y ArmFrame usado por movimiento no son duplicaciones que deban eliminarse automáticamente.

Correspondencia exacta y fórmulas: MIGRATION_MAP.md. Resultados, límites y prueba manual: REPORT.md. Se detiene en fase 3.
