Core
Progresión basada en conocimiento.
Pilares de diseño
El conocimiento como única recompensa
Sin enemigos
Estética 3d lowpoly
Suspense, terror psicologico
Todo transcurre dentro de la casa.
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
Guardar
Pantalla de titulo
Rediseñar la pantalla de mapa
Añadir parte de lore en la parte del mapa
Reordenar un poco la casa de forma que sea más creíble.
Retocar los controles para que sean más fluidos, sobre todo con el gamepad.
Sistemas
Mano funcional
Cada dedo de la mano se podrá extender o flexionar al toque de un botón. Si el dedo está flexionado se extiende y viceversa al tocar el botón correspondiente.
Existen diferentes poses clave que se conseguirán teniendo cierta combinación de dedos extendidos/ flexionados.
Pulgar e índice flexionados y el resto extendidos = Pose de OK.
Todos flexionados menos el índice = Pose de apuntar.
Se ha de preparar el sistema para más poses en el futuro.
Controles

Dedo
Gamepad
Teclado/Raton
Pulgar
Press right joystick
5
Índice
Cross up
4
Corazón
Cross right
3
Anular 
Cross down
2
Meñique
Cross left
1


Al pulsar el botón de acción (Left Trigger/ Mouse left button) según la pose que se tenga adoptada ocurrirá el evento que corresponda a la pose. Por ejemplo: en pose apuntar, mueve el dedo ligeramente como si hiciera click a un ratón.
Si se presiona el botón de acción pero no se tiene ninguna pose reconocible, no hará nada.
Hay que tener en cuenta que los eventos especiales pueden modificar el movimiento del brazo. Por ejemplo: Pose= OK, Left trigger= Presionado. Hace que la mano se aproxime al jugador imitando como si estuviera mirando a través de la abertura entre índice y pulgar. La mano y el brazo quedan fijas en ese lugar y el joystick derecho simplemente mueve la cámara con la mano fija en la visión del jugador, no importa que right trigger esté pulsado. Bloquea todo movimiento del brazo con el resto de controles.
La mano ha de tener un collider para la interacción física con los objetos. El jugador ha de estar físicamente tocando un objeto para interactuar con él. Por ejemplo: Pose= Índice.  Right trigger= Presionado. El jugador mueve el brazo hasta una campana
Comportamiento de mano y brazo:
Al mantener pulsado Right trigger o botón derecho del ratón se podrá mover el brazo libremente con el joystick derecho o ratón pudiendo abarcar toda la pantalla según se vaya moviendo el brazo.
La posición por defecto del brazo es con la mano ligeramente perpendicular al suelo con la palma hacia la izquierda:

En el modo por defecto, la mano ha de acompañar el movimiento de visión del jugador con el joystick derecho. Quedando fija sin moverse pero trasladándose con el jugador permaneciendo siempre fija en el mismo a la vista.
Al pulsar right bumper o shift se entra/ sale en modo “edición” de la mano
En modo edición. el brazo queda fijo en posición mostrando la palma de la mano claramente. Esta posición es como si el personaje estuviera mirando la palma de la mano de frente:

En modo edición podrá flexionar y contraer todos los dedos y probar las acciones de las poses que quiera.
En modo edición se bloquea el movimiento del personaje y la vista con el joystick derecho.
En modo edición mover el joystick izquierdo / Key A hacia izquierda: Rota el antebrazo y la mano hacia la izquierda.
En modo edición mover el joystick izquierdo / Key D hacia derecha: Rota el antebrazo y la mano hacia la derecha.
Existen límites de rotación: Hacia la derecha el límite es hasta que se vea la palma de la mano completamente y hacia la izquierda es que se vea el dorso de la mano completamente.
Máxima rotación derecha 
Máxima rotación izquierda 
Al volver a pulsar right bumper o shift se sale del modo “edición” de la mano y se adopta la posición del brazo por defecto.
En la posición por defecto del brazo se recupera el movimiento del personaje y el control de la vista con joystick derecho.
En la posición por defecto también se pueden contraer y flexionar los dedos a voluntad del jugador.
En la posición por defecto muchas poses vienen acompañadas de un movimiento de la mano entera:
Pose OK: La muñeca de la mano gira de manera que la O del OK mire directamente a la vista del jugador:

Pose apuntar: La muñeca de la mano gira mostrando el dorso alineado con el antebrazo como si apuntara hacia adelante:

Se ha de preparar para que cada pose tenga su pequeño movimiento de mano en modo por defecto.
el movimiento del brazo permanecerá igual aunque tenga una pose adoptada.
Si se adopta una pose en modo edición y se pasa a modo por defecto la pose se mantiene con su pose de mano pertinente.

