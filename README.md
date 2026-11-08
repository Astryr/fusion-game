# Banana Rush (Unity + Photon Fusion 2)

Proyecto para **Juegos en Red**. Banana Rush es un party game 2D de 2 a 4
jugadores hecho en Unity 6 con Photon Fusion 2 (Shared Mode).

Loop: menu → crear o unirse a una sala → lobby con ready → un minijuego →
pantalla de victoria → volver al lobby.

## Gameplay

- En el menu cada jugador pone su **nombre**, crea una sala o se une a una
  de la lista, y elige el **minijuego**. Maximo 4 por sala.
- El **lobby** muestra una tarjeta por jugador (nombre, mono, puntos de
  Copa y si esta listo). El juego no arranca solo: hacen falta al menos 2
  jugadores y que todos apreten **ESTOY LISTO**. Ahi corre una cuenta atras
  de 5s.
- El host puede usar **PROBAR SOLO** para testear sin un segundo jugador.
- En el lobby se puede cambiar el minijuego (eso cancela los ready).
  Mientras corre la cuenta atras o la partida la sala se cierra: nadie
  puede entrar a mitad de un minijuego. Se vuelve a abrir en el lobby.
- Controles: **A/D** o flechas para moverse, **Espacio** para saltar
  (o para el golpeo de pecho).
- **Chat de texto** en el lobby y en la partida: **ENTER** abre el chat,
  se escribe y **ENTER** envia (**ESC** cancela). Mientras se escribe el
  mono no se mueve. Mensajes de hasta 100 caracteres con nombre y color
  del jugador.
- Minijuegos:
  - **Lluvia de bananas**: +5 / explosiva -10 / verde rara +20. Gana quien
    llega a 100. A los 2:30 hay un corte de 30s.
  - **Parkour**: carrera. Arrancan quietos, la cuenta atras los suelta.
    Si te caes, la camara sigue al que va primero.
  - **Tronco gigante**: caen piedras que rompen pedazos del tronco y los
    jugadores se pueden empujar. Gana el ultimo que queda arriba (a los
    95s, el que este mas cerca del centro).
  - **Golpeo de pecho**: spam de Espacio con ritmo. Si el calor llega a
    rojo, perdes.
  - **Memotest**: cada jugador tiene su tablero 6x3 (9 pares). 7s para
    mirar. El primero en completar gana.
  - **Rompe el arbol**: QTE de teclas. Las rojas son trampa. Primero a 30
    golpes buenos o, a los 60s, el que mas tenga.
- Al terminar se ven puestos y puntos de Copa (4-3-2-1). Desde ahi se
  vuelve al lobby para otra ronda.
