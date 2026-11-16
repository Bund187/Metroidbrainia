# Aceptación visual pendiente — fase 7

No ejecutar Prepare ni recapturar. Abrir TestArmScene, limpiar Console y anotar backend, resolución, dispositivo y FPS objetivo. Repetir con Universal y Legacy; Profile Pulse solo en Universal. Cambiar el selector para probar no exige guardar la escena.

1. Exploration y HandEdit: alternar en reposo y durante transiciones; inspeccionar palma/dorso.
2. Movimiento libre lento, rápido, diagonal, extremos y retorno; repetir con ratón y mando. No debe aparecer deriva.
3. Accionar los cinco dedos y recorrer combinaciones durante movimiento; revisar OK, Point y orientación de muñeca.
4. PointClick y acciones configuradas: inicio/fin, eventos únicos, simultaneidad y cancelación al cambiar gesto, modo o adquirir objetos.
5. Las nueve puertas: contacto con el pomo, seguimiento al girar, apertura manual/automática y puertas bloqueadas; obstáculos y candidatos próximos; soltar/reintentar.
6. Los tres documentos: recoger desde OK/Point, mover libremente incluso fuera del alcance inicial, cambiar dedos y devolver; conservar 58 % de altura configurada.
7. Mientras se sostiene y devuelve el papel: Thumb Overlay con el pulgar animado delante del documento y el resto detrás. Comparar alineación con la misma RT, incluidos extremos/rotaciones del brazo.
8. Full Arm muestra el brazo completo por delante; Thumb Mask muestra la silueta magenta móvil. Alternar varias veces entre los tres modos, también durante devolución.
9. Texto localizado: cambiar idioma, comprobar fuentes, tamaños y colores individuales; no debe quedar texto del documento anterior al devolver/recoger otro.
10. Cambiar resolución y relación de aspecto (720p/1080p/1440p y 4:3/16:9) mientras se sostiene/mueve; comprobar layout, papel, pulgar y RT.
11. Pausa, Alt-Tab y desactivar/reactivar componentes durante acción, agarre y devolución; sin sesión residual, UI colgada o controles bloqueados.
12. Console sin NaN, Infinity, Quaternion To Matrix, fallback inesperado ni otros errores nuevos.

Registrar PASS/FAIL por backend, pasos y captura si hay diferencia. Las pruebas numéricas y la build no sustituyen esta aceptación visual.
