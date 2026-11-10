# Fase 0.5: compatibilidad de capturas completas e IK

Arnés descartable de Editor, sin componentes nuevos ni modificaciones de producción. Se conserva íntegra la referencia aceptada de `Tools/Hand3DPhase0`.

## Repetición

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Hand3DPhase05/Run.ps1
```

Por defecto utiliza la copia temporal marcada `%TEMP%/MetroidbrainiaPhase0-20261109`. Puede indicarse otra con `-ValidationProject`. Si no existe, el runner de fase 0 prepara una copia nueva; si existe, exige la marca de origen y verifica el C# y la escena frente a la referencia. Nunca ejecutar `FullPoseIkProbe` en el proyecto de producción.

El arnés C# permanece fuera de Assets. El runner lo introduce únicamente en `Assets/Editor/Phase05Probe` de la copia temporal. Ejecuta primero las 611 comprobaciones originales, sin adaptar sus expectativas; después ejecuta la prueba técnica. Cada ejecución conserva un identificador y un directorio propio bajo Results. Al terminar, incluso con fallo, verifica los 1.981 archivos originales y los archivos de fase 0 aceptados. No guarda escenas ni aplica cambios a prefabs. No ejecuta operaciones de Git ni modifica scripts funcionales.

Requisitos: Unity 6000.6.3f1 con licencia disponible y los paquetes ya instalados localmente. No se introduce un paquete de animación ni una dependencia nueva de runtime.

## Qué prueba

- Jerarquía real de WRAD: `shoulder.r > bicep.r > forearm.r > wrist.r`, quince articulaciones de dedos y dos twists de antebrazo derechos.
- Captura en memoria de la jerarquía, posiciones/escalas y rotaciones locales; raíz y orientación final de muñeca relativas a cámara. La prueba registra toda la jerarquía para detectar efectos colaterales, no propone animarla toda indiscriminadamente.
- Cuatro gestos: OK, Point, GrabDoor y GrabDocument. Para cada uno, una referencia basada en Exploration y otra derivada del ArmFrame HandEdit con una variación explícita de shoulder de (8,-12,5) grados. Esta variante sintética no es una nueva captura del usuario ni se guarda en su escena.
- Pesos 0, 0.5 y 1, desplazamientos horizontales, verticales y diagonales, flexión/extensión y destinos imposibles. Se llama al SolveArm original por reflexión; no se copia ni reimplementa su solver.
- Elbow Hint temporal obtenido de la posición del codo de la postura aplicada. Se compara con la preferencia fija calculada al iniciar el componente.
- Contacto sintético móvil de GrabDoor a través de FollowContact/ResolveContactAfterPose originales, con offset adicional de muñeca de 9 grados. No equivale a abrir las nueve puertas reales con input físico.
- Dedos/twists de sujeción durante movimiento; 100 ciclos de transición y retorno; reanudación desde una captura de la postura mostrada y referencia relativa a cámara girada.
- Recuperación del Point lógico y su orientación especial al terminar GrabDoor/GrabDocument, incluso si el gesto lógico cambia durante el agarre, usando el rig actual.
- Control negativo: reaplicar las rotaciones capturadas después del IK debe destruir un objetivo desplazado. Evita aceptar un arnés que no detecte conflictos de escritores.
- Renders aislados URP de la deformación CPU de WRAD con sus materiales originales, fuera de la composición final de Game. Ver los límites en REPORT.md.

## Datos y unidades

- `baseline.json`: repetición de las 611 comprobaciones.
- `report.json`: grupos, máximos, notas, jerarquía y alcance de validación.
- `captures.json`: capturas completas temporales y correspondencia de arrays con rutas de huesos.
- `measurements.json`: muestras individuales con destino solicitado, limitado, alcanzado, codo y errores.
- PNG: inspección aislada del modelo; variantes 0=referencia Exploration, 1=esa referencia con movimiento, 2=referencia alternativa con movimiento.
- Logs y `integrity.txt`: ejecución e integridad de originales/referencia aceptada.

Los errores de píxel se calculan mediante viewport multiplicado por 1920x1080, no mediante la resolución 320x180 de la RT. Son medidas geométricas en píxeles de referencia; no son diferencias de imagen rasterizada del Canvas. En destinos imposibles se mide el error frente al destino limitado, no se afirma que se alcance el solicitado. Las muestras fuera de los planos de cámara solo tienen evaluación espacial.

La orientación final de muñeca se restablece deliberadamente después del solver: el error medido comprueba la operación ejecutada, no demuestra que un consumidor posterior no pueda sobrescribirla. Las regresiones originales, el control negativo y la inspección visual complementan esa comprobación.

No se han medido mejoras de FPS. El objetivo es viabilidad y elección de la solución más sencilla, no implementar ni optimizar aún el sistema funcional.
