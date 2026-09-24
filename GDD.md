GDD
Banana Rush
Hecho por:
Thiago Lima, Santino Jorge, Guadalupe Muiños y Facundo Batastini
Repositorio de Git: https://github.com/Astryr/fusion-game

1. Premisa General
Cuatro monos adolescentes deberán pelear por convertirse en el campeón de la Copa Banana.

2. Estructura de la Partida y Formato de Juego
De 2 a 4 jugadores. En el menu cada uno pone su nombre, crea o entra a una sala y elige el minijuego. En el lobby se puede cambiar el minijuego. Nadie arranca hasta que todos apreten ESTOY LISTO. Ahi corre una cuenta atras de 5 segundos y empieza el minijuego. Al terminar se ve quien gano y se puede volver al lobby para jugar otro.

Rondas iniciales y eliminación: Hoy cada partida es un minijuego. No hay 10 rondas automaticas ni eliminacion cada 5 rondas. Si un jugador se cae o queda fuera dentro de un minijuego (parkour, tronco, golpeo de pecho), queda eliminado de esa ronda.

Fase final: No hay un "mejor de 5" automatico. Cuando termina el minijuego se muestra la pantalla de victoria y se vuelve al lobby. Desde ahi se puede elegir otro minijuego y seguir sumando puntos de Copa.

Puntuación: Al terminar un minijuego se dan puntos de Copa del 4 al 1 segun el puesto (el primero se lleva 4, despues 3, 2 y 1). En la pantalla de victoria se ve el ganador y sus puntos. En el parkour se ven los puestos (1er, 2do, 3er, 4to) y abajo los caidos.

3. Minijuegos
3.1. Caza de Bananas
Objetivo: Gana quien agarre mas bananas que caen de los arboles.
Mecánica: Caen bananas desde arriba. Las normales suman 5 puntos. Las explosivas restan 10. De vez en cuando cae una banana verde que suma 20 y baja un poco mas lento. Gana el primero que llega a 100 puntos. El orden de los demas queda segun el puntaje de ese momento. A medida que pasa el tiempo caen mas bananas y mas rapido.
Tiempo límite: Si pasan 2 minutos y medio, arranca un corte de 30 segundos. Gana el mono con mas puntos cuando se acaba ese tiempo.

3.2. Carrera de Parkour
Objetivo: Gana quien llegue primero a la meta, o el ultimo que quede en pie si los demas se caen.
Mecánica: Todos aparecen quietos, flotando sobre la plataforma de salida. Corre la cuenta atras de 5 segundos y recien ahi se pueden mover. Hay que saltar de plataforma en plataforma hasta la meta. A los que se quedan atras los come una avalancha blanca. Si un jugador se cae al vacio o lo come la avalancha, queda fuera y su camara sigue al que va mas adelante. El minijuego termina cuando todos los que siguen en carrera cruzan la meta, o cuando queda uno solo. La pantalla de victoria muestra:
1er Lugar: ...
2do lugar: ...
3er lugar: ...
Caidos: ...

3.3. Sobreviviente del Tronco Gigante
Objetivo: Gana el ultimo mono en permanecer sobre el tronco.
Propuestas de diseño (2 opciones):
Opción 1 (Empujones y Cáscaras): Esta es la que esta en el juego. Los monos se paran sobre un tronco. Se pueden empujar entre ellos. El tronco se va achicando. A los 45 segundos empiezan a caer cascaras que aturden 3 segundos. Quien se cae del tronco queda fuera. Gana el ultimo que queda arriba.
Opción 2 (Tronco Movedizo y Destrucción): No esta en el juego. Queda como idea: tronco en movimiento, piedras de monos espectadores y el tronco destruyendose.

3.4. Golpeo de Pechos
Objetivo: Gana quien logre realizar el golpe de pechos mas fuerte.
Mecánica: Hay que apretar Espacio a ritmo para llamar la atencion. Si se aprieta demasiado rapido, el pecho se pone rojo y ese mono queda fuera. Gana el primero que llega a 50 de atencion. Si todos menos uno se lastiman, gana el que queda. Si pasan 45 segundos, gana el que mas atencion tenga.

3.5. Memotest de la Jungla
Objetivo: Gana quien resuelva primero el rompecabezas.
Mecánica: Cada jugador ve su propio tablero (las mismas figuras, distintas posiciones). La grilla es de 6 por 3: 18 piezas y 9 pares. Las figuras son banana, banana bomba, MonikoBoca, Cazador, Cazador azul, banana espada y bananas verde, roja y azul. Tienen 7 segundos para ver el tablero completo. Despues dan vuelta de a 2 cartas. Si son iguales, quedan descubiertas. Quien complete todos los pares primero, gana.

3.6. Destrucción del Árbol
Objetivo: Gana el primer jugador en romper el arbol.
Mecánica: En la pantalla van apareciendo teclas al azar. Hay que apretar la tecla correcta para golpear el tronco. El primero que llega a 30 golpes buenos gana. Una tecla incorrecta o no apretar a tiempo resta 1 punto. A veces aparece una tecla roja de trampa: si se aprieta, tambien resta 1.
