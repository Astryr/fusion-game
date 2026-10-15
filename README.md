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
  - **Tronco gigante**: el tronco se achica, se pueden empujar, caen
    cascaras que aturden 3s.
  - **Golpeo de pecho**: spam de Espacio con ritmo. Si el calor llega a
    rojo, perdes.
  - **Memotest**: cada jugador tiene su tablero 6x3 (9 pares). 7s para
    mirar. El primero en completar gana.
  - **Rompe el arbol**: QTE de teclas. Las rojas son trampa. Primero a 30
    golpes buenos o, a los 60s, el que mas tenga.
- Al terminar se ven puestos y puntos de Copa (4-3-2-1). Desde ahi se
  vuelve al lobby para otra ronda.

## Stack

- Unity **6000.3.23f1** (2D, Built-in Render Pipeline).
- Photon Fusion 2 en **Shared Mode**.
- Region fija **`sa`** (Sao Paulo) en `PhotonAppSettings`. Todas las PCs
  tienen que usar la misma region o no se ven las salas. Si `sa` no
  responde, cambiar `FixedRegion` a `us` en todas las maquinas.
- Input Manager clasico y UI de `UnityEngine.UI` armada por codigo
  (`UIStyle` + `UIFactory`).

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
    Player/       Movimiento, ready, nickname, puntaje
    Gameplay/     Director + 6 minijuegos
    UI/           Menu, lobby, chat, HUD, UIStyle/UIFactory
  Sprites/
  Animations/Player/
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
