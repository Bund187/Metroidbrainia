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

Por ahora mantener saltar con espacio y shift correr. Sin botones asignados en el gamepad.
Mecánicas y diseños para el prototipo.
Abrir puertas.
Puzle campanas.
Coger/leer documentos.
Traspasar paredes.
Diseñar casa
Montar iluminación
Nota con la pista de que las campanas abren puertas
Interruptor que hay que pulsar
Plano ubicación campanas
Manera de como tocar campanas.
Nota simbolo Ø
Mapa
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
Interruptores de luz
Solo se puede interactuar estando a 1.8m del interruptor
Solo se puede interactuar con la pose: Point, pulsando R2 (mantenido) y L2.
El interruptor cambiará solo el switch del botón. Encendido es con la parte de abajo más protuberante que la de arriba y viceversa.
Se ha de preparar para lanzar un sonido. (Aunque aún no se tiene el clip de audio)
Apagará o encenderá una luz o varias.


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
Se puede navegar por las distintas opciones con la cruceta o joystick izquierdo del gamepad o posicionándose encima con el puntero del ratón.
Se aceptan las opciones con el botón South en gamepad o con click izquierdo del ratón.
Al volver a pulsar Start o Esc se sale de la pantalla de pausa.
Idioma
El juego ha de tener soporte para diferentes idiomas.
El idioma por defecto será español (por ahora)
El idioma se podrá cambiar en cualquier momento desde la pantalla de opciones.
Se han de traducir los textos de la UI así como los textos in-game.
Se ha elegido el Asset Localization de Unity. Toda la localización se hará a través de este.
Mapa
Al darle al botón select en gamepad o M en teclado se abre el mapa.
Al mostrarse el mapa, el resto del juego se congela.
El cursor se puede mover tanto con joystick derecho como con el ratón
El mapa consta de dos pestañas, mapa y descubrimientos. Se puede navegar entre ellas haciendo click sobre ellas o con Left Bumper y Right bumper
La pestaña mapa muestra un mapa de la casa y se va dibujando a medida que se va descubriendo. Cada habitación tendrá iconos de documentos y/o interrogaciones de cosas por descubrir.
Los interrogantes cambiarán de icono cuando se hayan resuelto.
Al hacer click sobre documentos, interrogantes o interrogantes resueltos se irá al nodo de descubrimiento que le corresponda. 
Las habitaciones se pueden seleccionar, mostrando a la derecha un resumen de la habitación y de las cosas descubiertas en ella.
En el resumen de la habitación aparecerán los iconos de documento, interrogante o interrogante resuelto si los tuviera con el título del descubrimiento al que pertenecen.
La pestaña descubrimientos muestra un esquema de todas aquellas cosas importantes que se hayan ido descubriendo y cómo se relacionan. Al seleccionar un descubrimiento, este se expandirá hacia abajo mostrando un pequeño resumen del descubrimiento.
Se pueden seleccionar tanto habitaciones como descubrimientos con el cursor o navegando entre ellos con los botones de dirección del gamepad.
Cada vez que se entrá en una habitación durante el juego se muestra un gif en la pantalla y sonará un pequeño audio notificando que se ha dibujado una nueva habitación en el mapa.
Cada vez que se interactúe con un documento o con algún objeto durante el juego se muestra un gif en la pantalla y sonará un pequeño audio notificando que se ha dibujado un nuevo descubrimiento en el mapa.
Cada vez que se entre al mapa y haya un nuevo descubrimiento o habitación, habrá una pequeña animación dibujando la nueva animación o descubrimiento, dependiendo de la pestaña en la que se entre.
La animación de las habitaciones consta de un fundido de invisible a visible. Durante la animación el mapa no es interactuable. Al seleccionar una habitación si hay algo que añadir al resumen en texto también aparecerá gradualmente.
La animación de los descubrimientos consta de un fundido de invisible a visible y luego se traza una línea (si aplica) conectando con otro descubrimiento.  Al seleccionar un descubrimiento si hay algo que añadir al resumen en texto también aparecerá gradualmente.
Se podrá hacer zoom tanto en las habitaciones como en los descubrimientos.



Audio
No poder abrir. Traqueteo del pomo.
Abrir: Chirrido de la puerta según se abre.
Abrir: Chaqueo de la puerta al abrirse.
Clic del interruptor de luz.
Chispas en interruptor de luz.
Pasos propios.
Tintineo de campanas
Lápiz dibujando en papel
Pequeño beep cuando se añade algo al mapa.

Diseño
Documentos
Pista campanas abren puertas (Elisabeth)
Español
Recuerdo cuando ya casi no me podía mover. Mi madre me dió una campanita para cuando quería pedir algo. Solo tenía que hacerla sonar y al poco tiempo se abría la puerta y allí estaba mi madre.

