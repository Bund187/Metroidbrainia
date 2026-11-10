# Resultado de fase 0.5

## Veredicto

La combinación es **geométricamente viable con el solver existente y sin un framework nuevo**: captura completa como referencia, bicep/forearm disponibles para IK y orientación final de muñeca preservada explícitamente. No se ha aprobado todavía el aspecto dinámico final en Game. No se ha iniciado refactorización, migración de datos ni modificación del runtime.

Ejecución final: `Results/ee47dbe4f20d4d7a9c41b7c194f3289d/`, Unity 6000.6.3f1. Se conservaron los originales y todos los archivos aceptados de fase 0; `integrity.txt` documenta la verificación. Solo se han creado archivos bajo Tools/Hand3DPhase05. Prototype, sistema sprite, controladores antiguos, prefabs compartidos y recursos de producción permanecen intactos.

## 1. Prueba y medidas

Las 611 regresiones aceptadas se han vuelto a ejecutar: PASS. El arnés técnico ha superado 39.821 comprobaciones sobre 12.969 muestras: 12.829 destinos alcanzables y 140 limitados. Hay 12.929 muestras con el extremo dentro del viewport; estar dentro no implica que no esté limitado.

| Magnitud | Máximo observado | Interpretación |
|---|---:|---|
| Error espacial de muñeca frente a destino factible | 0.000001222 unidades | Los imposibles se comparan con el destino limitado. |
| Error proyectado | 0.002412 píxeles de referencia | Viewport escalado a 1920x1080; no diferencia de imagen rasterizada. |
| Error de orientación final de muñeca | 0 grados medidos | Se aplica deliberadamente la orientación compuesta tras IK; no es una orientación resuelta por el solver. |
| Error local de dedos/twists | 0 grados medidos | IK no sustituye los valores de la sujeción/gesto. |
| Error relativo de longitudes | 0.000001887 | Sin cambios en offsets locales ni escalas. |
| Error del punto de agarre GrabDoor | 0.000001926 unidades / 0.001576 px | Contacto sintético móvil, con offset de muñeca de 9 grados. |
| Deriva al regresar tras 100 ciclos | 0.0000001193 unidades | Postura base reaplicada antes de cada evaluación. |
| Mayor paso consecutivo de codo en la transición muestreada | 0.005575 unidades | Sin cambio discontinuo de rama detectado en ese recorrido; no cubre todos los polos posibles. |
| Mayor paso de muñeca en transición de 60 pasos | 0.83743 grados | Dato numérico, no juicio de suavidad perceptiva. |

Hay muestras de movimiento horizontal, vertical, diagonal, flexión y extensión; pesos 0/0.5/1; retorno; destinos exteriores e interiores imposibles, 100 repeticiones de un límite; referencia relativa a cámara girada y reanudación desde la postura mostrada. Las curvas son una interpolación experimental SmoothStep; no constituyen una migración ni una reproducción de todos los tiempos de animación de la escena.

Las capturas A usan Exploration y los datos de cada gesto actual. Las B usan el ArmFrame HandEdit existente y una rotación sintética de shoulder de (8,-12,5) grados. No se presenta B como una postura calibrada por el usuario ni se alteran sus capturas. Se registran todas las articulaciones para detectar efectos colaterales; el runtime futuro no necesita animar huesos izquierdos, sockets o extremos sin uso.

## 2. Articulaciones y propiedad

Jerarquía confirmada: `shoulder.r > bicep.r > forearm.r > wrist.r`. Existen las quince articulaciones derechas de dedos y `forearm.Twist0.r > forearm.Twist1.r`, en una rama del antebrazo separada de wrist.

| Canal | Puede capturarse | Propiedad durante movimiento/contacto |
|---|---|---|
| Raíz del modelo relativa a cámara | Sí | Referencia y seguimiento de cámara, sin alterar escala. |
| shoulder.r | Sí | Postura de referencia; cambia el origen de la cadena. |
| bicep.r | Sí | Referencia inicial y preferencia de flexión; rotación final disponible para IK. |
| forearm.r | Sí | Referencia inicial y codo; rotación final disponible para IK. |
| wrist.r | Sí | Orientación compuesta de modo/gesto/interacción, conservada tras IK. |
| Quince articulaciones de dedos | Sí | Gesto o interacción activa; sin escrituras del solver. |
| Dos twists de antebrazo | Sí | Perfil visual capturado; calibración/deformación pendiente de revisión perceptiva. |
| Posiciones que definen longitudes / escalas | Se registran para verificar | Permanecen invariantes; no se habilita animación arbitraria de esos canales. |