Poses
Pulsando los botones cuadrado, X, O y triángulo cambias la posición de la mano.
X= Apuntar.
Cuadrado= OK.
Triángulo= Agarrar.
O= Descanso.
Pulsando R2 + el joystick derecho la mano se mueve libremente y el movimiento del personaje se bloquea. Si se pulsa L2 la mano interactúa.
Tipos de poses
Apuntar

Acción: Mueve el dedo como si pulsara. Puede interactuar con diferentes cosas del entorno.
Posición: Señalando hacia adelante como se ve en la imagen.
Gesto: Todos los dedos flexionados menos el indice
OK

Acción: La mano se acerca al ojo del jugador. Mirando a través de la O se revelan cosas que de otra forma no se verían.
Posición: Por defecto con la muñeca ligeramente levantada y girada viéndose claramente la pose.
Gesto: Pulgar e índice flexionados.

Pistola

Acción: Hace de linterna.


Puzles
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
Son sólidas en todo momento excepto cuando están totalmente fuera de la vista. Si es así se pueden traspasar (Ej: de espaldas)
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

Elementos ambientales
Televisor
Un televisor está encendido por defecto o se enciende en algún momento disparado por un evento.
Ha de mostrar una animación, generalmente en loop, en la pantalla del televisor.
Emite luz desde la pantalla. Esta luz no ha de ser fija sino que parpadea imitando a una televisión.
Un texto se ve encima de la pantalla y se hace más o menos grande dependiendo de la distancia del jugador.
El texto está fijo en la escena pero rota, siempre mira hacia el jugador, de manera que va rotando según este gira para que pueda leerlo en todo momento.
También reproduce un sonido que aumenta o disminuye según la distancia del jugador.
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
Tipo: Interrogante
Posición: centro.
Se relaciona con: Nada.

Nota campanas
Encontré una nota que mencionaba el uso de una campanilla para abrir una puerta.
Tipo: documento
Posición: izquierda.
Se relaciona con: Habitación con campanas

Plano campanas
En el estudio hay un plano de una parte de la casa donde había dibujadas varias campanas en diferentes tamaños.
Tipo: Interrogante
Posición: Arriba.
Se relaciona con: Habitación con campanas

Memo pasaje secreto
En el pasaje secreto encontré una nota que decía: Eres más capaz de lo que parece, solo tienes que intentarlo.
Tipo: documento
Posición: izquierda- arriba.
Se relaciona con: Nota campanas

Interruptor defectuoso
En el estudio hay un interruptor echando chispas.
Tipo: Interrogante
Posición: arriba-izquierda
Se relaciona con: Plano campanas.

Habitación oscura
Una habitación está totalmente a oscuras..
Tipo: Interrogante
Posición: arriba-derecha
Se relaciona con: Interruptor defectuoso.

Posters interactuando
En el pasaje secreto hay varios posters de manos interactuando con cosas colgando de la pared.
Tipo: Interrogante
Posición: arriba-derecha
Se relaciona con: Habitación con campanas.
Icono transparencia
Icono en la pared
En el comedor hay un extraño icono pintado en la pared.
Tipo: Interrogante
Posición: arriba-centro.
Se relaciona con: Memo pasaje secreto.

Nota con icono extraño
Encontré una nota hablando de ceguera y traspasar paredes.
Tiene el mismo icono que vi en el comedor.
Tipo: documento
Posición: arriba-derecha.
Se relaciona con: Icono en la pared

