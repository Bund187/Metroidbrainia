# Propuesta de prueba técnica: postura completa e IK

Estado: propuesta, no implementada ni ejecutada en fase 0.

## Solver actual

`Hand3DArmMovement.SolveArm` rota solo `upperArm` y `forearm` en espacio mundial. El destino es la posición de `hand`; no escribe su rotación ni los dedos. Longitudes constantes medidas en Initialize, límites geométricos `abs(a-b)` y `a+b`, y dirección de flexión mediante Elbow Hint o referencia en espacio de cámara. No estira ni escala segmentos.

`ApplyFrame` coloca raíz, brazo y antebrazo y restablece las posiciones locales de antebrazo/muñeca. `Hand3DRig` compone muñeca/dedos/twists. Al haber contacto conserva la orientación mundial final de muñeca, llama a `ResolveContactAfterPose` y restaura esa orientación después del cambio de padre causado por IK.

Unity ha resuelto la cadena real como `shoulder.r > bicep.r > forearm.r > wrist.r`. UpperArm es `bicep.r`: el solver controla bicep y forearm, no shoulder. Las rutas completas están en `Reference/phase0-ik-chain.txt`. Una pose que modifique shoulder cambia el origen y alcance respecto a pantalla; hay que volver a calcular el objetivo de referencia y la preferencia de codo. No confundirlo con permitir que la captura bloquee bicep y forearm, que deben seguir disponibles para IK.

## Diseño a evaluar

1. Evaluar una postura completa en memoria: raíz relativa a cámara, rotaciones locales de articulaciones y orientación deseada de muñeca en espacio explícito. Conservar canales no capturados y posiciones/escalas de segmentos.
2. Usarla como referencia, no como bloqueo rígido. Mantener libres para el solver las rotaciones de las dos articulaciones de la cadena. La postura puede aportar posición de hombro, orientación de raíz y preferencia de codo.
3. En reposo sin objetivo de movimiento/contacto, respetar la postura capturada. Con movimiento, sumar desplazamiento de pantalla a la referencia y limitar al alcance. Aplicar IK sobre la postura de ese frame, sin acumular sobre el resultado anterior.
4. En HandEdit, mantener base completa e inspección; la máscara de gesto solo afecta a los dedos. No usar el perfil completo de Exploration para reorientar el brazo de edición.
5. Para GrabDoor, primero componer muñeca/Grip Wrist Offset y dedos/twists. Calcular el offset mundial del punto de agarre respecto a muñeca. Resolver muñeca hacia `targetGrip - gripOffset` y restaurar su orientación mundial final. La IK puede cambiar brazo/antebrazo, pero no debe sustituir dedos/twists ni orientación de GrabDoor.
6. Para GrabDocument, conservar el movimiento libre y hacer que papel/máscara consuman el resultado final. No convertirlo en contacto fijo con el papel del mundo.

Una pose completa exacta y un destino arbitrario de pantalla pueden ser incompatibles. La prioridad del destino y el límite de alcance deben ser explícitos. No prometer conservación exacta de las rotaciones de codo/hombro cuando el solver necesita modificarlas.

## Prueba mínima propuesta, aislada y reversible

- Añadir posteriormente un único arnés de Editor en Tools, staged en una copia temporal como el de fase 0. Sin componente persistente, nuevas capturas ni sustitución del runtime.
- Instanciar una copia del rig actual. Guardar referencias, posiciones, escalas, longitudes y todas sus rotaciones; trabajar sobre copias de los datos.
- Aplicar dos posturas de referencia diferentes mediante el ArmFrame existente y un Elbow Hint temporal derivado del codo de cada referencia. Invocar el SolveArm existente por reflexión únicamente dentro del arnés. Esto prueba viabilidad, no introduce el compositor universal.
- Recorrer centro, cuatro direcciones y cuatro diagonales; incluir límites inalcanzables y pesos 0, 0.5 y 1 entre referencias. Repetir con el agarre del pomo y sus offsets y con la escala real del modelo.
- Medir errores de muñeca/agarre en mundo y pantalla, orientación, longitudes, retorno y determinismo. Capturar además imagen real en Game para detectar inversiones del codo y deformaciones que los números no muestran.
- Destruir las instancias temporales y comparar hashes. No guardar cambios en escenas/assets originales. No cambiar `elbowHint` ni perfiles de la escena original.

## Criterios de aprobación

1. Las pruebas de referencia anteriores siguen pasando; los casos visuales pendientes no se convierten automáticamente en PASS.
2. Destinos alcanzables: error de agarre/objetivo <= 0.5 píxeles a resolución registrada y <= 0.001 unidades de mundo; si la cuantización pixelada impone otro umbral, justificarlo antes de aceptarlo.
3. Orientación final de muñeca: error <= 0.1 grados respecto a la orientación compuesta; dedos/twists de GrabDoor dentro de 0.05 grados de su resultado de referencia.
4. Longitudes: error relativo <= 0.0001; posiciones locales de segmentos y escalas intactas. Sin NaN ni salto de rama de codo en el recorrido.
5. Objetivos inalcanzables: limitación explícita, sin estiramiento ni acumulación de un destino oculto; no exigir alcanzar todas las esquinas.
6. Mano en HandEdit: el gesto no modifica raíz, brazo, antebrazo ni la referencia de inspección. La herencia de la transformación del padre no cuenta como modificación de una captura local.
7. Peso 0 reproduce referencia anterior y peso 1 reproduce postura nueva en reposo; movimiento/contacto siguen resolviendo con todos los pesos.
8. Entrada/salida/interrupción de pose recupera la combinación lógica actual, sus overrides y muñeca, sin deriva tras 100 ciclos.
9. Comprobación visual real en Unity de contacto, codo, papel y pulgar. Una prueba de geometría o una textura sintética no basta para acreditar este punto.
10. Hashes de originales intactos; resultado y decisión registrados para revisión antes de cualquier refactorización.

## Riesgos

- Los espacios mezclados de ArmFrame no son intercambiables con rotaciones locales.
- La referencia de codo calculada una sola vez no describe necesariamente otra postura completa.
- Cambiar offsets o escala después de Initialize invalida las longitudes medidas.
- Orientación mundial y orientación local de muñeca cambian de relación al resolver el padre.
- Mantener contacto puede ser imposible en un objetivo fuera del alcance.
- La máscara del pulgar requiere su BakeMesh con tratamiento correcto de escala y actualización posterior a la pose final.
- Cambiar prioridades implícitas o el frame de adquisición puede alterar el comportamiento aunque una imagen estática coincida.

## Archivos previstos para trabajo posterior

Primero, solo un arnés nuevo bajo Tools y este protocolo/resultados. Si se aprueba posteriormente integrar el diseño: `Hand3DArmMovement.cs` (objetivo/solver), `Hand3DRig.cs` (composición final), `Hand3DModeController.cs`, `Hand3DInteractionPose.cs` y los datos de `Hand3DPoseRecognizer.cs` (perfiles y espacios). Las herramientas de captura solo cambiarían después de aprobar la conversión de datos. Para extraer contacto común: `Hand3DDoorInteractor.cs`, `Hand3DDocumentInteractor.cs`, `Hand3DDocumentDisplay.cs` y `Hand3DScreenProjection.cs`, más sus herramientas de conexión. No son cambios autorizados en esta fase.