Una captura completa no puede imponer exactamente bicep y forearm mientras exige simultáneamente cualquier destino de pantalla. Debe expresar una referencia, no un bloqueo de ambas articulaciones.

## 3. Cómo combinar postura y movimiento

1. Leer/interpolar los perfiles sin cambiar el estado lógico de los dedos.
2. Aplicar la raíz relativa a cámara y las rotaciones locales de referencia; conservar offsets y escala.
3. Derivar un punto de preferencia del codo desde esa referencia **antes de resolver**. No derivarlo del resultado del fotograma anterior.
4. Componer y guardar la orientación mundial deseada de muñeca, incluidos los offsets especiales del gesto o del pomo.
5. Movimiento libre: destino de muñeca respecto a la nueva referencia; resolver con el SolveArm existente. GrabDoor: destino de muñeca = objetivo de agarre - offset mundial del punto de agarre.
6. Restaurar la orientación mundial final de muñeca, que cambia de relación con el padre al resolver IK. Mantener dedos y twists del perfil activo.
7. Consumir el resultado final para papel y máscara del pulgar. En GrabDocument no se fija la mano al documento del mundo.

El experimento usa un Elbow Hint temporal con la posición del codo capturado. No implica que producción necesite otro GameObject permanente: el mismo dato puede pasarse como vector al solver existente cuando se autorice esa separación.

## 4. Hallazgos comprobados

- La preferencia fija de Initialize alcanza el extremo, pero no conserva necesariamente la postura: al reproducir las referencias, el codo se separa hasta **0.06310708 unidades** de su posición capturada. Con el codo de cada referencia, el máximo es **0.000001012**.
- Un escritor que reaplica la pose completa después de IK destruye el movimiento: control negativo con error de **0.03999996 unidades**, frente a **0.000000642** antes de sobrescribirlo.
- GrabDoor funciona geométricamente mediante el mecanismo actual de offset/contacto y restauración mundial de muñeca. La prueba no abre las nueve puertas reales con dispositivos físicos.
- GrabDocument conserva sus dedos durante el desplazamiento libre. En el rig actual se recuperan Point y su muñeca especial al soltar ambas interacciones, incluso después de cambiar el gesto lógico durante la sujeción.
- Las escalas y posiciones locales de segmentos no cambian. Las pequeñas diferencias de longitud medidas provienen de la aritmética de Transform/quaternions del solver.

## 5. Evidencia visual y límites

Se han generado e inspeccionado renders reales de Unity/URP con **la malla deformada CPU mediante BakeMesh y los materiales originales**. La cámara de inspección mantiene la orientación de ArmCamera y se reencuadra sobre la mano; no es el encuadre ni la composición UI de Game. Los renders tienen 640x360 y no pretenden reproducir el pixelado final de 320x180.

- OK conserva el círculo en las imágenes de referencia y tras movimiento; Point conserva el índice extendido.
- GrabDoor y GrabDocument muestran configuraciones de dedos distintas y permanecen distintas después de IK.
- La variante B cambia la orientación visible respecto a A de forma intencionada; no debe interpretarse como conservación del mismo encuadre.
- Se observan facetas y deformación en muñeca/antebrazo; en la vista Exploration de Point el antebrazo tapa parte de la mano. La exactitud del extremo no garantiza buena silueta, distribución de twist ni ausencia de auto-intersecciones.
- No se han renderizado pomo/papel en contacto con estas nuevas posturas ni validado el overlay del pulgar en el Canvas final.

Imágenes recomendadas para comparar en el directorio final: `OK-0/1-baked-URP.png`, `Point-0/1-baked-URP.png`, `GrabDoor-1-baked-URP.png` y `GrabDocument-1-baked-URP.png`. Variante 0=referencia A, 1=A desplazada, 2=B desplazada.

Trazabilidad de los intentos anteriores: `20a41a...` tiene mediciones geométricas válidas, pero sus cuatro renders GPU salieron idénticos y no acreditan gestos actualizados. `da3b6b...` añadió deformación CPU, pero el render quedó vacío. En la ejecución final se recalculan bounds y matrices de cámara y se comprueba que las imágenes no estén vacías y cambien entre variantes; no se aisló cuál de esos ajustes de render era suficiente por sí solo. Se corrigió exclusivamente el arnés.

## 6. Alternativas

Los números de componentes se refieren a adiciones al sistema actual, no al número de clases de datos o archivos de test.