Inglés
I remember when I could barely move anymore. My mother gave me a little bell to ring whenever I wanted something. All I had to do was ring it, and soon after, the door would open and there was my mother.

Nota símbolo Ø (Elisabeth)
Español
Cuando empecé a perder la vista. Fué como si las paredes desaparecieran y fantaseaba con poder atravesarlas. También recuerdo ver flashes de un extraño icono, algo así:

Inglés
When I began to lose my sight. It was as if the walls were disappearing and I fantasized about being able to walk right through them. I also remember seeing flashes of a strange icon, something like this:

Cómo usar el apuntar con el dedo. (Elisabeth)
La esquina del pasadizo secreto está llena de imágenes de manos usando el dedo indice para: pulsar un botón, encender una luz, mover un objeto… Sobre la mesa una nota que dice: 
Español
“Eres capaz de más de lo que piensas”
Inglés
You're capable of more than you think

Mapa
Descubrimientos
Campanas
Habitación con campanas
He encontrado una extraña habitación con cuatro campanas y una gran puerta.
Posición: centro.
Se relaciona con: Nada.

Nota campanas
Encontré una nota que mencionaba el uso de una campanilla para abrir una puerta.
Posición: izquierda.
Se relaciona con: Habitación con campanas

Plano campanas
En el estudio hay un plano de una parte de la casa donde había dibujadas varias campanas.
Posición: Arriba.
Se relaciona con: Habitación con campanas

Memo pasaje secreto
Al final del pasaje secreto había varios posters de manos interactuando con campanas y más cosas. Una nota se leía: Eres más capaz de lo que parece, solo tienes que intentarlo.
Posición: izquierda- arriba.
Se relaciona con: Nota campanas

Interruptor defectuoso
En el estudio hay un interruptor echando chispas.
Posición: arriba-izquierda
Se relaciona con: Plano campanas.
Icono transparencia
Icono en la pared
En el comedor hay un extraño icono pintado en la pared.
Posición: arriba-centro.
Se relaciona con: Memo pasaje secreto.

Nota con icono extraño
Encontré una nota hablando de ceguera y traspasar paredes.
Tiene el mismo icono que vi en el comedor.
Posición: arriba-derecha.
Se relaciona con: Icono en la pared

Habitaciones
Recibidor
Notas:no
Interrogantes: 1.
Hay un televisor encendido. Parece emitir noticias.

Habitación campanas
Notas:No
Interrogantes: 1.
He encontrado una extraña habitación con cuatro campanas y una gran puerta.

Sala de lectura
Notas:No.
Interrogantes:No.

Estudio
Notas:No
Interrogantes:2
La habitación está apagada, parece haber un interruptor echando chispas.
(Una vez encendida) Hay un plano de una parte de la casa donde están dibujadas varias campanas.

Dormitorio
Notas: 1
Nota con un icono extraño.
Interrogantes: No.

Baño
Notas: no.
Interrogantes: no.

Comedor
Notas: no.
Interrogantes: 1.
Hay un icono extraño pintado en la pared.
Cocina
Notas: 1
Notas campanas.
Interrogantes: no

Pasaje secreto
Notas: 1
Memo pasaje secreto.
Interrogantes: 1
Hay varios pósters en la pared de manos interactuando con cosas.

Idea global
¿Qué quiero que sienta el jugador?
Miedo/ Suspense: Encerrado en una casa antigua, extraña, oscura. Sentimiento de no estar solo.
Humor: Pequeñas pinceladas, notas, documentos…
Sentimiento de pérdida: Separado de su familia, no podrá ver crecer a su hijo pequeño. Historias de otras personas. 
Crecimiento personal: Siempre pude salir de la casa, solo que no estaba preparado.

¿Por qué el jugador está encerrado en la casa?
El jugador ha muerto y no lo sabe. Se ha desatado el caos en la tierra y gran parte de su población ha muerto.

¿Qué es esta casa, por qué estoy aquí y qué me impide marcharme? 
La casa es una representación del mundo al que van algunas almas que aún no están listas para ascender. En la casa se entremezclan historias de almas perdidas como la del jugador.
Aún no puedes marcharte de la casa porque todavía no has sido capaz de aceptar tu propia vida. 
 
¿Qué tiene que conseguir el jugador para ascender?
Comprender que la vida es pasajera.
No vivir en el futuro, disfrutar el presente y abrazar el pasado.
Aceptarse a sí mismo y a sus acciones.

