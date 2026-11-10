# Fase 4: Editor universal de poses de la mano 3D

Menú exacto: **Tools → Metroidbrainia → Hand 3D → Pose Editor**.

Solo Edit Mode y el rig de `Assets/Scenes/TestArmScene.unity`. No hay un nuevo componente que añadir. El runtime sigue usando todas las capturas antiguas. Los perfiles nuevos todavía no intervienen en Play.

## Arquitectura y reutilización

Tres archivos de Editor, con responsabilidades separadas:

- `Hand3DPoseEditor`: una sola ventana para seleccionar, editar, capturar, importar y guardar. Nombres e IDs de perfiles son datos; no hay código específico por gesto.
- `Hand3DPoseLibrary`: una colección opcional ScriptableObject, exclusiva de Editor, con el ProfileSet de fase 3 y las referencias a los dos perfiles de extremos de dedos. Un asset contiene muchos perfiles; no se crea un asset/componente por pose.
- `Hand3DPosePreview`: escenario temporal estándar de Unity (`PreviewSceneStage`). Copia la jerarquía Transform y los MeshRenderer/SkinnedMeshRenderer, reubica las referencias de piel en la copia y no copia scripts de runtime. El rig fuente no se escribe, ni siquiera durante Preview.

Se reutilizan `Hand3DEditing.CanEdit/ValidBone`, `Hand3DModeEditing.Ready`, el formato/validador de fase 3 y las ecuaciones ya auditadas de cámara/muñeca/neutral. No se llaman las capturas antiguas: esas operaciones escriben sus campos y marcan escena/prefab dirty, que aquí hay que preservar. Se conserva íntegro el conversor protegido: el editor importa su JSON, sin invocar Convert/RestoreLegacy en producción.

La única adaptación del archivo de datos `Hand3DPoseProfile.cs` añade displayName y permiso opcional de posición de segmento, inicialmente falso. Su validación exige espacio local y longitud local conservada para ese permiso. Preview/Capture comprueban además longitudes mundiales y escalas. No cambia la aplicación del esqueleto ni ningún consumidor funcional.

## Datos y almacenamiento explícito

Importar una exportación correcta de fase 3 crea una **copia de trabajo en memoria**. Solo se carga `data`; el respaldo `legacy` no se convierte en un mecanismo de escritura desde la ventana. Abrir la herramienta no importa ni convierte nada.

Guardar crea o actualiza una biblioteca propia exclusivamente bajo:

```text
Assets/Editor/Experiments/Hand3D/PoseProfiles/<nombre>.asset
```

La carpeta se crea al guardar explícitamente, no al abrir la ventana. Está separada de las escenas y assets compartidos y queda fuera del contenido de runtime por estar bajo Editor. No se conecta la biblioteca a Rig, Input ni otros componentes. El destino no puede contener `..`, tener otra extensión ni apuntar a otra carpeta. Una copia nueva no sobrescribe un asset existente; un asset de biblioteca cargado guarda únicamente su propio archivo. No se llama a SaveAssets global ni a guardar escena/prefab.

`Save Library` usa el nombre indicado para una copia nueva; el campo Saved asset muestra el destino real cuando ya existe. Para recuperar una biblioteca, asignar su `.asset` al campo Pose library. Para una biblioteca independiente nueva, volver a importar el JSON y usar otro nombre al guardar.

Los cambios de datos usan Undo completo del ScriptableObject y dirty de ese documento. Los cambios de Transform usan el Undo normal sobre objetos temporales. Al cambiar datos por Undo/Redo se cierra la previsualización obsoleta; Undo/Redo de un hueso de la copia mantiene la edición. Descartar cambios recupera la instantánea de trabajo guardada/cargada. Cerrar la ventana restaura siempre la escena visible; Unity gestiona el aviso de documento sin guardar.

## Selección de perfiles actuales

Asignar `PlayerCapsule/Capsule/armsFBX` (Hand3DRig) e importar, mediante el botón, por ejemplo:

```text
Tools/Hand3DPhase3/Results/691828105b3442dca24f219a65b4201e/phase3-profile-export.json
```

También sirve el export copiado en los resultados finales de fase 4. La identidad de escena/rig y los huesos deben coincidir. No hay retargeting automático a otra escena o prefab.

| Trabajo | Contexto / perfil principal | Referencia y complementos de Preview |
|---|---|---|
| Exploration Default | Exploration / Mode.exploration | Sin referencia |
| Exploration OK | Exploration / Gesture.0.OK.Overrides | Mode.exploration; companion Gesture.0.OK.Wrist |
| Exploration Point | Exploration / Gesture.1.Point.Overrides | Mode.exploration; companion Gesture.1.Point.Wrist |
| HandEdit Default | HandEdit / Mode.handEdit | Sin referencia |
| Gesto en edición | HandEdit / el perfil Overrides | Mode.handEdit; sin offset de muñeca Exploration |
| GrabDoor | Interaction / GrabDoor.Joints | Mode.exploration; companion GrabDoor.Additional |
| GrabDocument | Interaction / GrabDocument.Joints | Mode.exploration; opcionales actuales vacíos |
| Extended / Flexed | Exploration o HandEdit / Fingers.extended o Fingers.flexed | Independientes; referencia de modo opcional |

Los nombres de esta tabla son datos de la exportación actual, no condiciones del código del editor. Los 13 perfiles siguen siendo fragmentos parciales. Elegir companions no fusiona ni modifica los datos: solo construye una vista temporal. Las capas complementarias se filtran/validan por contexto; un offset de Exploration no se aplica a HandEdit. La base de referencia puede elegirse expresamente entre modos para preparar otra interacción. Capture siempre escribe únicamente el perfil principal.

