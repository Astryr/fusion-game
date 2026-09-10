# Banana Rush (Unity + Photon Fusion 2)

Proyecto para la materia **Juegos en Red** de la Licenciatura en Desarrollo
de Videojuegos. **Banana Rush** es un minijuego 2D multijugador (2-4
jugadores) hecho en **Unity** con **Photon Fusion 2** como capa de
networking: los jugadores corren de punta a punta de un mapa para atrapar
bananas que caen desde arriba antes de que toquen el piso.

> Estado actual: **jugable de punta a punta**. Conexión en red, personaje,
> movimiento/salto, lluvia de bananas con puntaje y condición de victoria ya
> están implementados sobre la base de Photon Fusion 2 (Shared Mode).

## Gameplay

- Cada jugador elige un **nombre** antes de conectarse (junto con el nombre
  de sala) y controla a **Moniko**, un mono con un tinte de color distinto
  por jugador para diferenciarse.
- Movimiento: **A/D** o flechas izquierda/derecha para caminar, **Espacio**
  (o W / flecha arriba) para un salto chico. No hay colisión entre
  jugadores (se pueden atravesar), pero sí con el piso.
- Bananas normales y explosivas caen desde arriba del mapa a velocidad
  variable: si un jugador la toca antes de que llegue al piso suma
  **+5 puntos** (normal) o resta **-3 puntos** (explosiva, con mínimo 0).
  Si la banana llega al piso sin que nadie la toque, se pierde sin sumar ni
  restar.
- Arriba a la izquierda de la pantalla hay una tabla de puntajes en vivo,
  ordenada de mayor a menor.
- El primer jugador en llegar al puntaje objetivo (30 por defecto, ver
  `BananaGameManager` en el Inspector) gana: la pantalla se pone negra y
  aparece su nombre en el centro, en la pantalla de **todos** los jugadores.

## Stack técnico

- **Unity 6000.3.23f1** (Unity 6 LTS) — 2D, Built-in Render Pipeline.
- **Photon Fusion 2** (SDK 2.1.x) en **Shared Mode**.
- C# puro, sin Input System nuevo (se usa el Input Manager clásico) y sin
  TextMeshPro (UI armada con `UnityEngine.UI` + fuente por defecto) para
  minimizar pasos de configuración adicionales.

## ¿Qué esta hecho ya?

- Conexión a una sala de Photon por nombre (**Shared Mode**): si la sala no
  existe se crea, si ya existe te unís a ella. Así cualquier compañero
  puede sumarse escribiendo el mismo nombre de sala.
- Pantalla inicial con nombre de jugador + nombre de sala (`MainMenuController`).
- Spawn automático del jugador al conectarse (`NetworkRunnerHandler`), con
  el director de partida (`BananaGameManager`) spawneado por el Master
  Client de la sala (autoridad que migra sola si ese jugador se va).
- Personaje Moniko con animaciones de Idle/Caminar/Salto (`Assets/Animations/Player`)
  y movimiento plataformero simple (izquierda/derecha + salto chico) en
  `PlayerController`, con tinte de color distinto por jugador.
- Nivel largo con piso propio (sprites en `Assets/Sprites/Environment`,
  con colisión) y fondo, armados en `Assets/Scenes/Game.unity`.
- Bananas normales y explosivas (`BananaController`) que caen desde arriba,
  otorgan/restan puntos al ser atrapadas y desaparecen sin efecto si tocan
  el piso.
- Tabla de puntajes en vivo y pantalla de victoria con el nombre del
  ganador (`GameHUDController`).
- Herramienta de editor (`Assets/Editor/BananaRushSetupTool.cs`, menú
  **Tools > Banana Rush**) que importa los sprites, genera las animaciones
  y arma los prefabs/escena — pensada para volver a correrse si se cambia
  algún sprite de arte.
- `.gitignore` / `.gitattributes` pensados para Unity + Git (y Git LFS listo
  para cuando sumemos arte/audio pesado).

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
      NetworkInputData.cs      Input que viaja por red (horizontal + salto)
      NetworkRunnerHandler.cs  Conexion, spawn de jugadores/GameManager, input
      BananaRushConfig.cs      Numeros del nivel compartidos por todo el juego
    Player/
      PlayerController.cs      Movimiento plataformero, tinte, nickname, score
    Gameplay/
      BananaController.cs      Caida de las bananas, puntaje, colision con jugador
      BananaGameManager.cs     Spawner de bananas + condicion de victoria
    UI/
      UIFactory.cs             Helpers para armar UI por codigo
      MainMenuController.cs    Pantalla de conexion (nombre + sala)
      GameHUDController.cs     HUD, tabla de puntajes y pantalla de victoria
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