Notas
Las habitaciones de la casa en principio parecen que pertenecen todas a una misma casa pero poco a poco se va notando que algunas habitaciones pertenecen a diferentes personas. En una misma habitación podemos encontrar notas que hablan de personas ajenas a nosotros. Y poco a poco iremos hilando que esas habitaciones han existido en casa totalmente distintas. Por ejemplo: podremos encontrar una nota que hable de un determinado objeto de la habitación, de un país determinado o vivencias completamente separadas.


Tres pisos, cada uno más abajo. Cuatro o cinco pruebas en cada uno. Solo tres en cada uno son esenciales para terminar el juego.

Grandes misterios:
La puerta cerrada de las campanas.
Unas escaleras “infinitas” tras la ducha.
Un pasillo con un viento que te empuja y no puedes pasar.



Prueba del pasado presente y futuro.
Un pasillo con tres estancias. La primera, el pasado aprendes algo para la siguiente y un puzle. La siguiente, el presente, necesitas algo del pasado, aprendes algo nuevo y un puzle. La última el futuro pones en práctica lo aprendido. Cada estancia tiene un texto: la estancia en la que estás dice: disfruta el presente; la que has pasado dice: este es tu pasado; la de delante dice: este es tu futuro. El objetivo de la prueba es aprender del pasado, disfrutar el presente y no adelantar tu futuro.

El gesto del OK revela cosas que de otra manera no se podrían ver.

Dos puertas oscuras, una al lado de otra. Al entrar en cualquiera se vuelve a la posición inicial. Mediante el signo Ok se revela que secuencia de entrada se ha de hacer para finalmente acceder.

Puedo ponerle un reloj hexagonal o muy característico al jugador en la mano. De esta forma puedo luego identificarlo como el propio jugador cuando quiera revelar que has estado hablando de él durante la historia.

Historia
Antes de empezar el juego se ve una pantalla negra y unas letras que dicen:
“He tenido el sueño más extraño de mi vida
Soñé que estaba en una casa que no era mía
no había ventanas y no encontraba la salida.
No se si he dejado de soñar…”

El personaje se despierta en una casa extraña.

El protagonista tenía una vida soñada. Una mujer que le quería y un niño de 6 años que le idolatraba, un trabajo que pagaba las facturas de sobra pero él quería más, quería viajar más, experimentar más, vivir más, nada era suficiente. En consecuencia de esto empezó a descuidar lo importante: perderse primeras veces de su hijo, faltar a compromisos, no pasar tiempo con su familia, descuidar a su esposa. Hasta que llegó el fin del mundo. Lo pillo solo trabajando en la oficina. No le dió tiempo ni de pensar en nadie. La onda expansiva de la bomba nuclear acabó con él en un segundo.

En su paso por la casa ha de darse cuenta de cómo murió, de lo que perdió teniéndolo todo y de aceptarlo.

Como representar esto:
Imágenes de una vida feliz. Familias felices, niños jugando, besos de pareja, vacaciones. Todo esto en aquellas habitaciones destinadas para el protagonista. Luego esas mismas imágenes tachadas y vandalizadas, una frase enorme escrita en grafiti diciendo “QUIERO MÁS!”, simbolizando que eso se acabó por ansias de más. 
Documento diciendo cosas como: “lo tenía todo, todo aquello por lo que muchos sueñan y lo dejé pasar”
“Nada te llenaba, te perdiste los primeros pasos de Pepito. Tu mujer te echaba de menos”
“Querías más, más dinero, más viajes, más diversión y solo tenías menos”
“El mundo estaba convulsionando, era momento para estar en familia. Tu no pensabas así”
“-Hey, esta noche se sale, te recojo después del trabajo”



Personajes
Elisabeth
Una niña que murió a los 15 años. Fué afectada por una enfermedad degenerativa que la fué dejando inmovil y ciega. Acabó con su vida ahogándose en la bañera. Su paso por la casa la ayuda a entender su suicidió y aprender a perdonarse por el dolor causado a su familia.

Morgan
Un arquitecto alcohólico. Murió junto a varios de sus compañeros al derrumbarse un edificio por una negligencia suya. Su paso por la casa le hace ver sus errores, llorar la pérdida y aceptar la culpa.




Bugs o mejoras
Ajustar el tamaño de los textos y la fuente dinámicamente según el documento.
Efecto del televisor
Las pestañas del mapa han de ser más grandes. Un cuadrado para cada una quedaría mejor.
Zoom tanto en las habitaciones como en los descubrimientos





Base del mapa: abrir/cerrar, congelación, input, dos pestañas y cursor.
Sistema de estado: habitaciones y descubrimientos, Undiscovered/Pending/Revealed.
Habitaciones: triggers, dibujo progresivo y panel de información.
Descubrimientos: nodos, conexiones y panel.
Notificaciones durante gameplay.
Animaciones pendientes al abrir cada pestaña.
Sonidos, pulido y finalmente persistencia/save.