| Alternativa | Componentes nuevos | Complejidad / dependencias | Capturas e interacciones | Evaluación |
|---|---:|---|---|---|
| Aplicar pose rígida después de IK | 0 | Pocas líneas, conflicto directo de escritores | Fácil capturar, pero bloquea el alcance/contacto | Rechazada por control negativo. |
| Pose antes de IK con codo fijo de arranque | 0 | Cambio pequeño | Movimiento viable; pierde preferencia de postura cuando cambia el brazo | Ensayada; error de codo de 0.0631 unidades. |
| Pose como referencia + codo capturado + muñeca final | 0 obligatorios | Datos de perfil y llamada explícita al solver en componentes existentes | Un formato de captura; adaptadores específicos piden perfil/objetivo | Recomendada; ensayada con muestras, contacto y ciclos. |
| Animator/grafo de capas y constraints adicional | Uno o varios según montaje | Más configuración, integración y propiedad que coordinar | Útil si más adelante se necesitan clips/mezclas complejas | Solo evaluada conceptualmente; no necesaria para el alcance actual. |

No se ha hecho un benchmark de CPU/GPU ni se promete una mejora de FPS por esta prueba. La ventaja comprobable de la alternativa recomendada es reutilizar el solver y evitar un segundo sistema que compita por los mismos canales. La reducción de duplicaciones aún requiere una refactorización posterior.

## 7. Arquitectura mínima recomendada

Conservar Hand3DFingerState, reconocimiento, modos, solver de Hand3DArmMovement, Hand3DRig como aplicador, coordinador local, bindings/adaptadores de puertas y documentos, y el renderizado existente. No añadir un manager global ni un sistema genérico de capas/eventos.

Unificar solo lo necesario:

- Un formato de perfil capturado para las articulaciones seleccionadas, con referencias y canales explícitos; raíz/muñeca con espacio de cámara identificado.
- Una herramienta común de captura/previsualización, preservando Extended/Flexed y los datos actuales mediante una migración separada y verificable.
- Un orden explícito de composición que reúna base de modo, gesto, pose temporal y acción, antes de solicitar alcance/contacto.
- Una sola aplicación final de huesos, con el solver participando en ese orden. Las acciones futuras entregan contribuciones de pose, no escrituras independientes sobre Transform.
- Contacto/proyección comunes cuando se autorice la fase correspondiente, manteniendo distintas reglas de continuación para puerta y documento.

No hace falta convertir estas responsabilidades en seis MonoBehaviours nuevos. Pueden resolverse inicialmente con datos pequeños y funciones en los componentes existentes.

## 8. Cambios posteriores necesarios, no realizados

1. En Hand3DArmMovement: separar la evaluación de referencia/objetivo de sus escrituras autónomas y permitir una preferencia de codo explícita. Mantener fórmulas, límites y escalas.
2. En Hand3DRig: ordenar composición completa, resolución y orientación final; evitar dependencias circulares y preservar la combinación lógica actual al liberar.
3. En Hand3DModeController, Hand3DPoseRecognizer y Hand3DInteractionPose: referenciar el formato unificado cuando se apruebe su migración, conservando datos antiguos durante comparación.
4. En Hand3DEditors, Hand3DModeEditors y Hand3DDoorEditors: consolidar captura/preview con espacios y máscaras. No reemplazar capturas por esta variante sintética.
5. Más adelante, extraer utilidades comunes de Hand3DDoorInteractor para documentos. Esa extracción no es requisito de este experimento.

Antes de cualquier cambio funcional hay que acordar un alcance pequeño y sus regresiones; no implementar todos estos puntos simultáneamente.

## 9. Pendientes visuales para aprobación final

- Movimiento continuo con ratón/mando a distintas tasas y resoluciones: inspeccionar codo, deformaciones, auto-intersecciones y legibilidad de OK/Point.
- Interrupciones rápidas de gestos y poses temporales; comprobar ausencia perceptible de saltos, además de continuidad numérica.
- Las nueve puertas reales, sus lados/offsets, contacto de la palma y límites inalcanzables.
- Los tres documentos reales, borde de papel, texto y superposición animada del pulgar, incluido cambio de modo y devolución.
- HandEdit con gestos limitados a dedos y su inspección longitudinal, sin aplicar por accidente el perfil completo de Exploration.
- Revisión específica de distribución de twist y silueta de muñeca/antebrazo. No se ha calibrado ninguna pose en esta fase.

La fase termina con viabilidad geométrica demostrada y evidencia visual estática parcial. La refactorización y la aprobación visual dinámica quedan pendientes de revisión del usuario.
