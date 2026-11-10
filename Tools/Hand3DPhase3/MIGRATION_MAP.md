# Correspondencia y semántica de capturas

Fuente: scripts y escena guardados actuales; no se usan defaults como sustitutos. El catálogo contiene 22 referencias reales. La exportación real tiene 13 perfiles y 79 contribuciones, algunas con más de un canal. Los perfiles vacíos representan configuraciones opcionales sin valores; no crean capturas.

| Datos antiguos | Perfil/representación | Valores | Espacio y significado |
|---|---|---:|---|
| Joint.extendedLocalEulerAngles + extendedCaptured | Fingers.extended | 15 | Rotación local absoluta, padre identificado; captura independiente |
| Joint.flexedLocalEulerAngles + flexedCaptured | Fingers.flexed | 15 | Igual; no se mezcla el flag de flexión con el de extensión |
| OK Visual Overrides | Gesture.0.OK.Overrides | 6 | Rotaciones locales absolutas, máscara solo dedos; combinación 3 |
| OK explorationWristEuler | Gesture.0.OK.Wrist | 1 | Offset especial de Exploration relativo a la orientación deseada en cámara; nunca localRotation absoluta |
| Point Visual Overrides | Gesture.1.Point.Overrides | 0 | Combinación 29; usa los extremos comunes, incluido índice Extended; no se fabrica un override |
| Point explorationWristEuler | Gesture.1.Point.Wrist | 1 | Misma operación especial; sin influencia en HandEdit |
| ArmConfiguration exploration | Mode.exploration | 4 | Raíz: posición+rotación en cámara; bicep/forearm: rotación local; wrist: rotación en cámara |
| ArmConfiguration handEdit | Mode.handEdit | 4 | Igual, con captura y transición propias; no es un poseado local homogéneo |
| grabOverrides | GrabDoor.Joints | 15 | Capturas locales absolutas; conserva fallback de dedos a Flexed |
| additionalGrabBones | GrabDoor.Additional | 3 | Muñeca: delta respecto a neutral y reposo local; twists: destino local absoluto |
| handHoldOverrides | GrabDocument.Joints | 15 | Capturas HandHold locales, independientes de GrabDoor |
| additionalHandHoldBones | GrabDocument.Additional | 0 | Sin capturas actuales |
| handHoldPositions + enable | GrabDocument.Positions | 0 | Si existen, posiciones locales absolutas neutral/hold; sin flag de captura inventado |
| Escalas y offsets locales actuales de raíz/articulaciones | Catálogo sourceLocal* y respaldo | — | Referencias/invariantes, no influencia ni captura de pose nueva |
| Shoulder sin captura específica | Referencia en catálogo, ningún valor activo | 0 | Influencia cero en todos los perfiles convertidos |

## Fórmulas que deben conservarse en el futuro

Usamos C como orientación mundial de ArmCamera, D como orientación deseada de mano en cámara, R como rotación local de muñeca al inicializar, V como rotación visual local y O como offset del gesto.

`Hand3DArmMovement.ComposeHandRotation` produce antes del offset de pomo:

```text
inverse(wrist.parent.rotation) * C * D * O * inverse(R) * V
```

`Hand3DModeEditing.Wrist` captura O como:

```text
inverse(Exploration.Frame.hand) * inverse(C) * wrist.rotation
```

O no es una rotación local absoluta ni una orientación mundial. La conversión conserva O y declara ExplorationWristOffset + Offset; conserva también D en el perfil de modo y R como referencia fuente, sin aplanarlos. El offset de muñeca específico de cada pomo sigue en los bindings originales; no es una captura estática común.

Para AdditionalBone de muñeca, el rig utiliza:

```text
V_grab = R * inverse(AdditionalBone.Neutral) * AdditionalBone.Grab
```

Se almacenan Grab, Neutral, sus Euler originales y flags/referencia neutral, con RestLocalNeutralDelta. Para twists, la operación antigua usa Grab directamente como destino local; el neutral se conserva igualmente aunque no se aplique esa fórmula de muñeca.

Los destinos completos pueden servir como referencia antes de IK: el solver actual controla bicep y forearm, no shoulder, wrist ni dedos. La futura composición debe derivar el codo desde la referencia y restaurar la orientación final de muñeca tras resolver; no imponer simultáneamente las rotaciones capturadas de los dos segmentos y un extremo arbitrario. No se ejecuta esa composición ahora.

## Curvas y flags

- Dedos/overrides: duración de Rig y SmoothStep; no hay una AnimationCurve serializada que inventar.
- Wrist de gesto: duración y copia de curva con claves, tangentes, pesos, weightedMode y wrap modes. Con t>=1 el rig fuerza el destino; con curva nula usa SmoothStep y con curva presente limita su resultado a 0..1. Al pasar a gesto no reconocido conserva el último timing; se declara esa política.
- Modos: transitionDuration y SmoothStep. Inspección y smoothing son procedimiento, no claves de una pose estática.
- GrabDoor/GrabDocument: duraciones respectivas y SmoothStep. HandHold conserva su duración de salida porque el flag handHolding sigue seleccionado durante esa mezcla en el código actual.
- Captura inexistente/deshabilitada: valores antiguos se conservan, canales efectivos None y peso cero. No se rellena con la postura de escena.
- FingerPosition y wrist enable no tienen un captured independiente en el origen: captureFlagPresent=false evita presentar uno inventado. La influencia respeta su habilitación.

## Datos preservados que no se convierten en una pose estática

El respaldo incluye íntegros los seis componentes fuente: nombres/combinaciones y tipo de acción, UnityEvents y sus argumentos/referencias, parámetros de inspección y signo del eje, flags que seleccionan el fallback de modo, movimiento, límites, smoothing, duraciones/amplitud de PointClick y todas las referencias/flags serializados.

Inspección es un roll calculado alrededor del eje longitudinal real, conjugado al espacio de cámara y suavizado. PointClick es una envolvente sinusoidal hacia Flexed del índice. El retorno del brazo, el offset dinámico del pomo, el estado de transición y las orientaciones producidas por IK tampoco son capturas estáticas. No se sustituyen por muestras, curvas aproximadas ni poses ficticias. Los estados transitorios no serializados de una sesión Play no se capturan: el conversor requiere estar fuera de Play.

No hay datos numéricos de las capturas actuales rechazados en la ejecución final. Los casos sintéticos inválidos sí se rechazan o se notifican, según si el formato antiguo los aplicaría o los consideraría inactivos. El respaldo permite recuperar los originales; no transforma nuevas capturas completas de shoulder a formatos antiguos que nunca tuvieron ese canal.