- El menu tiene un boton **RANKING** con el top 10 global de puntos de
  Copa (ver [Ranking online](#ranking-online-unity-gaming-services)).

## Stack

- Unity **6000.3.23f1** (2D, Built-in Render Pipeline).
- Photon Fusion 2 en **Shared Mode**.
- Region fija **`sa`** (Sao Paulo) en `PhotonAppSettings`. Todas las PCs
  tienen que usar la misma region o no se ven las salas. Si `sa` no
  responde, cambiar `FixedRegion` a `us` en todas las maquinas.
- Input Manager clasico y UI de `UnityEngine.UI` armada por codigo
  (`UIStyle` + `UIFactory`).
- Unity Gaming Services: **Authentication** (login anonimo),
  **Leaderboards** (ranking online) y **Remote Config** (balance y flags).

## Que esta hecho

- Crear sala, listar salas abiertas y unirse. Tope de 4. Property de
  sesion con el minijuego. La sala se cierra al arrancar la cuenta atras.
- Ready check por RPC. No arranca hasta que todos estan listos.
- 6 minijuegos. Director de partida con fases lobby / countdown /
  playing / results.
- Input de Fusion, State/Input Authority por jugador, bananas y cascaras
  en red.
- Chat de texto por RPC (`PlayerController.RPC_Chat`): el remitente sale
  del `InputAuthority` del objeto, el texto se sanea al enviar y al recibir.
- Menu principal con arte (fondo, logo de la Copa Banana, monos y bananas
  animados) y lobby estilo "tarjetas de jugador".
- Nombres sobre la cabeza (unicos: si hay repetidos se agrega un numero),
  toasts, popups, pantalla de victoria.
- Musica de menu/lobby y de minijuegos.
- Desconexion a mitad de partida: si queda un jugador, gana. Si te
  desconectan, el menu te avisa el motivo.
- Ranking online con Unity Gaming Services: login anonimo, suma de puntos
  de Copa por partida y top 10, con cola local si no hay internet.
- Remote Config: 21 claves (balance de los minijuegos, evento de doble
  puntos, flags de chat / modo prueba / ranking / minijuegos, mensaje del
  dia y version minima) con valores por defecto, validacion y cache.

## Ranking online (Unity Gaming Services)

Al terminar una partida de 2 o mas jugadores, los puntos de Copa ganados
se suman al jugador en el ranking global (`RANKING` en el menu muestra el
top 10 y tu puesto). La pantalla de victoria muestra si se guardo.

- **Identidad**: login anonimo con un perfil de Authentication por apodo.
  Dos instancias en la misma PC con apodos distintos son jugadores
  distintos; con el mismo apodo son el mismo. El ultimo apodo se recuerda.
- **Sin internet**: el juego anda igual. Los puntos se anotan primero en
  `PlayerPrefs`, se reintentan solos (a los 8s, 25s y 70s) y se mandan al
  volver a conectar, al abrir el ranking o en la proxima partida.
- **Que no suma**: la practica en solitario (PROBAR SOLO) y las salas de un
  solo jugador.
- **Limite conocido**: los puntos de cada partida los define el director de
  partida (RPC validado, de 1 a 4), pero el ranking confia en el cliente:
  uno modificado podria llamar directo a la API. Validarlo en servidor
  pediria Cloud Code y queda fuera del alcance.

Configuracion (ya hecha para este proyecto):

1. Proyecto vinculado a Unity Cloud en `Edit > Project Settings > Services`.
   El ID queda en `ProjectSettings/ProjectSettings.asset` y el entorno en
   `ProjectSettings/Packages/com.unity.services.core/Settings.json`.
2. En el dashboard, entorno `production`, el leaderboard tiene que existir
   con ID `copa_banana`, Sort Order *Highest to lowest* y Update Strategy
   *Total score*. Si el ID cambia, tocar `Assets/Scripts/Services/CloudConfig.cs`.
3. Antes de una demo, resetear el ranking desde el dashboard
   (Leaderboards > `copa_banana` > Reset) para sacar los puntajes de prueba.

Codigo: `Assets/Scripts/Services/` (`CloudServices`, `RankingService`) y
`Assets/Scripts/UI/RankingPanel.cs`.

## Remote Config (Live Ops)

El balance y varios flags se cambian con **Unity Remote Config** sin sacar
una build nueva. `Assets/LiveOps/BananaRush.rc` es la fuente de verdad (trae
los valores originales del juego) y se despliega al dashboard desde el
Editor (`Services > Deployment`).

- **Cuando se aplica**: el menu pide la config al abrir y el host la vuelve
  a pedir cada vez que la sala vuelve al lobby (si pasaron mas de 45s). Los
  cambios entran en el lobby; durante la cuenta atras y la partida las reglas
  quedan congeladas.
- **Todos juegan igual**: las claves de balance las publica el host por red
  (`BananaGameManager.Config`) y cada cliente usa esa copia aunque su Remote
  Config local sea distinto. El overlay **F3** muestra si la config propia
  es igual a la del host.
- **Si algo falla**: sin internet o con el servicio lento (timeout de 8s)
  se usa la ultima config buena (cache del SDK) y, si no hay, los valores
  originales. Cada clave se valida (tipo y rango): una invalida se ignora,
  se usa su valor por defecto y se informa en el log y en el menu
  ("N ignoradas").
- **Version**: `config_revision` identifica cada publicacion (menu y F3) y
  `min_version` bloquea crear o unirse con una build vieja.

| Clave | Tipo | Defecto | Rango | Que cambia |
|---|---|---|---|---|
| `config_revision` | INT | 1 | 0 a 1000000 | Numero de revision de la config (subirlo en cada cambio) |
| `min_version` | STRING | `1.0` | version | Version minima del juego para jugar |
| `motd` | STRING | texto de bienvenida | 90 caracteres | Mensaje "NOVEDAD" del menu (un espacio lo oculta) |
| `chat_enabled` | BOOL | true | | Chat de texto |
| `solo_practice_enabled` | BOOL | true | | Boton PROBAR SOLO |
| `leaderboard_enabled` | BOOL | true | | Ranking global (false = mantenimiento) |
| `disabled_minigames` | STRING | `none` | | Minijuegos que no se pueden elegir, separados por coma: `BananaRain, Parkour, LogSurvive, ChestBeat, MemoryPuzzle, TreeChop` |
| `cup_points_multiplier` | INT | 1 | 1 a 5 | Multiplica los puntos de Copa (2 = doble puntos) |
| `countdown_seconds` | FLOAT | 5 | 3 a 10 | Cuenta atras antes de cada minijuego |
| `banana_target_score` | INT | 100 | 30 a 300 | Puntos para ganar Lluvia de bananas |
| `banana_points_normal` | INT | 5 | 1 a 20 | Banana comun |
| `banana_points_green` | INT | 20 | 2 a 60 | Banana verde (mayor que la comun) |
| `banana_points_explosive` | INT | -10 | -40 a -1 | Banana explosiva |
| `banana_green_chance` | FLOAT | 0.035 | 0 a 0.3 | Probabilidad de banana verde |
| `banana_spawn_interval_scale` | FLOAT | 1 | 0.5 a 2 | Factor del intervalo entre bananas (menor = mas bananas) |
| `banana_time_limit_seconds` | FLOAT | 150 | 30 a 300 | Tiempo antes del corte de 30s |
| `parkour_avalanche_speed` | FLOAT | 2.55 | 1 a 5 | Velocidad de la avalancha |
| `log_match_seconds` | FLOAT | 95 | 30 a 180 | Duracion del Tronco gigante |
| `chest_target` | INT | 50 | 20 a 120 | Atencion para ganar Golpeo de pecho |
| `memotest_peek_seconds` | FLOAT | 7 | 2 a 15 | Segundos para mirar el tablero |
| `treechop_target` | INT | 30 | 10 a 80 | Golpes buenos para ganar Rompe el arbol |

Cambiar un valor: editar `BananaRush.rc`, subir `config_revision` y
desplegar (`Services > Deployment > Deploy Selected`). Tambien se puede
editar en el dashboard (Remote Config > Config, entorno `production`). Las
claves que falten usan su valor por defecto del codigo.

Codigo: `Assets/Scripts/Services/` (`LiveConfig`, `MatchConfig`).

## Primeros pasos

1. Segui [`SETUP.md`](./SETUP.md).
2. Quien tenga el AppID de Photon hace la primera configuracion una vez
   y la sube. El resto solo clona y abre el proyecto.
3. Las convenciones del equipo estan en
   [`.cursor/rules/working-style.mdc`](./.cursor/rules/working-style.mdc).

## Estructura

```
Assets/
  Scenes/
    MainMenu.unity
    Game.unity
  Scripts/
    Core/         Conexion, input, config, audio, chat (GameChat)
    Services/     Unity Gaming Services: login anonimo, ranking online y Remote Config
    Player/       Movimiento, ready, nickname, puntaje
    Gameplay/     Director + 6 minijuegos
    UI/           Menu, lobby, chat, HUD, UIStyle/UIFactory
  Sprites/
  Animations/Player/
  LiveOps/        Config de Remote Config (BananaRush.rc)
  Resources/      Prefabs de red + audio + cartas del memotest + arte del menu
  Editor/         Tools > Banana Rush
  Photon/         SDK Fusion 2
```

## Repositorio

https://github.com/Astryr/fusion-game (privado). Invitar al equipo desde
Settings → Collaborators. Como clonarlo esta en `SETUP.md`.

## Git

Se versiona Assets, Packages y ProjectSettings (incluye Photon y el
AppID). `Library/`, `Temp/`, `Obj/`, `Build/` y `UserSettings/` estan
ignorados. Para binarios pesados: `git lfs install`.
