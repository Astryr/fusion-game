# Banana Rush (Unity + Photon Fusion 2)

Proyecto para la materia **Juegos en Red** de la Licenciatura en Desarrollo
de Videojuegos. **Banana Rush** es un minijuego 2D multijugador (2-4
jugadores) hecho en **Unity** con **Photon Fusion 2** como capa de
networking: los jugadores corren de punta a punta de un mapa para atrapar
bananas que caen desde arriba antes de que toquen el piso.

> Estado actual: **prototipo de primera entrega**. Lobby con lista de salas,
> ready check, 6 minijuegos del GDD y loop completo
> (conexion → lobby → minijuego → resultado → volver al lobby).

## Gameplay

- En el menu cada jugador pone su **nombre**, ve las **salas abiertas**
  (o crea una) y elige el **minijuego**. Maximo **4** jugadores por sala.
- Al entrar se queda en el **lobby**. El juego **no arranca solo**: hace
  falta que haya al menos 2 jugadores y que **todos** apreten
  **ESTOY LISTO**. Recien ahi corre la cuenta regresiva.
- Si estas testeando **sin compañeros**, el host tiene **PROBAR SOLO**:
  con eso alcanza 1 jugador listo para arrancar el minijuego.
- En el lobby se puede cambiar el minijuego con el dropdown o las flechas
  (eso cancela los ready).
- Cada mono tiene **color + nombre** arriba de la cabeza. Controles de
  movimiento: **A/D** o flechas, **Espacio** para saltar (o para el golpeo
  de pecho).
- Minijuegos (GDD, con los sprites que hay):
  - **Lluvia de bananas**: +5 / explosiva **-10**. Gana quien llega a 100.
    A los 2:30 arranca un corte de 30s y gana el mejor puntaje.
  - **Parkour**: carrera larga a la meta, con fondo en parallax. La
    avalancha es un muro blanco de pantalla completa; si te come, quedas fuera.
  - **Tronco gigante**: el tronco se achica, te pueden empujar, y caen
    cascaras que aturden 3s.
  - **Golpeo de pecho**: spam de Espacio con ritmo. Si el calor llega a
    rojo, perdes.
  - **Puzzle 4x4**: memoriza el cuadro. Una ficha mal mueve otra correcta.
  - **Rompe el arbol**: QTE de teclas. Las rojas son trampa. Primero a 30.
- Al terminar se ve el ganador y **Volver al lobby** para otra ronda.

## Stack técnico

- **Unity 6000.3.23f1** (Unity 6 LTS) — 2D, Built-in Render Pipeline.
- **Photon Fusion 2** (SDK 2.1.x) en **Shared Mode**.
- C# puro, sin Input System nuevo (se usa el Input Manager clásico) y sin
  TextMeshPro (UI armada con `UnityEngine.UI` + fuente por defecto) para
  minimizar pasos de configuración adicionales.

## ¿Qué esta hecho ya?

- Conexion Photon Fusion 2 **Shared Mode**: crear sala, listar salas
  abiertas, unirse, tope de 4 jugadores, estados y errores de conexion.
- Lobby in-game con **ready check** (RPC `RPC_SetReady`). No arranca hasta
  que todos esten listos. El host elige el minijuego.
- 6 minijuegos del GDD, director de partida (`BananaGameManager`) con fase
  lobby/countdown/playing/results sincronizada.
- Input de Fusion (`NetworkInputData` + `NetworkButtons`), State/Input
  Authority por jugador, bananas y cascaras con RPC de consume/stun.
- Feedback: nombres sobre la cabeza, toasts, popups +5/-10, barras de calor
  y QTE, pantalla de ganador y volver al lobby.
- Tipografia Bangers para titulos y herramienta `Tools > Banana Rush`.

## Qué falta / posibles mejoras

- Ajustar numeros de diseño a gusto (velocidad de caida de bananas,
  puntaje objetivo, fuerza de salto) — son todos campos expuestos en el
  Inspector de `BananaGameManager` / `PlayerController`.
- Sesiones/lobby más avanzado (lista de salas, matchmaking, salas privadas) —
  buen candidato para cuando se vea JR7 (Sesiones y gestión de partidas).