Cuando se marcan como resueltos los descubrimientos:
Habitación oscura e interruptor: Al encender la luz.
Icono en la pared: Al traspasar la pared de espaldas.
Habitación con campanas y plano campanas: Al tocar las campanas correctamente.
Posters interactuando: Al tocar las campanas correctamente O Al encender la luz. 

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
Pantalla de título
Es una pantalla de título sencilla y minimalista.
Tiene una imagen de fondo y hasta cuatro botones.
Los botones serán en forma de texto únicamente. Al pasar por encima cambiarán de color resaltado a rojo brillante. Al pulsarlo volverán a su color original.
Los textos de los botones obtendrán su texto dependiendo del lenguaje de Localization.
Los botones deben de ser legibles en cualquier resolución. 
Se navegará entre ellos con el cursor del ratón y click izquierdo para aceptar o con left joystick/ cruceta y button south para aceptar, para gamepad.
Los cuatro botones en mismo orden que se deben de mostrar:
“Start New Game”: Al clicar, comienza el juego. Si se detecta un archivo de guardado se muestra un pequeño aviso en el centro de la pantalla diciendo: “Esto borrará tu actual partida guardada. Continuar?” junto a dos botones que dicen “Si” y “No”. Si se pulsa “Si” se inicia el juego sobreescribiendo la partida guardada. Si se pulsa “No” se cierra el aviso sin hacer nada más.
Continue: Solo se muestra si se detecta un archivo de guardado. Continua la partida cargando el archivo de guardado.
Options: Al clicar, se mantiene la misma imagen de fondo; los botones desaparecen y se muestra una pantalla con las opciones.
Salir del juego: Al clicar, cierra el juego.
Las opciones:
Lenguaje: se muestran las banderas del lenguaje disponible una al lado de la otra. Similar al menú de pausa del juego.
Como se cierran las opciones: En teclado + raton: Se muestra un icono de cerrar, al hacer click cierra la pantalla de opciones. Gamepad: botón east.
Tanto al comenzar un juego nuevo como al continuar una partida guardada se cargará la escena Prototype. Al estar todo el juego en una sola escena se ha de valorar el crear una pantalla de carga con todo lo que ello implica.

Sistema de Guardado
Se guarda siempre en el mismo slot. No hay diferentes. Desde el menú de inicio se puede continuar la partida guardada o borrar la partida en curso y empezar una nueva.
Lo que se guarda:
Eventos especiales: Puerta campanas abiertas, interruptor del estudio encendido.
Todo lo relacionado con mapa y descubrimientos: Habitaciones, iconos, iconos resueltos, nodos, conexiones, variantes de habitación.
Configuración de las opciones.
Cuando se guarda:
Al cerrar el juego.
Al hacer un nuevo descubrimiento o una nueva habitación.

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

Reglas del mundo:
Colores como números.
Acontecimientos en horas pares o impares

Conocimiento también es aprender cómo se comporta algo. Por ejemplo: la arena en gemelo brasa sube y te puede matar. En el juego puedo poner habitaciones que se comporten de ciertas maneras.

En la tv al principio del juego tengo que poner algo que incita la curiosidad del jugador. Algo que plantee una de las grandes preguntas del juego. Se puede poner un noticiero donde insta a la población a quedarse en casa, cerrar puertas y ventanas, buscar refugio. De esa forma el jugador piensa que el protagonista se ha metido en la casa huyendo de algo.

Humaniza a Elisabeth y Morgan. Que hagan chistes o hablen de cosas comunes, objetivos, pequeñas cosas

Debería de además del mapa y descubrimientos tener un diario para ir apuntando las intrahistorias de los NPCs y la propia

El personaje llega a una estancia donde una de las puertas está abierta pero en el techo de la pared. A la altura del jugador, “encima” de la puerta, se lee “ALÉJATE” si el jugador se aleja lo suficiente y se sube a una silla podrá ver el símbolo de desaparecer escrito en el fondo de la habitación al revés. Esa es la pista para decirle que tiene que entrar de espaldas a la habitación. De esa manera unimos a Morgan y Elisabeth. 


Historia
Antes de empezar el juego se ve una pantalla negra y unas letras que dicen:
“He tenido el sueño más extraño de mi vida
Soñé que estaba en una casa que no era mía
no había ventanas y no encontraba la salida.
No se si he dejado de soñar…”

El personaje se despierta en una casa extraña.

El protagonista tenía una vida soñada. Una mujer que le quería y un niño de 6 años que le idolatraba, un trabajo que pagaba las facturas de sobra pero él quería más, quería viajar más, experimentar más, vivir más, nada era suficiente. En consecuencia de esto empezó a descuidar lo importante: perderse las primeras veces de su hijo, faltar a compromisos, no pasar tiempo con su familia, descuidar a su esposa. Hasta que llegó el fin del mundo. Lo pillo solo trabajando en la oficina. No le dió tiempo ni de pensar en nadie. La onda expansiva de la bomba nuclear acabó con él en un segundo.

En su paso por la casa ha de darse cuenta de cómo murió, de lo que perdió teniéndolo todo y de aceptarlo.

