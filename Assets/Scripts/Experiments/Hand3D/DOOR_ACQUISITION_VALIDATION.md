# Validación de adquisición — TestArmScene

## Diagnóstico del código y configuración encontrados

- Radio fijo de 18 píxeles: demasiado pequeño y sin conversión entre resoluciones.
- Solo se medía el extremo de cada fotograma: podía saltarse un pomo al atravesarlo rápidamente.
- La retención reutilizaba la adquisición con otro límite de 80 píxeles. Un desfase durante el ajuste inicial podía cancelar un Grab ya válido.
- Ese intento se marcaba consumido al adquirir, por lo que una cancelación transitoria impedía reintentar hasta soltar R2.
- En la escena guardada se encontró Arm Display sin asignar, Target Door Root vacío y Debug desactivado. El RawImage podía resolverse automáticamente, pero el marco no tenía exclusión explícita.

Se conservan las referencias elegidas por el usuario (incluida Test Door), Grab, poses y datos anteriores. Se conectan PixelArmDisplay y la raíz de la puerta seleccionada y se activa Debug. Test Door sigue filtrando a una instancia cuando está asignada: revisa el nombre que muestra el panel si pruebas otra puerta.

## Qué se ha validado automáticamente

Se han ejecutado los ocho casos siguientes mediante Unity 6000.6.3f1 en una escena temporal con cámaras, RawImage/Canvas y colliders reales. La prueba usa los componentes experimentales, el rig procedural y la lógica original de puerta, y compara adquisición con una copia del detector anterior.

**Límites explícitos:** la entrada está simulada mediante el estado efectivo del controlador, no mediante un R2/ratón físico. Las actualizaciones de componentes se avanzan desde el test de Editor. La copia temporal del motor de puerta usa deltaTime fijo para poder comprobar sus amortiguaciones en modo Editor. No se ha jugado la escena TestArmScene real ni se ha comprobado visualmente la colocación de la palma del modelo WRAD o el dibujo final del panel. Estos resultados no demuestran por sí solos que la experiencia final en Game esté validada.

| Caso | Prueba automatizada | Resultado |
|---|---|---|
| 1 | Mano a 0.95 radios del pomo, puerta a profundidad 0.7 y dentro de 1.8 m. El detector anterior de 18 px rechaza el mismo punto | PASS: Grab con la zona ampliada |
| 2 | Mantener estado de R2 durante 12 fotogramas fuera de la zona; entrar después sin volver a pulsar | PASS: adquiere al entrar |
| 3 | Mover la mano desde -3 radios a +3 radios en un fotograma. Ambos extremos quedan fuera | PASS: adquisición por segmento; detector anterior no la detectaba |
| 4 | Misma coincidencia visual, pomo a profundidad 3 | PASS: rechazo por alcance |
| 5 | Collider sólido independiente entre cámara y pomo; luego el mismo collider bajo la raíz de la puerta objetivo | PASS: la pared bloquea, el marco propio no |
| 6 | Tras adquirir, alejar el marcador más de 80 px y mantener 12 fotogramas | PASS: Grab se mantiene sin volver al filtro visual. También se comprobó reintento tras una obstrucción transitoria |
| 7 | Entregar empuje al motor original, superar el umbral y avanzar apertura automática | PASS: handoff a 10°, termina a 125° |
| 8 | Activar OK, guardar rotaciones de los cinco dedos y offset de muñeca; agarrar y liberar | PASS: combinación, cinco rotaciones y muñeca de OK restauradas |

El resultado de esta ejecución incluye 98 comprobaciones (los ocho casos y regresiones previas de proyección, modos, poses y bloqueo). Compilación sin errores. No se han cambiado controles ni el movimiento del brazo; en Hand3DInputController solo se añade una propiedad de lectura del botón para Debug.

## Repetir la ejecución aislada durante esta sesión

El proyecto temporal, código del test y log quedan en:

- `%TEMP%/MetroidbrainiaArmValidation/Assets/Editor/ArmValidation.cs`
- `%TEMP%/MetroidbrainiaArmValidation/validation.log`

Con esa copia temporal disponible, ejecuta en PowerShell:

```powershell
& 'D:/Programas/UnityHub/Editor/6000.6.3f1/Editor/Unity.exe' -batchmode -nographics -projectPath "$env:TEMP/MetroidbrainiaArmValidation" -executeMethod ArmValidation.Run -logFile "$env:TEMP/MetroidbrainiaArmValidation/validation.log"
```

El log debe contener `CASE_1_PASS` hasta `CASE_8_PASS` y `ACQUISITION_VALIDATION_PASS`. Esta copia es temporal y corresponde al código de esta corrección; no se actualiza automáticamente con cambios futuros del proyecto.

## Protocolo manual obligatorio en la escena real — pendiente

1. **Preparación:** abre TestArmScene, selecciona el adaptador y activa Debug Contact. Confirma que Test Door identifica la puerta que vas a tocar, Arm Display es PixelArmDisplay y Target Door Root incluye solo esa puerta. Acércate a menos de 1.8 m.
2. **Punto percibido:** mantén R2/RMB. El círculo cian debe caer donde la palma/dedos parecen tocar. Si no coincide, ajusta Grip Screen Offset (referencia 1080p). El círculo amarillo debe estar en el pomo. No recaptures poses para corregir el contacto.
3. **Caso 1:** mueve lentamente el círculo cian dentro del círculo grande del pomo. Esperado: Grab al entrar; panel INPUT activo, proyección/alcance/raycast válidos.
4. **Caso 2:** empieza a mantener R2 con la mano fuera de la zona; espera y entra sin soltar. Esperado: adquisición igual que en el caso 1.
5. **Caso 3:** recorre el pomo rápidamente de un lado al otro con ratón y stick. Esperado: Grab aunque ningún extremo caiga dentro; panel “entrada por recorrido: SI”.
6. **Caso 4:** repite alineando desde más de 1.8 m. Esperado: ningún Grab; panel “Fuera de alcance” y distancia concreta. No amplíes el alcance para ocultar este rechazo.
7. **Caso 5:** prueba un obstáculo independiente real entre personaje y pomo, conservando sus capas. Esperado: raycast BLOQUEADO con nombre y capa del collider. Prueba aparte hoja/marco propios: no deben bloquear automáticamente.
8. **Caso 6:** tras adquirir, sigue empujando y comprueba que la mano permanece agarrada mientras se ajusta, sin búsqueda de píxel ni cancelación por separación visual. Suelta para cancelar y prueba una puerta bloqueada.
9. **Caso 7:** empuja unos grados y suelta (debe cerrar); repite y supera 10° (debe liberar y continuar hasta 125°, quedando abierta).
10. **Caso 8:** repite con OK y Point. Comprueba recuperación de dedos, overrides, orientación especial y clic. Entra en HandEdit agarrando: debe cancelar de forma segura.
11. Repite en otra resolución/aspecto de Game y con ambos dispositivos. A 1080p el radio es 55 px; a 720p es aproximadamente 36.7 px. No debe cambiar el tamaño relativo de la zona.

Si falla, registra el panel completo y los dos círculos durante el rechazo. No basta con decir que la mano estaba encima del pomo: el panel distingue INPUT físico/efectivo, proyección, zona/recorrido, alcance, máscara, collider y rechazo del motor original. Desactiva Debug Contact al terminar.
