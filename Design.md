Prototipo
Una puerta sin pomo cerrada.
Varias campanas esparcidas por la habitación.
Un recibidor grande, un dormitorio, un despacho, cocina, baño y un pasadizo secreto.
Elementos necesarios para solucionar el puzzle:
Cómo tocar las campanas.
En qué orden.
Por qué las campanas abren la puerta.
En la cocina hay una nota diciendo que el sonido de las campanas abre puertas que no tienen pomo.
En el dormitorio hay una especie de plano con la ubicación de las campanas y un orden.
(El jugador ha de descifrar el mapa).
En el pasadizo secreto se encuentra la manera de cómo tocar las campanas. Se revela que pulsando R2 + X se puede interactuar con el escenario.
Cómo acceder al pasadizo secreto:
En un pasillo hay este icono: Ø aprox.
En el despacho hay una pequeña nota que dice:
“Algunas paredes, cuando no las miras, desaparecen”
y el mismo icono en una esquina.
Volviendo al pasillo con el icono hay que caminar hacia la pared de espaldas; de esa forma la pared se puede atravesar.
Opciones
Por ahora manter saltar con espacio y shift correr. Sin botones asignados en gamepad.
Mecánicas y diseños para el prototipo.
Abrir puertas.
Puzle campanas.
Coger/leer documentos.
Traspasar paredes.
Diseñar casa
Montar iluminación
Nota con la pista de que las campanas abren puertas
Plano ubicación campanas
Manera de como tocar campanas.
Nota simbolo Ø
Sistemas
Poses
Pulsando los botones cuadrado, X, O y triángulo cambias la posición de la mano.
X= Apuntar.
Cuadrado= OK.
Triángulo= Agarrar.
O= Descanso.
Pulsando R2 + el joystick derecho la mano se mueve libremente y el movimiento del personaje se bloquea. Si se pulsa L2 la mano interactúa.
Campanas
Solo se puede interactuar estando a 1.8m de la campana.
Solo se puede interactuar con la pose: Point, pulsando R2 (mantenido) y L2.
La campana se moverá, balanceándose de izquierda a derecha en el eje X de -45º a 45º de una forma suave y orgánica imitando el movimiento de una campana al tintinear.
Se ha de preparar para lanzar un sonido. (Aunque aun no se tiene el clip de audio)
Cada campana, aunque tiene su comportamiento por separado, forma parte de un puzle en el cual, habrá 3 campanas (aunque se podrá escalar a más campanas en el futuro) las cuales se han de tocar en un orden específico. Cuando se han tocado en el orden correcto, se ha de abrir la puerta WallDoor_MainEntranceFront.

Interactuar
Pulsando R2 + el joystick derecho la mano se mueve libremente . Al entrar en contacto con ciertos objetos como una puerta o un documento interactúa automáticamente.
Puertas
El personaje ha de estar pegado o muy cerca de la puerta para poder abrirla.
Con pulsar R2 y acercar la mano al pomo con el joystick derecho:
El sprite de la mano cambia mientras está abriendo la puerta, cuando deja de abrirla vuelve a cambiar a su sprite inicial.
La puerta se abre “pegandose” a la mano como si esta sujetara el pomo. Empujando el joystick aún más la puerta al tener una apertura de unos 10º se termina de abrir del todo automáticamente, no hace falta empujarla entera con el joystick.
La puerta siempre se abre en dirección contraria al jugador.
Las puertas se abren en arco quedando pegadas a la pared en el extremo opuesto al que está el pomo.
Las puertas, una vez abiertas, se quedan abiertas para siempre.
No todas las puertas se pueden abrir solo con la mano. El sistema debe de estar preparado para puertas que necesitan de otro evento para ser abiertas.
Si la puerta no se puede abrir solo con la mano, el pomo temblará ligeramente.
Documentos
A lo largo del escenario se encontrarán documentos con los que el personaje podrá interactuar.
El personaje ha de estar pegado o muy cerca del documento para cogerlo.
Con pulsar R2 y acercar la mano al documento con el joystick derecho:
El sprite de la mano cambia automáticamente, cogiendo el documento, a un nuevo sprite (hand_hold.png) .
El documento siempre será un sprite plano mirando a cámara para que el player pueda leerlo.
El documento se queda “agarrado” (o sea el sprite del documento junto al sprite de la mano, estando la mano en una capa superior al documento para que parezca que lo sujeta) a la mano mientras el jugador mantiene R2. También puede seguir moviendo libremente el brazo y con él, el documento.
Al soltar R2 el documento vuelve a su ubicación inicial en el world space (sobre una mesa, en el suelo, etc) y la mano vuelve también a su sprite inicial.
Sobre el documento:
Como dije anteriormente el documento será un sprite junto a la mano.
Cada documento tendrá su propio sprite.
Los documentos podrán tener texto o no. El texto de los documentos deberá cambiar según el idioma elegido.
Traspasar paredes
Hay ciertas paredes que se pueden traspasar.
Son solidas en todo momento excepto cuando están totalmente fuera de la vista. Si es así se pueden traspasar (Ej: de espaldas)
Una vez traspasadas, y estando el jugador del otro lado de la pared, esta queda traspasable incluso si se está mirando.
Si se vuelve a traspasar al lado original, la pared vuelve a su estado original. (No traspasable. A no ser que no se esté mirando a ella)
Cuando la pared es traspasable, cambia a semi transparente (usa el material Wall_Transparent). Cuando no lo es, deja de ser semi transparente (retoma su material original).
Opciones
Pulsando Start o Esc se congelará la imagen y se mostrará la pantalla de pausa.
Las opciones serán:
Salir del juego: Al pulsar sobre esto se cierra la aplicación.
Cambiar de idioma: Se muestran dos banderas de dos idiomas diferentes, al seleccionar la correspondiente el idioma del juego cambia.
Se puede navegar por las distintas opciones con la cruceta o joystick izquierdo del gamepad o posicionandose encima con el puntero del ratón.
Se aceptan las opciones con el boton South en gamepad o con click izquierdo del ratón.
Al volver a pulsar Start o Esc se sale de la pantalla de pausa.
Idioma
El juego ha de tener soporte para diferentes idiomas.
El idioma por defecto será español (por ahora)
El idioma se podrá cambiar en cualquier momento desde la pantalla de opciones.
Se han de traducir los textos de la UI así como los textos in-game.
Se ha elegido el Asset Localization de Unity. Toda la localización se hará a través de este.



Audio
Puertas
Sonido de no poder abrir. Traqueteo del pomo.
Al abrir: Chirrido de la puerta segun se abre.
Sonido de abrir: Chaqueo de la puerta al abrirse.

Diseño
Pista campanas abren puertas
Español
“Siempre me pareció increíblemente basto y de poca educación aporrear una puerta como quien llama al ganado. En contraposición el sonido de las campanas siempre fue de mi agrado. El tintineo suave y melódico que anuncia la llegada de alguien me parece un acierto en lo que al recibimiento de una persona se refiere.”

Ingles
“I always found it incredibly crude and ill-mannered to bang on a door as if calling cattle. In contrast, I have always been fond of the sound of bells. The soft, melodic tinkling that announces someone’s arrival strikes me as the perfect way to welcome a guest.“

Nota icono invisibilidad
“Me di cuenta que solo tu miraba bastaba
para que yo existiera
pero cuando dejaste de mirarme
la vida entera
dejó de materializarme”





