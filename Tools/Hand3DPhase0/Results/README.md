# Resultado de fase 0

Ejecución final: Unity 6000.6.3f1, copia completa y aislada del proyecto, 9 de noviembre de 2026. Ver `final.json` para identificador y hora UTC y `final.log` para el registro de Unity.

| Grupo | Resultado | Comprobaciones |
|---|---|---:|
| Referencia resuelta | PASS | 7 |
| 32 combinaciones / 15 articulaciones | PASS | 547 |
| Acciones | PASS | 8 |
| Input: clon privado, bloqueo/restauración y pausa | PASS | 10 |
| Movimiento, retorno, modos e inspección | PASS | 15 |
| Interacciones y lifecycle | PASS | 24 |
| Total | PASS | 611 |
| Aspecto real en Game, dispositivos, localización y animaciones completas | PENDIENTE | — |

Los 1.981 archivos originales de Assets, Packages, ProjectSettings y AGENTS.md se comprobaron antes y después: sin cambios. No se sustituyeron valores serializados por defaults. Los objetivos sintéticos de interacción usan el runtime actual y un marcador colocado por la prueba; no acreditan contacto percibido sobre WRAD.

## Intentos de desarrollo del arnés

Se conservan como trazabilidad, no como resultados finales del juego:

- `attempt-1.json`: el marcador sintético no se recolocaba después de que la IK real resolviese el agarre; la adquisición siguiente ya no coincidía visualmente.
- `attempt-2.json`: el empuje sintético era insuficiente para superar el umbral, por el divisor mínimo de píxeles/grado del motor original. Se usa un desplazamiento tangencial de prueba explícito.
- `attempt-3.json`: reflexión ambigua entre las sobrecargas heredadas de OnPopulateMesh. El arnés resuelve también tipos de parámetros.
- `attempt-4.json`: se habilitaban acciones de prueba sin activar PlayerInput en una escena cargada fuera de Play. La restauración del runtime comprueba inputIsActive. El arnés activa PlayerInput mediante su API real antes de comprobar bloqueo/restauración.

Estas correcciones se realizaron solo en el arnés; no se cambió el código de producción para hacer pasar las pruebas. La inspección visual sigue pendiente aunque todas las comprobaciones automáticas hayan pasado. La prueba de poses completas con IK es una propuesta independiente y no se ha ejecutado.