Para un gesto con fingerCombination, Preview toma los extremos Extended/Flexed según la máscara y aplica los overrides. Las referencias se infieren de los propertyPath de los dos perfiles antiguos durante la importación explícita y pueden configurarse en el desplegable de endpoints. Un extremo faltante/sin captura provoca diagnóstico. Point no recibe un override inventado del índice.

## Captura común

1. Crear un perfil vacío con nombre, ID estable (o dejar que genere uno) y contexto. También se puede duplicar un perfil para conservar una operación especial/neutral y asignarle otro ID.
2. Elegir Reference/base y companions si hacen falta. Las muñecas relativas exigen una referencia con orientación de muñeca en cámara y peso 1; no hay fallback silencioso.
3. **Begin Manual Pose** crea la copia con la postura fuente. **Preview Pose** crea la copia y aplica la selección actual. Ambas abren el escenario temporal en Scene View.
4. Seleccionar articulaciones en la jerarquía de ese escenario o desplegar Select preview joints en la ventana. Usar las herramientas normales Rotate/Move y el Inspector de Transform sobre la copia.
5. Elegir articulaciones y canales, y pulsar **Capture Pose — selected profile only**.
6. **Restore / Cancel** sale del escenario y destruye la copia. No borra una captura que se haya realizado explícitamente: para eso usar Undo o descartar la edición del documento.
7. Guardar la biblioteca explícitamente. Los originales permanecen sin cambios.

Máscaras de captura:

- FullBody: las 22 articulaciones registradas actualmente.
- ArmOnly: raíz, shoulder, bicep, forearm y twists; excluye muñeca y dedos.
- WristOnly: muñeca.
- FingersOnly: las 15 articulaciones actuales.
- Custom: las rutas marcadas.

Rotation es el valor inicial. Position/Both permiten posición de raíz, hombro y dedos. Los segmentos/twists se omiten salvo permiso explícito; incluso con permiso se rechazan cambios de longitud local/mundial o de escala. No se anima escala. La posición de segmento se guarda en ParentLocal; raíz y rotación de muñeca nuevas se capturan en cámara, y las restantes rotaciones en local. Las contribuciones existentes conservan su espacio y operación.

La captura parcial actualiza solo canales seleccionados. Conserva otros huesos, otros canales, neutrales y los demás perfiles; las repeticiones reutilizan la contribución y no acumulan duplicados. Los pesos existentes se conservan: un peso cero sigue sin influencia hasta editarlo expresamente. Los canales nuevos tienen peso 1. Campos vacíos o sin captura no se rellenan con una pose ficticia al importar/previsualizar.

## Contextos y geometría

Exploration y las dos interacciones pueden capturar todo el cuerpo como referencia futura de IK. No se bloquea bicep/forearm en el runtime ni se simula el contacto con pomo o el movimiento libre del papel aquí.

Para un gesto normal HandEdit, crear un perfil vacío en HandEdit, activar Fingers-only mask y capturar FingersOnly. El validador impide influencias activas de cuerpo/muñeca en ese perfil. La base HandEdit puede mantener su cuerpo completo sin esa restricción. La selección de captura parcial no elimina contribuciones anteriores de cuerpo: esa conservación es deliberada.

Add joint to catalog permite registrar un Transform real descendiente del rig, con rol y dedo cuando corresponda. No inventa nodos. Se rechazan referencias repetidas/ajenas. Si se amplía un dedo, hay que capturar sus nuevos extremos antes de usar una máscara de gesto que lo necesite.

## Espacios especiales y límites actuales

Preview no interpreta la muñeca de Exploration como localRotation: introduce su offset respecto a la orientación de cámara de la referencia. AdditionalBone de muñeca mantiene el delta respecto al neutral. Las operaciones inversas de Capture conservan esa semántica. La orientación en cámara se evalúa después de los padres; las contribuciones locales se aplican por jerarquía.

Se admite un offset especial y un delta neutral de muñeca en una vista compuesta. Si se combinan ambos, sus pesos relativos deben ser 1; las combinaciones relativas fraccionarias ambiguas se rechazan. Los offsets de cámara genéricos sin operación de referencia soportada se diagnostican; se puede usar Absolute o el offset de muñeca específico. No se sustituye un contexto ausente por una aproximación.

El editor muestra posturas estáticas y pesos, no reproduce las curvas en una línea temporal. Duraciones/curvas se conservan/editan para la fase futura. No aplica IK, apertura de puerta, retorno de documentos, pixelado ni overlay en la copia. La vista es la geometría de brazo en Scene View; la composición de Game sigue siendo la original.

Restauración ocurre al cancelar, cambiar perfil/referencia, cerrar, antes de recargar assemblies y al salir de Edit Mode. También se puede salir por el breadcrumb del escenario. Toda la copia es descartable y no se depende de entrar/salir de Play para recuperar la fuente. En Play la ventana no ofrece edición.

## Reproducción de pruebas

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase4/Run.ps1
```

Mismos requisitos de copia marcada, Unity 6000.6.3f1 y licencia de las fases anteriores. Nunca se lanza Unity sobre producción, se usa Git ni se instalan paquetes. El runner copia los Assets actuales al proyecto separado, ejecuta las 2.745 regresiones y las pruebas de Editor, y verifica hashes de todos los originales. `-EditorOnly` es una opción de desarrollo que omite las regresiones; la ejecución final documentada es completa.

La prueba guarda bibliotecas solo en Assets de la copia temporal. Producción recibe únicamente tres fuentes nuevas de Editor, sus metas generadas por Unity, la adaptación de datos indicada y Tools/Hand3DPhase4. Las fases 0–3 se conservan intactas. Resultados y comprobación manual: REPORT.md.
