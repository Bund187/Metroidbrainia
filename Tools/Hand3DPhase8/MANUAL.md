# Protocolo visual de cierre — pendiente

Solo TestArmScene, con las funcionalidades disponibles. No trasladar sistemas de Prototype. No exigir pausa ni localización como condiciones de aceptación si no están disponibles en esta escena. No ejecutar Prepare/Capture Legacy para validar Universal.

1. Abrir TestArmScene y limpiar Console. En el rig, elegir Universal y comprobar su asset runtime actual; repetir después en Legacy sin guardar cambios involuntarios de escena.
2. Movimiento lento, rápido, diagonal/extremos y retorno; comprobar orientación/IK sin deriva. Controlar por separado los cinco dedos y recorrer OK/Point durante movimiento.
3. PointClick: inicio, pulso, fin y recuperación de pose. Alternar Exploration/HandEdit, inspeccionar palma/dorso y salir a Exploration.
4. Probar puertas normales y bloqueadas disponibles: contacto con pomo, GrabDoor, seguimiento, apertura/liberación y reintento tras soltar. Conservar alcance/tolerancias y reglas existentes.
5. Recoger los documentos disponibles: GrabDocument, movimiento libre, papel y devolución. Pulgar delante del papel, resto del brazo detrás; comprobar Thumb Overlay/Thumb Mask/Full Arm sin errores nuevos.
6. Alternar Universal/Legacy; comprobar que Legacy conserva sus poses originales y que no aparece fallback inesperado. Console sin errores nuevos, en particular Quaternion To Matrix.
7. Fuera de Play, abrir Pose Editor y asignar rig/biblioteca. Duplicar un perfil válido con ID único de prueba, editar/capturar la copia temporal, Restore y Save Library. No recapturar la fuente ni usar un ID funcional existente para esta prueba.
8. En la publicación, marcar solo ese ID y elegir el runtime experimental. Revisar ADD, destino y selección. Cancelar la confirmación y comprobar que no se publica. Repetir y confirmar; otros perfiles/asignaciones deben conservarse.
9. Hacer Undo y guardar el runtime desde su Inspector; comprobar retirada del ID de prueba. Redo y guardar; comprobar recuperación. Publicar no debe alterar HandPoses.asset ni capturas/eventos de escena.
10. Para probar visualmente el perfil nuevo, asignarlo expresamente al grupo de un gesto compatible o al profileId de una acción Profile Pulse ya creada para prueba. Validar asignaciones, usar Universal en Play y comprobar efecto. Un ID nuevo sin asignación debe permanecer inactivo. No es necesario añadir mecánicas de mundo.

Registrar backend, resolución, input, IDs y PASS/FAIL. La creación de prueba modifica deliberadamente la biblioteca/asset escogidos por el usuario: usar copias experimentales si se desea preservar también esos assets durante la revisión. El código entregado no ha publicado nada sobre los assets originales. Deshacer/restaurar las asignaciones temporales al terminar; no borrar las capturas originales.
