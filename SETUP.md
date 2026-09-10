# Guía de configuración

Esta guía tiene dos caminos:

- **A. Primera configuración** — lo hace **una sola vez** la persona que
  tiene la cuenta de Photon y el AppID (probablemente vos). Deja todo
  commiteado para que nadie más tenga que repetirlo.
- **B. Para el resto del equipo** — mucho más corto, una vez que el paso A
  ya está subido al repositorio.

Al final hay una sección de **pruebas con varias personas** y otra de
**problemas comunes**.

---

## A. Primera configuración (una sola vez, por el dueño del AppID)

### A.1. Instalar Unity 6000.3.23f1

1. Abrí Unity Hub.
2. Ir a **Installs > Install Editor** y buscar la versión **6000.3.23f1**
   (Unity 6.3 LTS). Si no aparece en la lista, se puede instalar desde el
   [archivo de versiones de Unity](https://unity.com/releases/editor/archive)
   usando el botón "Install in Hub".
   - Si preferís usar otra versión 6.3.x o 2022.3.x LTS que ya tengas
     instalada, también funciona: Fusion 2 soporta 2021.3.45+, 2022.3.x,
     6.0.x y 6.3.x. Vas a tener que actualizar el número de versión en
     `ProjectSettings/ProjectVersion.txt` o simplemente aceptar el cambio de
     versión que te va a proponer Unity Hub al abrir el proyecto.
3. En los módulos de la instalación no hace falta nada especial para probar
   en PC (Windows/Mac Build Support alcanza si más adelante van a exportar
   un build).

### A.2. Clonar el repositorio y abrir el proyecto

El código vive en GitHub: **<https://github.com/Astryr/fusion-game>**
(repositorio privado). La forma más simple de bajarlo, sin usar la terminal,
es con **GitHub Desktop**:

1. Instalá [GitHub Desktop](https://desktop.github.com/) e iniciá sesión con
   la cuenta de GitHub que tiene acceso al repo (la cuenta **Astryr**, o la
   que hayan sumado como colaboradora).
2. **File → Clone repository**, pestaña "GitHub.com", buscá **`fusion-game`**
   en la lista y seleccionalo.
3. En **Local path**, elegí la carpeta de proyectos donde ya tenés tus otros
   juegos (por ejemplo `C:\Users\Urano\Proyectos`) — GitHub Desktop va a
   crear ahí la subcarpeta `fusion-game`.
4. **Clone**.

Alternativa por terminal, para quien prefiera usarla (Windows, macOS o
Linux, con [Git](https://git-scm.com/) instalado):

```bash
git clone https://github.com/Astryr/fusion-game.git
```

**Para sumar compañeros de equipo:** desde
<https://github.com/Astryr/fusion-game> → **Settings → Collaborators →
Add people**, invitalos con su usuario o email de GitHub. Una vez que
acepten la invitación, pueden clonar con cualquiera de los dos métodos de
arriba.

Una vez clonado:

1. Abrí **Unity Hub > Open > Add project from disk** y seleccioná la
   carpeta clonada (la raíz del repo, donde están `Assets/`, `Packages/`
   y `ProjectSettings/`).
2. Abrí el proyecto. La primera vez Unity va a importar todo (puede tardar
   varios minutos) y va a mostrar errores de compilación esperables del
   tipo `The type or namespace name 'Fusion' could not be found` — es
   normal, todavía no importamos el SDK. Seguí al siguiente paso.

### A.3. Descargar e importar el SDK de Photon Fusion 2

1. Entrá a tu cuenta en el
   [Photon Dashboard](https://dashboard.photonengine.com/) (la misma donde
   ya creaste tu AppID de Fusion).
2. Andá a la página de
   [descarga del SDK de Fusion 2](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download)
   y bajá la build **estable más reciente de la serie 2.1.x** (el archivo
   `.unitypackage`).
3. En Unity, andá a **Assets > Import Package > Custom Package...** y
   seleccioná el `.unitypackage` descargado (también podés arrastrarlo
   directo a la ventana Project).
4. Dejá todo tildado y confirmá la importación. Va a crear la carpeta
   `Assets/Photon/...`.
5. Al terminar debería abrirse solo el panel **Fusion Hub**. Si no aparece,
   abrilo manualmente desde **Tools > Fusion > Fusion Hub**.

### A.4. Cargar tu AppID de Fusion

1. En el Fusion Hub, pestaña **Welcome** (o **Fusion 2 Setup**), pegá tu
   **Fusion 2 AppID** (el que ya creaste en el dashboard de Photon).
2. El ícono debería ponerse verde si el AppID es válido.
3. Guardá la escena/proyecto (`Ctrl+S` / `Cmd+S`).

### A.5. Verificar la serialización en texto (Force Text)

Ya viene configurado en `ProjectSettings/EditorSettings.asset`, pero
conviene chequearlo una vez:

1. **Edit > Project Settings > Editor**.
2. En **Asset Serialization > Mode**, confirmá que dice **Force Text**.

Esto es importante para que los `.unity`, `.prefab` y `.asset` se puedan
versionar y mergear bien en git.

### A.6. Crear el prefab del jugador

Fusion necesita que el objeto que se "spawnea" en red sea un **prefab del
proyecto** (no se puede crear a mano desde afuera de Unity sin el SDK
instalado, por eso este es el único paso manual dentro del editor):

1. En la ventana **Hierarchy** de la escena `MainMenu` (o cualquier escena
   abierta), click derecho > **Create Empty**. Renombralo a `Player`.
2. Con `Player` seleccionado, **Add Component**:
   - **Sprite Renderer**.
   - **Network Object** (buscalo escribiendo "Network Object"; es un
     componente de Fusion).
   - **Network Transform** (buscalo escribiendo "Network Transform"; sincroniza
     posición/rotación por red).
   - **Player Controller** (nuestro script, en `Assets/Scripts/Player`).
3. Arrastrá el objeto `Player` desde la Hierarchy hacia la carpeta
   **`Assets/Resources/`** en la ventana Project. Esto lo
   convierte en un prefab (Unity te va a preguntar "Original Prefab" —
   elegí esa opción).
4. Borrá la instancia `Player` que quedó en la Hierarchy (ya está guardada
   como prefab en `Resources`, el código la carga solo con
   `Resources.Load<NetworkObject>("Player")`).
5. Guardá la escena.

> Si el nombre exacto de "Network Object" o "Network Transform" cambió en tu
> versión del SDK, escribí simplemente "Network" en el buscador de Add
> Component y vas a ver las opciones disponibles del namespace `Fusion`.

### A.7. Probar la conexión

1. Abrí la escena `Assets/Scenes/MainMenu.unity`.
2. Dale **Play**.
3. Escribí un nombre de sala (por ejemplo `Sala1`) y tocá **Conectar**.
4. Deberías pasar a la escena `Game` y ver tu cuadrado de color moverse con
   WASD / flechas.

Si funcionó, ¡vas al último paso!

### A.8. Commitear y subir todo

Con la terminal:

```bash
git add -A
git commit -m "Importar Photon Fusion 2 SDK y crear prefab del jugador"
git push
```

O si preferís no usar la terminal, desde **GitHub Desktop**: va a listar
todos los archivos nuevos (los de `Assets/Photon` y el prefab del jugador)
en la pestaña "Changes", escribís un resumen abajo a la izquierda,
**Commit to main**, y arriba a la derecha **Push origin**.

A partir de acá, nadie más del equipo necesita repetir los pasos A.3 a A.6:
ya está todo en el repo.

> **Nota sobre el AppID:** al importar el SDK y configurar el AppID, ese
> valor queda guardado en
> `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` **dentro del
> repositorio**. No es una clave ultra sensible (no da acceso a datos, como
> mucho alguien podría gastar cuota gratuita de tu cuenta de Photon si el
> repo fuera público), pero el repositorio ya está en modo **privado** en
> GitHub, así que no hay problema.

---

## B. Para el resto del equipo (después del paso A)

1. Pedí que te sumen como colaborador/a del repositorio (desde
   <https://github.com/Astryr/fusion-game> → Settings → Collaborators).
   Vas a recibir una invitación por email o notificación de GitHub que hay
   que aceptar.
2. Instalá **Unity 6000.3.23f1** (o la versión que haya quedado configurada
   en `ProjectSettings/ProjectVersion.txt`) desde Unity Hub.
3. Cloná el repositorio siguiendo la sección **A.2** de arriba (con GitHub
   Desktop es lo más simple) — ya va a incluir `Assets/Photon` y el prefab
   del jugador, una vez que se haya completado el paso A.
4. Abrí el proyecto desde Unity Hub.
5. Esperá a que termine de importar. No debería haber errores de
   compilación ni pasos adicionales.
6. Abrí `Assets/Scenes/MainMenu.unity`, dale Play, escribí el
   mismo nombre de sala que use el resto del equipo y conectate.

---

## C. Probar con varias personas

Photon Fusion se conecta a través de la nube de Photon, así que **no hace
falta configurar puertos ni estar en la misma red** para probar con
compañeros en otra casa: alcanza con que todos escriban el mismo nombre de
sala. Tampoco hace falta abrir el juego en el instante exacto: el primero
en conectarse crea la sala y esta queda abierta hasta que se desconecta, así
que el resto se puede unir en cualquier momento mientras siga viva.

### Jugar con alguien que no es del equipo (ej. un amigo probando el juego)

Dos formas, de más simple a más completa para esa persona:

1. **Pasarle un build ya compilado (lo más simple si no va a programar)**:
   generá un ejecutable (ver "Probar en una sola máquina" más abajo, paso 1)
   y compartíselo por Drive/Discord/WeTransfer. Tu amigo lo abre directo, sin
   instalar Unity ni clonar nada.
2. **Que clone el repositorio** (si va a laburar en el proyecto): como es
   privado, primero sumalo como colaborador (GitHub → Settings →
   Collaborators → Add people). Una vez que acepte la invitación, sigue la
   sección **B** de esta guía — el SDK de Photon y el AppID ya están
   commiteados, así que **no** tiene que repetir la configuración inicial
   (sección A): solo clonar, abrir el proyecto en Unity y darle Play.

Cualquiera de las dos formas termina igual: cada uno conecta por su lado
escribiendo **el mismo nombre de sala** (ej. `Sala1`) en la pantalla inicial.

### Posible problema: región de Photon distinta

Este proyecto usa selección automática de "mejor región" (no hay una región
fija configurada en `PhotonAppSettings`). Si los dos están en la misma zona
geográfica, Photon normalmente les asigna la misma región solo y no van a
notar nada. Pero si tu amigo está en otro país (o conecta por una red muy
distinta — VPN, datos móviles, etc.), podría terminar en una región de
Photon diferente a la tuya — y ahí, **aunque escriban el mismo nombre de
sala, no se van a poder ver** (las salas son por región, no globales). Si
sospechan que les está pasando esto, avisen para fijar una región fija en
`Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` (campo
`FixedRegion`) y que los dos apunten siempre al mismo datacenter.

### Cómo confirmar que funcionó

- Los dos pasan de la escena `MainMenu` a `Game` sin quedarse trabados en
  "Conectando...".
- Cada uno ve **dos** cuadrados de colores distintos (el propio y el del
  otro) moviéndose de forma independiente.
- Al mover WASD/flechas de un lado, el movimiento se ve reflejado del otro
  lado (con la latencia normal de internet).

Si algo de esto no pasa, ver la sección **D. Problemas comunes** más abajo.

### Probar en una sola máquina (mientras se suma el resto del equipo)

1. Con el proyecto abierto, **File > Build Settings > Build** para generar
   un ejecutable (asegurate de que ambas escenas estén en la lista, ya
   vienen agregadas por defecto).
2. Corré el ejecutable generado (será tu "jugador 2") y, por separado, le
   das Play en el editor (será tu "jugador 1"). Conectá ambos a la misma
   sala.
3. También podés usar herramientas como
   [ParrelSync](https://github.com/VeriorPies/ParrelSync) para tener dos
   ventanas del Editor abiertas al mismo tiempo sin necesitar un build.

---

## D. Problemas comunes

**"The type or namespace name 'Fusion' could not be found"**
Todavía no importaste el SDK (paso A.3), o falló la importación. Volvé a
importar el `.unitypackage`.

**Unity marca que faltan implementar miembros de una interfaz en
`NetworkRunnerHandler` (`INetworkRunnerCallbacks`)**
Puede pasar si tu versión del SDK agregó/cambió algún método del callback.
Es un arreglo rápido: en tu IDE (Visual Studio / Rider / VS Code), hacé
click derecho sobre el nombre de la clase `NetworkRunnerHandler` > **Quick
Actions and Refactorings > Implement interface** (el texto exacto varía
según el IDE) para autogenerar los métodos que falten.

**Falla la conexión y en la consola aparece "No se encontró 'Player' dentro
de una carpeta Resources"**
Falta crear el prefab del jugador (paso A.6) o no quedó dentro de una
carpeta llamada exactamente `Resources`.

**Al conectar se ve la escena de Menu y la de Juego superpuestas**
En `NetworkRunnerHandler.cs`, dentro de `ConnectAsync`, cambiá
`LoadSceneMode.Single` por `LoadSceneMode.Additive` (o viceversa) según lo
que prefieran — esto depende de cómo tu versión del SDK maneja el cambio
de escena en red.

**Warning "Missing Script" en el prefab `Player`**
Volvé a agregar el componente correspondiente (Network Object, Network
Transform o Player Controller) desde **Add Component**; puede pasar si el
prefab se creó antes de que el SDK termine de compilar.

**Quiero cambiar de Shared Mode a Host Mode**
En `NetworkRunnerHandler.cs`, cambiá `GameMode.Shared` por `GameMode.Host`
(para quien crea la sala) o `GameMode.Client` (para quien se une). Tené en
cuenta que Host Mode requiere que la persona "host" mantenga la app
abierta — si se desconecta, se corta la partida para todos (a menos que
configuren migración de host).

---

## E. Trabajar con Cursor en modo local (commits directos, sin PRs)

Cuando pedís cambios en el **chat de Cursor Cloud** (el que corre en la nube),
esos cambios siempre llegan como Pull Request: es una restricción de esa
plataforma, no algo configurable. Si preferís el flujo directo a `main` sin
PRs — como se trabaja en `seminario-martes` —, hacelo con **Cursor local**,
instalado en tu propia máquina:

1. Instalá [Cursor](https://cursor.com/) (el editor) y abrí sesión con tu
   cuenta.
2. **File → Open Folder** y seleccioná la carpeta raíz de tu clon del repo
   (donde están `Assets/`, `Packages/` y `ProjectSettings/`; ver sección A.2
   si todavía no lo clonaste).
3. Antes de arrancar, asegurate de estar al día:
   ```bash
   git checkout main
   git pull origin main
   ```
4. Abrí el panel de chat/Agent de Cursor (ícono en la barra lateral o
   `Ctrl+L` / `Ctrl+I`) y pedí los cambios como charlando. Al correr local,
   el agente edita los archivos del repo clonado en tu disco y —si le pedís
   que commitee— corre `git add` / `git commit` / `git push` directo sobre la
   rama que tengas activa (`main` por defecto), sin crear ramas ni PRs.
5. (Opcional, para que no te pida confirmar cada comando de terminal) En
   **Cursor Settings → Chat/Agent** buscá la opción de auto-ejecutar
   comandos ("Auto-run"/"Yolo mode") y habilitala si querés que ande más
   fluido, igual de autónomo que el agente cloud.
6. Este repo ya tiene un archivo `.cursor/rules/working-style.mdc` con las
   convenciones del equipo (idioma, estructura de carpetas, estilo de
   commits). Cursor lo lee solo apenas abrís el proyecto, así que no hace
   falta reexplicarle el estilo de trabajo en cada sesión nueva.

> Ojo: si varios compañeros pushean directo a `main` al mismo tiempo, pueden
> pisarse cambios. Hacer `git pull origin main` seguido (sobre todo antes de
> empezar a laburar) ayuda a evitar sorpresas.

## Próximos pasos sugeridos (según el temario de la materia)

- **JR6 (Sincronización e interacción)**: agregar más propiedades
  `[Networked]` (vida, puntaje, estado) e interacciones entre jugadores
  usando RPCs.
- **JR7 (Sesiones y gestión de partidas)**: reemplazar el campo de texto
  de sala por una lista de salas activas usando
  `runner.JoinSessionLobby()` + `OnSessionListUpdated`.
- Reemplazar el sprite/color placeholder por el arte y las mecánicas del
  GDD.
