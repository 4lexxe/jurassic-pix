# Jurassic Pix

Nacés como una cría de dinosaurio en una isla de unos 4,15 km de ancho, con cinco lagos, cordilleras, seis regiones y costa, sin caminos. Elegís la especie en el menú de inicio y, en tercera persona, tenés que comer, beber, crecer y sobrevivir hasta ser adulto.

Abre esta carpeta desde Unity Hub con **Unity 6.6 (6000.6.0f1)**. Abre [Expedicion.unity](Assets/JurassicPix/Scenes/Expedicion.unity) y pulsa **Play**. La escena ya está incluida en el perfil de compilación. El proyecto utiliza Built-in Render Pipeline y está preparado para jugar en horizontal.

Para la entrega de Programación de Videojuegos II, consultá el [informe en PDF](output/pdf/Informe_Jurassic_Pix.pdf) y la [revisión de la consigna en el código](Docs/Avance_de_diseno.md#revisión-de-la-consigna-de-entrega).

| Acción | PC | Pantalla táctil |
| --- | --- | --- |
| Moverse | WASD o flechas. Mantén Shift para correr | Arrastra en el lado izquierdo. Lleva el mando al borde para correr |
| Mover la cámara | Ratón. Esc libera el cursor y un clic vuelve a capturarlo | Arrastra en el lado derecho |
| Elegir especie en el menú de inicio | ← → o 1–5; Enter, Espacio o clic para nacer | Tocá la tarjeta de la especie y después **Nacer** |
| Atacar | Clic izquierdo o F | Toca **Atacar** |
| Defender el frente | Mantén G o el botón derecho del ratón | Mantén **Defensa** |
| Agacharse y avanzar al acecho | Mantén Ctrl, o C en tierra | — |
| Beber, pastar o comer | Quedate quieto y mantén E junto al agua, sobre el pasto (herbívoros) o al lado de un cadáver (carnívoros) | Mantén **Beber**, **Pastar** o **Comer**, que aparece arriba de **Defensa** cuando hay algo al alcance |
| Liderar a tu manada o grupo, o dejar de hacerlo | L | Tocá el cartel del grupo, debajo de la tarjeta |
| Volar con Quetzalcoatlus | Espacio para despegar o aterrizar. R y C cambian la altura. Shift acelera | Toca **Despegar** o **Aterrizar**. Mantén **Subir** o **Bajar** para cambiar la altura |
| Volver a nacer con otra especie | V | Abrí el panel y tocá **Volver a nacer con otra especie** |
| Abrir o cerrar el panel | Tab | Tocá las tres rayas, arriba a la derecha |
| Cambiar entre día y noche | N | Tocá el sol o la luna, arriba a la derecha |
| Adelantar el clima | H | Abrí el panel y tocá **Clima** |
| Cambiar el minimapa entre vista cercana, isla completa y oculto | M | Tocá el minimapa |
| Abrir el menú multijugador | F2 | Tocá **Multijugador** en el menú de inicio o en el panel |
| Alternar pantalla completa y ventana | F11 | Pantalla completa al iniciar |

En Windows el juego inicia en pantalla completa con la resolución del monitor. El día dura 20 minutos y la noche 10, así que un ciclo completo de iluminación lleva media hora. La partida empieza a la mañana, así que tenés casi todo el día para crecer antes de la primera noche. **N** cambia la hora y mantiene la transición gradual de cielo, luz y niebla. En red, el anfitrión controla los cambios manuales de hora y de clima.

## Nacer, crecer y sobrevivir

- **Menú de inicio.** La partida arranca en un menú con la isla viva de fondo: la cámara gira alrededor de la especie elegida mientras ves su dieta, cómo es su infancia y sus barras de vida, ataque, velocidad y aguante. Abajo hay una tarjeta por especie. **Nacer** te pone en el mundo.
- **Nacés como cría.** Empezás con el 36 % del tamaño adulto, una cuarta parte de la vida, menos velocidad y un golpe débil, al lado de tu manada o grupo (el T. rex nace solo). Las crías tienen la cabeza grande y la voz aguda.
- **Crecés si te cuidás.** Llegar a adulto lleva unos 8 minutos, pero solo crecés mientras la sed y el hambre estén por debajo del 80 %. Al 30 % pasás a juvenil y al 100 % sos adulto. El Quetzalcoatlus vuela desde el nacimiento y solo un adulto puede liderar a su grupo con **L**.
- **Sed y hambre.** Suben solas. Si alguna llega al máximo perdés vida de a poco hasta quedar en un 25 %, y dejás de crecer. Mantené **E** para beber en una orilla, pastar si sos herbívoro o comer de un cadáver si sos carnívoro.
- **Peligros.** Los depredadores prefieren presas chicas y heridas, así que una cría sola es el blanco ideal. Quedarte entre los adultos de tu manada es la mejor defensa. El agua profunda también ahoga, aunque a tu criatura le da tiempo de salir.
- **Si morís.** La pantalla muestra cuánto viviste y en qué etapa estabas. A los 12 segundos volvés a nacer como cría de la misma especie; con **V** podés abrir el menú y elegir otra.

El modo explorador a pie, el cuaderno de avistamientos y el cambio libre de criatura con 1–5 ya no existen: siempre jugás como el dinosaurio que elegiste.

## Interfaz

La pantalla de juego muestra solo lo vital y sin recuadros. Arriba a la izquierda, la silueta de tu especie, el nombre con la etapa y el porcentaje de crecimiento, una línea de vida, una más fina de aguante y dos cortas de sed y hambre; los rótulos **SED** y **HAMBRE** aparecen recién cuando pasan de la mitad. Debajo hay una sola línea de texto: qué hace tu grupo o, si algo anda mal, el aviso (te ahogás, necesitás agua, sin aliento). Arriba a la derecha queda solo el sol o la luna. Abajo, el minimapa con el conteo de crías de tu manada y los atajos del panel y del mapa.

**Tab** abre el panel: a la izquierda tu dinosaurio (etapa, barra de crecimiento, tiempo con vida, vida, sed, hambre y consejos para sobrevivir) y a la derecha volver a nacer con otra especie, la hora, el clima, el multijugador y la lista de controles. Las opciones del panel también se pueden elegir con el ratón.

## Multijugador

Abrí **Multijugador** desde el menú de inicio en Android o con **F2** en PC. Escribí **tu nombre** para que los demás lo vean sobre tu dinosaurio.

### Unirse con código

1. En el equipo anfitrión, elegí **Crear partida por internet** para jugar desde redes distintas o **Crear partida local** si todos están en la misma red Wi-Fi.
2. Tocá **Copiar código** y compartilo con tus invitados. En **Multijugador**, pueden escribirlo o pegarlo y tocar **Unirse con código**. Los códigos locales empiezan con **JP-**.
3. Cerrá el menú con **Volver al juego** o **F2**. Cada participante nace como cría de una especie libre y puede volver a nacer con otra desde el menú de inicio (**V** en PC). Las especies que ya tiene otro jugador figuran como ocupadas. El anfitrión puede cambiar el ciclo de día y noche para todos.

El modo por internet usa **Unity Relay** para conectar jugadores sin abrir puertos. La opción aparece en las builds vinculadas a Unity Services. Para compilar una copia del proyecto, comprobá el vínculo en **Edit ▸ Ajustes del proyecto ▸ Services**. El código local funciona sin Unity Services.

### Directo, por red local o IP pública

1. En el menú **Multijugador**, desplegá **Conexión por IP y puerto** y elegí **Alojar**. El menú muestra la dirección IPv4 y el puerto que deben usar los invitados. El puerto UDP predeterminado es **7777**.
2. En cada equipo de la misma red, desplegá los mismos controles, ingresá la dirección y el puerto del anfitrión y elegí **Unirse**. Para probar con dos instancias en el mismo equipo, usá **127.0.0.1**. Por internet también funciona si quien hospeda abre o redirige el puerto UDP en su router y comparte su IP pública.
3. **Servidor** abre la partida sin ocupar un dinosaurio; quien lo corre solo observa, y con ← → cambia la especie que sigue la cámara. También se puede lanzar sin menú con `JurassicPix.exe -batchmode -nographics -servidor -puerto 7777`. Para unirse desde el arranque: `-unirse direccion:puerto`.

La partida admite hasta cinco jugadores. Usá la misma versión del juego en cada equipo. Al desconectarte, tu criatura vuelve a ser un adulto silvestre y la especie queda disponible. Si el anfitrión cierra la partida, los invitados vuelven al menú de inicio en modo individual. El anfitrión simula el crecimiento y lo envía en cada instantánea.

Al crear una partida por internet, la región de Relay viene configurada en **São Paulo**. Podés pasarla a **Automática** desde el menú de multijugador si juegan en otra región.

El anfitrión simula la fauna, el ciclo de vida, la guardia, los impactos y las reapariciones. En condiciones normales manda 30 actualizaciones por segundo con las posiciones de los jugadores y un tercio de la fauna en cada una. Si se pierde una actualización, la siguiente puede llegar sin esperar una retransmisión. Los invitados mandan los cambios de dirección importantes en el mismo cuadro. Los cambios de criatura, ataques, órdenes de vuelo y la lista de nombres siguen viajando por canales fiables. El nombre viaja en la solicitud de conexión y el anfitrión difunde la lista cada vez que cambia.

Para comprobar una compilación de red, ejecutá dos instancias y probá mover y atacar con cada jugador, alternar día y noche desde el anfitrión, elegir una criatura ocupada y volver a entrar tras desconectarte. Compará el estado de la fauna en ambas ventanas y revisá los registros si hay diferencias.

## Estilo PSX cinematográfico

La piel, la vegetación y el suelo tienen una capa especular que refleja el cielo a ángulos rasantes. Durante la tormenta se acumula humedad, aparecen zonas más pulidas en el suelo y un matiz aceitoso en los reflejos. Los relámpagos iluminan esa película y el brillo húmedo se desvanece después de la lluvia. En PC, pulsá **H** para avanzar el clima. El efecto corre en el shader de superficie del Built-in Render Pipeline y aproxima el entorno por dirección de vista.

La cámara renderiza el mundo a **427 × 240** con escalado **Point**, **32 niveles RGB por canal** y dithering ordenado **Bayer 4 × 4**. Los materiales ajustan los vértices a una grilla de medio píxel y la niebla varía con la distancia y el hábitat. En **Vista PSX → Psx View**, **Retro** regula cuánto de ese tratamiento se aplica y está en **15 %**: la imagen se renderiza cerca de la resolución de pantalla (unas 708 líneas en 1080p, con píxeles de 1,5 en vez de 4,5), y la paleta reducida, el dithering y la trama se mezclan al 15 % sobre la imagen limpia. Con **Retro** en 1 vuelve el aspecto PSX completo de 240 líneas. **Stipple** tiene una intensidad del **14 %**. En la silueta de la fauna aplica alrededor del **1,4 %**. **CRT Scanlines** y **Film Grain** están apagados. La interfaz conserva su resolución.

## Audio ambiental y 3D

De día el coro de pájaros aparece de a ratos, entre 9 y 20 segundos, con silencios de 14 a 36 segundos, y cada vez entra desde otro lado y se desplaza despacio entre los parlantes. De noche lo reemplazan los grillos. El viento aumenta hacia la cordillera y hay llamadas de aves situadas en puntos del bosque. Cerca de los lagos y de la costa se escucha agua corriendo, y en el mar se suma el oleaje. La cámara escucha las voces de cada especie, sus pisadas, barridos e impactos desde la posición de la criatura. Quetzalcoatlus tiene aleteos y pisadas de aterrizaje. Al vadear, las pisadas chapotean; al comer o pastar se escuchan los bocados y al beber, los lengüetazos. El panel y el menú suenan al abrirse.

La mezcla está pensada para que no suene todo a la vez:

- **Distancia.** Cada voz, pisada y golpe cae de forma inversa a la distancia en vez de lineal: lo que está cerca domina y lo lejano queda apenas de fondo. Las voces llegan de 60 a 200 m según el tamaño del animal.
- **Paneo.** De cerca el sonido se abre entre los dos parlantes en vez de saltar entero a un lado, y a medida que se aleja se ubica con precisión. Tu propia criatura suena casi centrada y sin atenuación. El agua se escucha del lado donde está el lago o el mar.
- **Menos voces juntas.** Solo los tres caminantes más cercanos suenan a volumen completo, los nueve siguientes van bajando y el resto de la manada no suena. Cada manada llama una vez cada 8 a 15 segundos en lugar de hacerlo cada animal por su cuenta, y no se pisan más de dos rugidos de ataque por segundo. Lo que está fuera del alcance del oído no se reproduce.
- **Volumen.** Las voces, los golpes, las pisadas y los fondos de aves, grillos y agua subieron entre un 15 y un 30 %.

Los apoyos alternan su posición a izquierda y derecha. Los obstáculos atenúan las frecuencias agudas de las voces. Argentinosaurus, Triceratops y T. rex sacuden suavemente la cámara al pisar cerca, con más fuerza durante la carrera. Las criaturas de menos de 5 m, como el Velociraptor, no generan temblor. Varias criaturas corriendo cerca superponen los pasos y activan el sonido de estampida. El viento del Quetzalcoatlus crece con la velocidad y cambia de lado según su dirección de vuelo. Su llamada es una interpretación para el juego basada en una grabación de aligátor. [Grabación utilizada](https://freesound.org/people/D.jones/sounds/527844/).

Los eventos de la fauna usan las animaciones y el estado ya sincronizado por la partida. Cada equipo mezcla los sonidos desde su propia cámara. Las muestras provienen de Freesound y de OpenGameArt y son **CC0**, salvo las pisadas de Tyrannosaurus, Triceratops y Argentinosaurus: salen de `pisada.mp3`, un golpe grave con dos impactos que se recortó en dos clips (`Pisada_Gigante_1` y `_2`) que se alternan entre un paso y el siguiente. También son propios del proyecto el trueno (`trueno.mp3`), los grillos (cuatro grabaciones `grillo*.mp3` mezcladas en un bucle de 36 s), el viento (`sonido_viento.mp3`), el agua (`agua.mp3`) y el fondo de pájaros (`pajaros.mp3`); los bucles se recortaron y se empalmaron con un fundido para que no se note la repetición. El aguacero, el trueno largo, el trueno lejano, las pisadas en agua y los bocados son descargas CC0 de OpenGameArt que quedan en `Tools/sonidos_cc0` y se convierten con `python Tools/import_sounds.py`. Los créditos se incluyen con el juego. [Créditos de audio](Assets/StreamingAssets/Creditos-de-audio.txt).

## Fauna, isla y clima

- La isla empieza con **104 criaturas**, entre ellas seis Tyrannosaurus, tres con pelajes propios. Las dos manadas originales de Triceratops tienen el doble de filas (14 y 12 adultos) y hay cuatro manadas nuevas con pelajes propios: Triceratops rojizos en la Pradera Central, Triceratops verdosos en el Valle del Norte, Argentinosaurus oscuros en la Costa Sudoeste y Velociraptor oscuros en el Bosque del Sur. Tres manadas de **ocho Argentinosaurus** tienen filas escalonadas y adaptan la separación al ancho y largo del animal. Los Velociraptor flanquean en grupo y los tiranosaurios cazan por separado. Los Quetzalcoatlus anidan en las sierras del sur y las sobrevuelan en bandada.
- La isla no tiene senderos: es terreno salvaje. El mar tiene oleaje y espuma cerca de la playa. La laguna del este tiene orillas poco profundas, y hay tres charcas más repartidas por la isla (al sur de la bahía, en el llano centro-oeste y en la meseta norte) para que ninguna manada viaje lejos en busca de agua. Las criaturas terrestres avanzan más despacio al vadear y pierden vida en aguas profundas. Los Triceratops no se ahogan y, si se meten en agua honda, vuelven solos a la orilla.
- El minimapa se genera a partir de la costa, las elevaciones, la laguna y la ubicación de los árboles. Tiene cuatro vistas que se cambian con **M** o con el botón de la esquina: cercana (560 m, 980 m en vuelo), media isla (1.400–1.600 m), la isla completa en grande y oculto. Tocando el mapa se marca un punto del mundo que se ve en el mapa y sobre el terreno con su distancia; tocando la marca se borra. En partidas en red los demás jugadores aparecen con su nombre. Tu posición se marca con una flecha amarilla y un recuadro que late, tu manada en verde claro y los depredadores en rojo.
- Cada unos tres minutos se levantan ráfagas y hay un 30 % de probabilidad de que traigan una tormenta con lluvia y relámpagos; si no, el cielo sigue despejado. La secuencia cambia en cada partida. **H** salta a la próxima tormenta o, si ya llueve, despeja. Las nubes se desplazan y los árboles se flexionan con el viento.
- **Tormenta.** La lluvia baja la visión de más de un kilómetro a unos 250 m (150 m de noche): la niebla empieza a 12 m y el cielo se funde con la bruma. Caen hasta 3.400 gotas por segundo alrededor de la cámara, una cortina de trazos inclinados por el viento cubre la pantalla, el suelo y los lomos se oscurecen y chispean con las gotas que rebotan, y el agua de los lagos se llena de puntos claros. Suenan dos capas de lluvia: sobre hojas y, cuando arrecia, un aguacero.
- **Rayos y truenos.** Cada tormenta trae siete rayos, cada uno con su rumbo y su distancia, entre 140 m y 1,3 km. El rayo se ve en el cielo, con una rama, y el destello ilumina todo el paisaje. El trueno llega después según la distancia y se impone sobre todo lo demás: las cuatro grabaciones, del estallido seco al retumbo largo, suenan el doble de fuerte que antes, debajo de cada estallido se suma el retumbo largo, y mientras truena la lluvia, el viento y los fondos bajan a la mitad. Suena del lado donde cayó el rayo.

## Depredadores y saurópodos

- **Tyrannosaurus rex** patrulla su territorio y caza cuando tiene hambre. Prefiere presas chicas o heridas, se acerca caminando y embiste a unos 26 m o cuando la presa huye. Abandona la persecución si no la alcanza en unos 34 segundos y se retira con poca vida. Tras matar come junto al cadáver, recupera vida y pasa más de un minuto sin cazar. También aprovecha cadáveres ajenos y solo ataca a un Argentinosaurus malherido.
- **Velociraptor** caza en manada con una presa compartida. Persiguen por los flancos a la presa que huye. Si planta cara, la rodean corriendo en círculo y entran a morder por turnos desde los costados y la espalda. Necesitan compañeros para atacar presas grandes, comen juntos el cadáver y abandonan a las presas que resisten demasiado. Huyen de un T. rex sano y lo vigilan a distancia, pero atacan a uno malherido si son suficientes.
- **Argentinosaurus** ya no aguanta en bloque. Cuando un T. rex de buen tamaño se acerca a unos 34 m o lastima a uno, la manada escapa corriendo en la dirección que marca su guía, y solo uno o dos adultos sanos, los más cercanos al peligro, se quedan de guardia: le dan la espalda y barren con la cola. Como se cansan a los 12 segundos de carrera, los rezagados quedan al alcance. Un guardia malherido abandona y vuelve con la manada. Tienen algo menos de vida, daño y armadura que antes. La manada hace pausas para descansar durante su recorrido.
- La fauna silvestre recupera vida lentamente cuando lleva un rato en calma.
- Los depredadores apuntan adonde va a estar la presa que huye, prefieren a los Triceratops que se alejaron de su manada y el T. rex camina hasta el último lugar donde vio a una presa que perdió de vista. Un animal que queda trabado contra un obstáculo da un rodeo. La fauna terrestre mira el terreno que tiene por delante y bordea las montañas y las pendientes de más de unos 48° en vez de empujar contra ellas.
- De noche los herbívoros detectan a los depredadores desde más cerca.

## Combate y defensa

Cada especie tiene su propia vida, daño, armadura y resistencia en guardia. Defender reduce el daño que llega de frente y la velocidad de desplazamiento. Los ataques laterales y traseros evitan esa protección. El jugador vuelve a nacer como cría 12 segundos después de morir. Los Triceratops silvestres muertos liberan su lugar tras 45 segundos para futuros nacimientos, y el resto de los cadáveres silvestres dura lo mismo. El resto de la fauna silvestre reaparece en su territorio.

## Isla, lagos y cielo

- La isla mide el doble de largo que antes. Tiene cinco lagos repartidos, con orillas de arena de pendiente suave (unos 12°) para que la fauna entre y salga sin quedar atrapada, y cinco macizos nuevos; todas las montañas son casi el doble de altas.
- El bosque se dibuja con un componente propio, **Forest Canopy**, que instancia los árboles y arbustos por celdas alrededor de la cámara. Usa solo los modelos de los packs de paisaje (abetos y pinos de montaña, ponderosas y el Low Poly Forest Tree Pack); ya no quedan árboles ni plantas de estilo PSX.
- La noche es oscura de verdad: el cielo es casi negro, con dos capas de estrellas que titilan, una franja tenue de Vía Láctea, estrellas fugaces ocasionales y una luna con halo que marca de dónde viene la luz. Las nubes se ven como siluetas oscuras que tapan las estrellas y solo se aclaran cerca de la luna. El terreno, el agua y la lluvia quedan apenas iluminados por la luna, con un reflejo suave sobre el agua, y la niebla cierra en negro a unos 170 m, así que se ve poco a la distancia.
- Con **E** tu criatura bebe si tiene agua bajo las patas o delante del hocico, pasta si es herbívora y come si es carnívora y hay un cadáver al alcance, con las mismas animaciones que la fauna silvestre. Beber baja la sed y recupera el aliento tres veces más rápido; comer baja el hambre y devuelve vida con cada bocado (al pastar, solo mientras quede hambre). Cuando la acción está disponible aparece el aviso **E** abajo en el centro, y en red la ven todos los jugadores.
- De noche la fauna duerme y respira despacio. Los Triceratops y los Argentinosaurus se echan sobre la panza, con las patas plegadas y la cabeza baja. El Quetzalcoatlus duerme apoyado en sus cuatro extremidades, con las alas plegadas y el pico bajo. El T. rex y el Velociraptor se echan de costado. Duermen las manadas de Triceratops alrededor de su centinela, los grupos que no tienen hambre ni sed y los T. rex en su territorio. Se despiertan si aparece una amenaza.
- Las criaturas beben y pastan con animaciones propias, y una pareja de Triceratops se corteja unos segundos antes de que la hembra vaya al nido.
- Hasta seis Velociraptor pueden colgarse a la vez de una misma presa, tres por flanco.

## Rutinas y territorios

Cada grupo de fauna recorre la isla con un objetivo, y vuelve a decidir cuando lo cumple.

- **Triceratops:** la manada sigue a su líder a la laguna o a la costa para beber, a los pastos para comer y a los extremos de su territorio.
- **Argentinosaurus:** cada manada camina en formación detrás de su guía hasta la orilla más cercana para beber y hasta un árbol gigante para ramonear, con el cuello estirado hacia la copa. Entre una cosa y otra recorre su zona y descansa.
- **Tyrannosaurus rex:** va solo a beber a la orilla y, cuando tiene hambre, elige la manada de presas más cercana y camina hacia ella, agachado en el tramo final. Si no consigue nada, prueba con otra manada.
- **Velociraptor:** el grupo tiene un guía que decide adónde ir y a qué presa atacar; los demás atacan a la que él marca. Si controlás un raptor, la presa que golpeás pasa a ser la del grupo. Cuando un raptor muerde a una presa bastante más grande, se le cuelga del lomo unos segundos y la sigue mordiendo, hasta que la presa ataca, cae o lo sacude. Con tu criatura, Ctrl te suelta.
- **Quetzalcoatlus:** la bandada vuela hasta la orilla, aterriza para beber y vuelve a despegar. Cuando tiene hambre vuela hasta un grupo de presas chicas, baja en picado sobre la más débil y aterriza a comer lo que mata.

Con **L** tu criatura, una vez adulta, pasa a ser la líder de su manada o grupo. Mientras liderás, los demás te siguen en formación, trotan si corrés y se quedan con vos cuando te detenés: beben si paraste junto a una orilla, comen si hay un pasto o un árbol gigante cerca y, si no, descansan. Los Velociraptor atacan a la presa que vos golpeás y los Quetzalcoatlus vuelan y aterrizan con vos. Un Triceratops que lidera crece como cualquier líder. Si la manada de Triceratops detecta un depredador, reacciona por su cuenta hasta que pasa el peligro. Dejás de liderar con **L**, al volver a nacer o al morir, y el grupo vuelve a su rutina.

Con una criatura elegida, debajo de su tarjeta aparece qué está haciendo su grupo. El minimapa es circular. Una aguja amarilla marca tu posición y hacia dónde apunta tu criatura, y un abanico claro que sale de ella muestra hacia dónde mira la cámara. Cada manada y cada T. rex aparece con la silueta de su especie: verde claro tu grupo, rojo los depredadores, celeste los voladores y arena los saurópodos. Un rombo amarillo marca adónde va tu grupo y los cuadrados naranjas marcan las manadas que un depredador puede cazar.

La fauna que no es Triceratops decide con una máquina de estados: tranquilo, en camino, bebe, come, descansa, acecha, persigue, ataca, huye y en guardia. Cada estado tiene un tiempo mínimo antes de poder cambiar, y una huida se sostiene unos segundos aunque el peligro deje de verse, para que el animal no dude entre dos conductas.

Una criatura que queda con el agua por encima de las patas deja lo que está haciendo y sale hacia tierra firme, sin frenarse por la pendiente de la orilla. Las manadas de Triceratops se reparten a lo largo de la orilla para beber en vez de amontonarse en un punto.

## Aguante, tambaleo y derribos

Cada especie puede correr un tiempo distinto antes de quedarse sin aliento: Velociraptor 30 s, Tyrannosaurus 26 s, Triceratops 22 s, Argentinosaurus 12 s y Quetzalcoatlus 9 s en tierra. El aguante se recupera al caminar y más rápido al detenerse. Sin aliento, la criatura tambalea, jadea con la boca abierta y no puede volver a correr hasta recuperar un 35 %. La barra fina debajo de la vida muestra el aguante. El vuelo no lo gasta.

Un golpe que no se bloquea hace tambalear al que lo recibe: pierde velocidad y se balancea durante un momento. Si el atacante es bastante más grande y el golpe llega de costado, por detrás o cuando la víctima está sin aliento, la derriba: cae sobre un costado, patalea y se reincorpora unos segundos después, sin poder moverse ni atacar mientras tanto. Un mismo animal no puede ser derribado más de una vez cada 14 segundos. Así un T. rex puede voltear a un Triceratops al que alcanza en la persecución.

Los depredadores comen los cadáveres con una animación propia: bajan el cuerpo, hunden la cabeza y arrancan bocados. Con tu criatura, mantené **E** o agachate junto a un cadáver para comer y recuperar vida.

## Ciclo de vida de Triceratops

La Bahía del Sur y la Laguna Oriental empiezan con dos crías y un juvenil por manada. Las crías eligen un adulto protector, juegan cuando están a salvo y llaman a la manada si se aíslan. Al detectar un depredador a lo lejos, los adultos cierran filas mirando hacia él y las crías buscan reunirse con el grupo. Si se acerca a menos de 27 m o hiere a un miembro, la manada huye en estampida detrás de su líder, con las crías pegadas a su rastro y los adultos en dos filas, y no frena hasta poner unos 46 m de distancia. Ya no planta cara entera: solo uno o dos adultos sanos, los más cercanos al peligro, se quedan a cornear al atacante para cubrir la retirada. Las crías corren más despacio y son las primeras en quedar atrás. Los Triceratops tienen algo menos de vida, daño y armadura que antes.

Cada manada sigue a un líder, el adulto sano de mayor edad. El líder decide el próximo destino: la laguna si hay sed, un pasto si hay hambre o, si no, el extremo más lejano del territorio. La manada viaja en filas detrás de él, el líder se detiene a esperar si la mayoría se rezaga y, al llegar, todos beben, pastan o descansan un rato antes de partir hacia otro extremo. Si el líder muere o queda malherido, lo reemplaza otro adulto. El líder crece hasta ser un 12 % más grande que los demás adultos y evita llevar a la manada a un extremo donde hay un depredador. De noche, si no hay hambre ni sed, la manada duerme reunida y un adulto queda de guardia por turnos. Si muere el protector, otro adulto sano puede tomar su lugar.

Las crías pasan a juveniles a los 72 segundos de edad y a adultas a los 205 segundos de edad. El modelo conserva su malla low-poly y cambia las proporciones de cráneo, cuernos y cuerpo, además de ajustar la colisión, la salud, la velocidad y el paso. La sed y el hambre guían las rutas del NavMesh hacia la laguna y los pastos. Cada animal registra los sitios donde comió o bebió para usarlos al reencontrarse con la manada.

En las dos manadas, las hembras adultas sanas pueden poner huevos si hay pareja, alimento, agua, espacio y calma. Los huevos tardan 36 segundos en nacer con el ajuste inicial. El componente **Triceratops Herd** permite desactivar la reproducción o configurar el tiempo de incubación. Cada manada tiene dos lugares reservados para nacimientos y puede reutilizar los de individuos silvestres muertos. Todos los tiempos se miden con la partida activa. Los Triceratops silvestres viven unas dos horas de partida, con variación entre individuos, y al final de su vida pierden salud de a poco. El hambre y la sed extremas los debilitan hasta un 30 % de vida sin matarlos.

Las conductas del grupo son decisiones de diseño para el juego. Si jugás como Triceratops crecés con estas mismas etapas y siluetas, pero al ritmo de tu sed y tu hambre.

## Escala y animaciones

| Criatura | Escala en juego | Ataque |
| --- | --- | --- |
| Triceratops | 9 m de largo | Corneada |
| Velociraptor | 2,65 m de largo | Salto y zarpazo |
| Argentinosaurus | 32 m de largo | Barrido de cola |
| Quetzalcoatlus | 12 m de envergadura | Picotazo y ataque en picado |
| Tyrannosaurus rex | 12,3 m de largo | Mordida |

Las escalas son aproximaciones para el juego. El Velociraptor se amplió para mejorar su lectura en cámara. [Triceratops](https://www.nhm.ac.uk/discover/dino-directory/triceratops.html) · [Velociraptor](https://www.nhm.ac.uk/discover/dino-directory/velociraptor.html) · [Argentinosaurus](https://www.nhm.ac.uk/discover/dino-directory/argentinosaurus.html) · [Quetzalcoatlus](https://doi.org/10.1080/02724634.2020.1780247) · [Tyrannosaurus](https://www.amnh.org/dinosaurs/tyrannosaurus-rex)

- La escena usa las mallas de los cinco FBX originales con esqueletos, pesos, albedos de 256 × 256 y clips propios de reposo, caminata, carrera y ataque. Quetzalcoatlus tiene clips adicionales de vuelo y picado. [Generador de rigs y clips para Blender](Tools/rig_creatures.py).
- Las patas se pesan midiendo cada modelo: todo lo que está por debajo de la panza pertenece por completo a la pata y recién se mezcla con el cuerpo desde la entrepierna hasta la cadera. La mandíbula se separa del cráneo por distancia sobre la superficie entre el mentón y el hocico, así los dientes y el labio superior no se van con la mandíbula al morder. En Triceratops y Argentinosaurus los huesos bajan por el centro de la pata.
- Cada especie tiene además clips de agachado, avance al acecho y muerte. Al morir se le doblan las patas, cae sobre un costado y el cuello, la cola y las patas se asientan en el suelo; el Quetzalcoatlus pliega las alas.
- La boca queda cerrada en reposo. Se abre al jadear en carrera, al morder, al vocalizar y en bostezos ocasionales. Al correr, la cabeza cabecea con cada paso y el cuello se estira hacia adelante.
- Sobre los clips, cada criatura curva el cuello, el pecho y la cola hacia el lado al que gira, con la cola un poco retrasada, se inclina en las curvas (más al correr y al volar) y mueve la cabeza de un lado a otro cuando está quieta. Si gira sin avanzar da pasos en el lugar en vez de rotar rígida.
- La cola no tiene colisionador propio: sondea lo que hay detrás del cuerpo y se curva hacia el otro lado para no atravesar troncos ni a otros animales.
- Agachada, una criatura avanza al 60 % de su velocidad y no corre. El T. rex se agacha mientras acecha y al comer, los Velociraptor al vigilar a un T. rex y las crías de Triceratops al refugiarse. Un depredador agachado tiene que acercarse un tercio más para que una manada de Triceratops lo detecte.
- La dirección de la fauna silvestre se suaviza y los giros bajan de velocidad cuando la intención de movimiento es débil, para que no tiemble ni dé media vuelta de golpe.
- La isla reutiliza el relieve de **Pine Forest at Sunset** para su base, las laderas y vegetación de **Landscape Forest Mountains**, las montañas de **Mountain Range 01** y **Peak Mountain Hiking Trail**. No usa el volcán ni las piezas de basalto oscuro de **Primeval Frontier**.
- Cinco cerros de 95 a 140 m ocupan la franja cercana a la costa. El bosque tiene más de 50.000 árboles, miles de arbustos, rocas, troncos y 44 puntos para árboles gigantes.
- La cordillera pasó de **524.288** a **13.778 triángulos**, y el pico de **969.522** a **21.632**. Los bordes de ambos relieves se integraron al valle y las texturas principales se ajustaron a **1.024 px**. [Herramienta de importación de paisajes](Tools/import_landscape.py).
- La cámara alcanza **1.250 m**, la niebla empieza entre 165 y 250 m y cierra entre 1.010 y 1.490 m según la región, y se aclara al ganar altura en vuelo, para que se pueda leer la isla desde el aire.
- El bosque usa como base los árboles, arbustos y pastos de **A Forest 3 with a Road at Night** en la Bahía del Sur, la Laguna Oriental y la Llanura Central. El Bosque Occidental usa las ponderosas, pastos y troncos de **Pine Forest at Sunset**. Los Cabos del Norte, los Altos del Noreste y las laderas por encima de 62 m usan los abetos y pinos de **The Landscape Is a Forest in the Mountains**. En las tres zonas se mezclan los abetos, píceas, abedules, arbustos y rocas de **Low Poly Forest Tree Pack**. Del pack de StarkCrafts quedan solo flores, piñas y tallos sueltos.
- Los yuyos cubren toda la isla. El componente **Meadow Grass** arma el pastizal alrededor de la cámara con las matas de los tres bosques y del Low Poly Forest Tree Pack, hasta 200 m y con unas 460 matas cada 40 m, más densas en los manchones de césped. **Tufts Per Cell** y **Range** regulan la densidad y el alcance.
- Las escenas originales se usan como biblioteca de modelos. Cada planta se exporta con el pivote en su base y el generador la replanta sobre el punto más bajo del terreno bajo su copa, para que ninguna quede flotando. Los árboles gigantes mantienen raíces ensanchadas, colisión, sombras y flexión con el viento.
- El suelo mezcla la tierra con musgo de **A Forest 3** con manchones del césped de **Pine Forest at Sunset**. Las rocas de las zonas altas usan la piedra con musgo del paisaje de montaña.
- Cada ponderosa conserva el tronco completo y las ramas principales, con unos **2.100 triángulos** de cerca y **550** a distancia. Los árboles de los otros dos packs usan entre **36 y 348**. La vegetación se dibuja con GPU instancing. [Importación del pack PSX](Tools/import_forest.py) · [Extracción de los tres bosques](Tools/import_woodland.py).
- Los panoramas **PSX Daysky HDRI** y **PSX Nightsky HDRI** se mezclan durante el ciclo. La escena usa sombras direccionales y ajusta la iluminación de la vegetación y los dinosaurios según la hora.
- El sol recorre el cielo durante el día: sale por el este a las 6, sube hasta 60° pasando por el norte al mediodía y se pone por el oeste a las 18. Las sombras giran y se alargan con él, y al amanecer y al atardecer la luz, la niebla y el horizonte se tiñen de naranja. De noche la luz viene de la luna.
- El sol tiene disco, corona y un tono más cálido cerca del horizonte. Cuando entra en cuadro sin nubes ni árboles delante, la cámara muestra rayos de luz, un destello en estrella, un halo y reflejos de lente. Los troncos y las copas cortan los rayos. **Vista PSX → Psx View → Sun Flare** regula la intensidad.

## Rendimiento

Con el mismo recorrido de prueba en el editor, el cuadro pasó de 45 ms a unos 21 ms (de 22 a 47 fps). Los cambios:

- **Sin pasada de profundidad previa.** Las sombras ya no usan cascadas en espacio de pantalla, así que Unity dejó de dibujar toda la escena dos veces. La sombra direccional usa un solo mapa de 4.096 px hasta 90 m.
- **Bosque.** Cada celda se prueba contra el cuadro de la cámara, no se dibuja nada más allá de donde cierra la niebla (de noche y con lluvia eso es casi todo el bosque) y en la franja ya velada se dibuja el 65 % y después el 40 % de los árboles.
- **Troncos.** Los más de 50.000 colisionadores de tronco ya no son objetos de la escena: **Forest Canopy** los crea al cargar, agrupados por celda. La escena pasó de 91 MB y 54.000 objetos a 19 MB y 3.000.
- **Fauna.** La fauna que nadie ve (fuera de cuadro o más allá de la niebla, y a más de 130 m) se simula uno de cada tres cuadros, anima su esqueleto 4 veces por segundo y no se dibuja. En red el anfitrión simula todo a ritmo completo. La búsqueda de vecinos usa posiciones guardadas en vez de consultar cada transformación.
- **Imagen.** El destello del sol y la cortina de lluvia solo se calculan cuando hacen falta.

## Verificar y compilar para Windows

Con el editor cerrado, ejecutá `powershell -File Tools/check_delivery.ps1 -BuildWindows` desde la carpeta del proyecto. Unity Test Framework ejecuta las pruebas de Edit Mode y escribe el resultado en `Logs/utf-entrega.xml`. Si pasan, Unity compila `Build/Windows-Entrega/JurassicPix.exe`. Sin `-BuildWindows` se ejecutan las pruebas.

## Compilar para Android

Con el módulo **Android Build Support** instalado, el menú **Jurassic Pix > Compilar APK de Android** genera `Build/Android/JurassicPix.apk` (ARM64, IL2CPP, SDK mínimo 26, paquete `com.jurassicpix.juego`). Desde consola, con el editor cerrado:

```
"...\Unity.exe" -batchmode -projectPath . -executeMethod JurassicPix.Editor.AndroidBuild.Build -quit -logFile Logs\android_build.log
```

En esta máquina el módulo vive en `D:\UnityAndroid\AndroidPlayer`, enlazado desde `Editor\Data\PlaybackEngines\AndroidPlayer` por falta de espacio en C:.