- Sonido/música (no incluido en los assets de arte provistos hasta ahora).

## Primeros pasos

1. Leé [`SETUP.md`](./SETUP.md) — tiene la guía paso a paso para dejar el
   proyecto andando en tu máquina (instalar Unity, importar Fusion, crear
   el prefab del jugador, probar con más de una persona).
2. Si sos la primera persona en configurar el proyecto (la que tiene el
   AppID de Photon), seguí la sección **"Primera configuración"**.
3. Si un compañero ya hizo el paso anterior y lo subió al repo, con seguir
   la sección **"Para el resto del equipo"** alcanza.
4. Si vas a usar **Cursor** (IDE con IA) en tu máquina para laburar directo
   sobre `main` sin Pull Requests, ver `SETUP.md` → sección **"E. Trabajar
   con Cursor en modo local"**. Las convenciones del equipo (estructura de
   carpetas, estilo de commits, etc.) ya están en
   [`.cursor/rules/working-style.mdc`](./.cursor/rules/working-style.mdc),
   así que Cursor las lee solo al abrir el proyecto.

## Estructura del proyecto

```
Assets/
  Scenes/
    MainMenu.unity     Pantalla inicial: nombre + crear/unirse a una sala
    Game.unity          Escena de juego (camara, fondo, piso, jugadores)
  Scripts/
    Core/
      NetworkInputData.cs      Input de Fusion (eje + botones)
      NetworkRunnerHandler.cs  Lobby de salas, conexion, spawn, input
      BananaRushConfig.cs      Numeros del nivel
      MiniGameId.cs            Minijuegos, fases, nombres
    Player/
      PlayerController.cs      Movimiento, ready, tinte, nickname, score
    Gameplay/
      BananaGameManager.cs     Director: lobby, ready, minijuego, resultado
      *Minigame.cs             Los 6 minijuegos del GDD
      BananaController.cs      Bananas / cascaras
    UI/
      MainMenuController.cs    Nombre, crear/listar/unirse, elegir minijuego
      GameHUDController.cs     Lobby ready, HUD, toasts, ganador
  Sprites/              Arte importado (Player, Props, Environment)
  Animations/Player/     Clips + Animator Controller de Moniko
  Editor/
    BananaRushSetupTool.cs   Herramienta "Tools > Banana Rush" (setup de arte/prefabs)
  Resources/
    Player.prefab, Banana.prefab, BananaExplosiva.prefab,
    BananaGameManager.prefab   Prefabs de red (spawneados por Resources.Load)
  Photon/               SDK de Photon Fusion 2 (importado, ver SETUP.md)
Packages/
  manifest.json      Dependencias del proyecto (paquetes 2D/UI de Unity)
ProjectSettings/     Configuracion del proyecto (Force Text para poder versionar en git)
```

> Nota: las carpetas van directo bajo `Assets/` (sin carpeta envoltorio tipo
> `_Project`), organizadas por dominio (`Scripts/Player`, `Scripts/UI`, etc.),
> siguiendo el mismo orden que usamos en otros proyectos del equipo.

## Sobre el repositorio

El código vive en GitHub: **<https://github.com/Astryr/fusion-game>**
(repositorio privado). Para invitar compañeros de equipo, sumalos como
colaboradores desde **Settings → Collaborators** en esa página.

Para clonarlo, la forma más simple es con
[GitHub Desktop](https://desktop.github.com/) (sin usar la terminal) — ver
el detalle en [`SETUP.md`](./SETUP.md) → sección "Clonar el repositorio".

## Control de versiones

- Se versiona **todo** el proyecto Unity (Assets, Packages, ProjectSettings),
  incluyendo — una vez importado — la carpeta `Assets/Photon` con el SDK y
  la configuración del AppID, para que ningún compañero tenga que repetir
  la importación manual.
- `Library/`, `Temp/`, `Obj/`, `Build/` y `UserSettings/` están ignorados:
  Unity los regenera solo al abrir el proyecto.
- Este proyecto usa **Git LFS** para binarios pesados (imágenes, audio,
  modelos). Antes de sumar assets de ese tipo, corré una vez por máquina:

  ```bash
  git lfs install
  ```