Como representar esto:
Imágenes de una vida feliz. Familias felices, niños jugando, besos de pareja, vacaciones. Todo esto en aquellas habitaciones destinadas para el protagonista. Luego esas mismas imágenes tachadas y vandalizadas, una frase enorme escrita en grafiti diciendo “QUIERO MÁS!”, simbolizando que eso se acabó por ansias de más. 
Documento diciendo cosas como: “lo tenía todo, todo aquello por lo que muchos sueñan y lo dejé pasar”
“Nada te llenaba, te perdiste los primeros pasos de Pepito. Tu mujer te echaba de menos”
“Querías más, más dinero, más viajes, más diversión y solo tenías menos”
“El mundo estaba convulsionando, era momento para estar en familia. Tu no pensabas así”
“-Hey, esta noche se sale, te recojo después del trabajo”
Mensajes de tu mujer a un familiar contándole lo que tu haces sin juicio simplemente esperando que estés más con ellos.



Personajes
¿Cómo te pone a prueba la casa?
Para todos los que pasan por ella debería de ser igual.
Te muestra varias historias, tú no sabes cual es la tuya para que empatices con todas. No te juzga, te deja a ti hacerlo. 
Primero te introduce el desarrollo de cada historia y personaje
Luego te confronta con lo que ha hecho y sus consecuencias
Luego muestra la culpa
Por ultimo redención y ascenso.

 Elisabeth, la culpa de existir:
Era una niña normal. Empezó con la enfermedad poco a poco. Su madre se desvivía por ella. Se culpa del malvivir de su madre. Decide morir para así liberar a su madre de la carga que ella misma suponía.
Consigue suicidarse. Su madre no puede soportarlo y se destroza la vida a base de medicamentos y alcohol.
Elisabeth entiende el sufrimiento de su madre ante la pérdida y valora su propia existencia.
Comprende que su dolor era real pero el de su madre también. Lamenta su decisión al aprender el valor de su vida y se libera de culpa por existir.
Relación con las reglas de la casa:
Incapacidad física: No poder llegar a ciertos lugares? Ver cosas pero no alcanzarlas?
Incapacidad visual. Poder traspasar paredes u objetos al no verlas. 
Culpa por existir. Suicidio para avanzar por ciertos lugares?

Morgan: La culpa de haber causado daño.
Se refugiaba en su trabajo para ocultar su soledad. Muchas horas ocupado. Total productividad. Sentimiento de profundo vacío.
Demasiado estrés. Un fallo decisivo en un plano provoca un derrumbe que mata a 6 trabajadores y a él mismo.
Morgan entiende que si no hubiera forzado tanto su vida a base de trabajo, esto no hubiera pasado y no sería un asesino.
Entiende el valor de la vida, el peligro de los límites y acepta esas 6 vidas como su penitencia.
Relación con las reglas de la casa:
Mediciones complejas: Relacionar números con colores? ¿Cantidad de objetos en una habitación como indicadores para avanzar?
Relación con los espacios: Reglas abstractas sobre habitaciones? Habitaciones dadas la vuelta, geometrías imposibles, mobiliario cambiante…
Conocimiento sobre puertas distintas, diferentes sistemas de apertura o existencia?

Protagonista
Vida perfecta. Mujer encantadora e hijo joven. Trabajo acomodado y sin falta de dinero. Quiere a su familia pero necesita más. Más diversión, excesos, placer, más dinero. La vida sabe a poco cuando lo tienes todo.
El mundo está convulsionando, es momento de estar con los tuyos pero decide otro camino. Mínima atención a su familia y máxima por placeres inmediatos con chutes de dopamina. Como fiestas, otras mujeres y drogas seguidas de sentimiento de culpa en bucle. Finalmente el mundo se rompe, las guerras se acrecientan y algo que él jamás pensó que pasaría ocurre. Una bomba nuclear estalla en su ciudad barriendo todo, amigos, familia y él mismo.
El personaje contempla su vida y nada tiene sentido, se siente inmensamente vacío.
Tenía lo verdaderamente importante delante y no supo apreciarlo. Ya nada puede cambiarlo, solo queda aceptarlo.
Relación con las reglas de la casa:
Mecánica principal de la mano y gestos. Desde el principio tienes algo que vale oro, tu mano. No la valoras (porque no sabes como usarla) pero desde el principio tiene alto potencial. Varios gestos que cambian la interacción con la casa: interactuar con todo tipo de cosas; ver cosas que de otra manera no se puede ver; defenderte o parar cosas que te impiden avanzar, etc.





Bugs o mejoras
Ajustar el tamaño de los textos y la fuente dinámicamente según el documento.
Efecto del televisor
Las pestañas del mapa han de ser más grandes. Un cuadrado para cada una quedaría mejor.
Zoom tanto en las habitaciones como en los descubrimientos
Repensar como diseñar los nodos con la info porque actualmente cabe muy poca información cuando estos se expanden











