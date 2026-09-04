# Proyecto Juegos en Red (Unity + Photon Fusion 2)

Proyecto base para la materia **Juegos en Red** de la Licenciatura en
Desarrollo de Videojuegos. Es un juego 2D multijugador hecho en **Unity**
con **Photon Fusion 2** como capa de networking.

> Estado actual: **scaffold funcional**. Está resuelta toda la base para que
> el equipo se pueda conectar y ver a los demás jugadores moverse en red.
> Mecánicas de juego, arte, audio y diseño final de UI (ver el GDD) se
> agregan en las próximas entregas sobre esta misma base.

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
- Spawn automático del jugador al conectarse (`NetworkRunnerHandler`).
- Movimiento en red sincronizado con WASD / flechas (`PlayerController`),
  con un cuadrado de color como placeholder visual (cada jugador tiene un
  color distinto, generado por código, para verificar a simple vista que
  la sincronización funciona).
- Menú principal (`MainMenu.unity`) y HUD de partida (`Game.unity`) con UI
  mínima armada 100% por código (fácil de reemplazar cuando tengamos el
  diseño final de UI del GDD).
- `.gitignore` / `.gitattributes` pensados para Unity + Git (y Git LFS listo
  para cuando sumemos arte/audio pesado).

## Qué falta (a propósito, para las próximas entregas)

- Importar el SDK de Photon Fusion 2 y crear el prefab del jugador — **paso
  manual único**, ver [`SETUP.md`](./SETUP.md). No se puede automatizar
  porque el SDK requiere descarga autenticada desde el dashboard de Photon.
- Mecánicas del juego según el GDD (`GDD JUEGO EN REDES.pdf`).
- Arte, animaciones, audio.
- Sesiones/lobby más avanzado (lista de salas, matchmaking, salas privadas) —
  buen candidato para cuando se vea JR7 (Sesiones y gestión de partidas).

## Primeros pasos

1. Leé [`SETUP.md`](./SETUP.md) — tiene la guía paso a paso para dejar el
   proyecto andando en tu máquina (instalar Unity, importar Fusion, crear
   el prefab del jugador, probar con más de una persona).
2. Si sos la primera persona en configurar el proyecto (la que tiene el
   AppID de Photon), seguí la sección **"Primera configuración"**.
3. Si un compañero ya hizo el paso anterior y lo subió al repo, con seguir
   la sección **"Para el resto del equipo"** alcanza.

## Estructura del proyecto

```
Assets/
  Scenes/
    MainMenu.unity     Pantalla inicial: crear/unirse a una sala
    Game.unity          Escena de juego (donde se spawnean los jugadores)
  Scripts/
    Core/
      NetworkInputData.cs      Estructura de input que viaja por red
      NetworkRunnerHandler.cs  Conexion, spawn de jugadores, callbacks de Fusion
    Player/
      PlayerController.cs      Movimiento en red + color placeholder
    UI/
      UIFactory.cs             Helpers para armar UI por codigo
      MainMenuController.cs    Pantalla de conexion
      GameHUDController.cs     HUD dentro de la partida
  Resources/
    Player.prefab      Prefab del jugador (ver SETUP.md)
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
