# Validación visual pendiente

Solo TestArmScene, sin ejecutar Prepare ni reconfigurar perfiles. Repetir Legacy y Universal; ratón y mando.

1. Limpiar Console. Formar Point: índice extendido, pulgar/corazón/anular/meñique flexionados (máscara 29). E/T/F/G alternan esos dedos; R alterna índice. R3/D-pad son los controles de dedos equivalentes existentes.
2. Mantener RMB/R2 y mover la punta por la campana visible, también fuera del centro de pantalla. LMB/L2 debe producir PointClick y un golpe de campana. Mantener el botón no debe producir varios golpes.
3. Soltar y volver a pulsar después del clic: la campana refuerza su oscilación actual. Comprobar que movimiento del brazo permanece libre y no entra en GrabDoor/GrabDocument.
4. Probar fuera de alcance, dedo fuera del collider, obstáculo, candidato oculto y dedo que se mueve rápidamente. Sin contacto debe seguir habiendo clic visual, sin activación ambiental. Comprobar distintas resoluciones/relaciones de aspecto para RT/RawImage.
5. Apuntar al interruptor y pulsar repetidamente: luz/estado alternan solo al aceptar la acción, rocker animado y eventos/efectos configurados. Una nueva pulsación mientras moving no debe saltarse su bloqueo. Los clips actuales están vacíos: sonido es N/A hasta asignar un clip al componente original de la instancia de prueba.
6. En la sala de campanas, probar orden incorrecto y luego BellHinge 1→2→3→4. Debe desbloquearse/abrirse la puerta configurada Wall_Door_ClosedDoor/DoorHinge. No se exige sistema de mapa o progreso de Prototype.
7. Sobre las cuatro campanas próximas, comprobar objetivo único. Para revisar tolerancia/empate se pueden usar copias de prueba o tolerancias temporales explícitas; la entrega mantiene cero en las cinco entradas.
8. En HandEdit, incluso con R2/RMB y Point, solo debe producirse el clic visual. Mantener la inspección y controles de cámara/jugador existentes.
9. Sujetar una puerta y después un documento: LMB/L2 no debe activar objetos Point. Comprobar al liberar que siguen funcionando poses, texto/documento y pulgar superpuesto.
10. Cambiar gesto durante el clic, soltar R2, desactivar/reactivar el adaptador o un objetivo: no deben aparecer activaciones diferidas ni duplicadas. Una campana ya golpeada conserva su animación original; cancelación visual no revierte gameplay ya ejecutado.
11. Revisar Point Fingertip + offset con el índice animado. Se puede usar un Transform hijo y offset cero o ajustar el offset del distal; no cambiar cámaras, modelo ni prefabs para corregir el marcador.
12. Console sin errores nuevos, especialmente Quaternion To Matrix. Revisar también movimiento/retorno, OK, puertas bloqueadas, documentos, máscara y flujo Pose Editor→Runtime como comprobación breve de no regresión.

Registrar PASS/FAIL, backend, resolución, dispositivo y diferencias. Las pruebas automatizadas usan intenciones Mouse/Gamepad y stepping; no sustituyen hardware real, sonido audible ni oclusión final en Game.
