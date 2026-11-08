# Documento de optimización y pruebas de red

Banana Rush · Photon Fusion 2.1.1 · Shared Mode sobre Photon Cloud (región `sa`) · DLL Debug de Fusion · hasta 4 jugadores por sala.

Todas las cifras de este documento son mediciones reales del 8/11/2026. Lo que no se pudo medir figura como pendiente, con el motivo.

## 1. Resumen

| Tema | Resultado |
|---|---|
| Condiciones normales (2 clientes, Photon Cloud) | RTT 77 ms, propagación host a cliente 132 ms, RPC de ida y vuelta 214 ms, entre 0,5 y 2,2 KB/s por cliente, 0 resimulaciones |
| Condiciones desfavorables | Pendiente de ejecución con Clumsy (sección 6) |
| Salida de un jugador | La partida se resuelve sola; detección inmediata si sale ordenado y a los 10 s si desaparece de golpe |
| Salida del host | Photon reasigna el master client y el manager sigue funcionando; mismo criterio de tiempos |
| Optimizaciones | Sección 8: 4 propiedades de red eliminadas, efectos por contador en vez de RPC, roca reconstruida, sin toast por banana |
| Lag Compensation e Interest Management | No se aplican; justificación en la sección 10 |

## 2. Topología y autoridad

Shared Mode: no hay servidor dedicado ni host de verdad. Cada cliente tiene autoridad sobre lo que spawnea y el cliente que Photon marca como master client es el dueño del estado compartido de la partida.

| Dato | State Authority | Cómo se replica |
|---|---|---|
| Jugador: posición, animación, puntaje, ready, eliminado, calor | Dueño del jugador | `NetworkTransform` y propiedades `[Networked]` del prefab `Player` |
| Input horizontal y botones | Dueño del jugador | Canal de input de Fusion (`NetworkInputData`), sin RPC por tecla |
| Fase, cuenta atrás, ganador, resultados, reglas activas | Master client (`BananaGameManager`, flag `SharedModeStateAuthMasterClient`) | `[Networked]` del manager |
| Bananas | Master client | Un `NetworkObject` por banana mientras cae |
| Efectos puntuales (popup, destello, sonido) | Dueño del jugador | Contador `[Networked]` por jugador (sección 8.3) |
| Roca del Tronco | Master client | Hora de inicio y velocidad; la altura se calcula en cada peer |

No se implementa migración de host propia: no hace falta en Shared Mode, Photon reasigna el master client y el manager viaja con él (sección 7).

## 3. Instrumentación

### 3.1 Overlay en el juego

**F3** muestra u oculta el overlay en el Editor y en los builds de Windows. Arranca oculto; también se activa desde AJUSTES en el menú. Muestra:

- RTT promedio, mínimo y máximo con `NetworkRunner.GetPlayerRtt`, una muestra cada 0,5 s en una ventana móvil de 30 s.
- Variación del RTT: diferencia suavizada entre muestras consecutivas. No es jitter de llegada de paquetes medido en el transporte.
- Tráfico entrante y saliente en KB/s y paquetes por segundo, tomado de `FusionStatisticsManager.SimulationSnapshot`. Solo hay datos con la DLL Debug de Fusion, que es la que está activa.
- Cantidad de RPC de feedback global recibidos en la conexión.
- Fuente y revisión de la configuración remota, y si coincide con la del host.

### 3.2 Bots de prueba

Las mediciones automáticas se hicieron con dos procesos del build (host y cliente) contra Photon Cloud, controlados por un harness de bots que no forma parte del proyecto entregado. Cada bot juega partidas completas y registra tiempos con marcas UTC. Los dos procesos corren en la misma PC, así que comparten reloj. Los escenarios usados fueron: `latency`, `happy`, `rain`, `parkour`, `hostleaves`, `kill`, `latejoin`.

- **Propagación host a cliente:** el host cambia el minijuego seleccionado (`[Networked]` del manager) y anota la hora UTC; el cliente anota la hora en que ve el cambio. 6 muestras. Resolución de un frame (16,7 ms) por lado.
- **RPC de ida y vuelta:** el cliente llama a `RPC_RequestSelectGame` (RPC hacia la State Authority) y mide hasta ver el valor reflejado en su copia del estado. 6 muestras.
- **Bajas abruptas:** un script mata el proceso con `Stop-Process -Force`, sin cierre ordenado de la conexión.

## 4. Simulación de red

### 4.1 Network Conditions de Fusion no sirve en Shared Mode

La versión anterior de este documento proponía activar Network Conditions en `NetworkProjectConfig.fusion`. Se midió y no tiene efecto sobre el tráfico de juego en Shared Mode:

| Medición (6 muestras cada una) | Sin simulación | Network Conditions: 400 ms de delay y 10% de pérdida |
|---|---:|---:|
| Propagación host a cliente | 132 ms | 125 ms |
| RPC de ida y vuelta | 214 ms | 217 ms |
| RTT promedio | 77 ms | 77 ms |

El delay de 400 ms habría sumado más de 800 ms de RTT; no apareció. El perfil "adverso" de ese método daba números iguales a los normales, por eso se descartó.

### 4.2 Clumsy (método adoptado)

Se usa [Clumsy 0.3](https://github.com/jagt/clumsy/releases/tag/0.3) (basado en WinDivert), la herramienta que recomienda Photon en su guía de diagnóstico de desconexiones. Instala un driver al arrancar, así que hay que ejecutarlo como **Administrador**.

Filtro, tal como lo documenta Photon, para afectar solo el tráfico de juego contra el Game Server (puertos UDP 5056 o 27002):

```text
udp.DstPort == 5056 or udp.SrcPort == 5056 or udp.DstPort == 27002 or udp.SrcPort == 27002
```

| Perfil | Configuración en Clumsy |
|---|---|
| Normal | Clumsy cerrado |
| Adverso | Lag 150 ms y Drop 5%, con Inbound y Outbound tildados en ambos. Suma unos 300 ms de RTT |
| Recuperación | Drop 100% con Inbound y Outbound durante 5 s, después Stop |
| Corte | Drop 100% con Inbound y Outbound durante 20 s, sin parar |

## 5. Resultados en condiciones normales

Dos clientes en la misma PC contra Photon Cloud, build de desarrollo, 60 FPS, DLL Debug.

| Métrica | Resultado |
|---|---|
| RTT promedio / mínimo / máximo | 77 ms / 63-66 ms / 100-117 ms |
| Variación del RTT | 2,4 a 8,8 ms según la corrida |
| Propagación host a cliente (6 muestras) | 112, 117, 150, 117, 150, 149 ms. Promedio 132 ms |
| RPC de ida y vuelta (6 muestras) | 200, 250, 217, 217, 217, 183 ms. Promedio 214 ms |
| Paquetes | 32 por segundo entrantes, 16 por segundo salientes |
| Resimulaciones | 0 |

Tráfico por escenario (promedio de toda la corrida, incluido el lobby):

| Escenario | Host entra / sale | Cliente entra / sale |
|---|---:|---:|
| Solo lobby, 2 jugadores (`latency`) | 522 / 233 B/s | 692 / 186 B/s |
| Lluvia de bananas hasta los 100 puntos, 53 s de partida (`rain`) | 570 / 885 B/s | 2174 / 217 B/s |
| Tronco, 2 partidas de bananas y salida (`happy`) | 555 / 685 B/s | 1641 / 202 B/s |

El que más recibe es el cliente que no es master en la lluvia de bananas, porque el master spawnea cada banana. Aun así son unos 2 KB/s. Todo se midió con 2 jugadores; con 4 se espera que el tráfico entrante crezca con la cantidad de jugadores remotos, pero no se midió.

Comprobaciones funcionales de las mismas corridas, todas correctas: lobby y listos consistentes, premios de Copa (+4 al ganador, +3 al segundo), ganador y puntaje iguales en ambos clientes, vuelta al lobby con el jugador reseteado, y los efectos de un jugador (popups de puntos y OUT) visibles en el otro cliente.

## 6. Resultados en condiciones desfavorables

**Estado: pendiente de ejecución.** Clumsy exige permisos de administrador y un driver, así que no se puede lanzar de forma automática. Procedimiento para completarla (sección 12) y tabla a llenar:

| Perfil | RTT prom / mín / máx | Propagación host a cliente | RPC de ida y vuelta | Tráfico entra / sale | Resultado funcional |
|---|---|---|---|---|---|
| Normal | 77 / 65 / 100 ms | 132 ms | 214 ms | 692 / 186 B/s (lobby) | Correcto |
| Adverso (150 ms + 5%) | Pendiente | Pendiente | Pendiente | Pendiente | Pendiente |
| Recuperación (5 s sin red) | Pendiente | - | - | - | Pendiente |
| Corte (20 s sin red) | - | - | - | - | Pendiente |

Qué se espera, por el código y la configuración: Fusion reintenta lo que no llegó, así que con 5% de pérdida no debería haber recompensas duplicadas ni dos ganadores, porque todo premio sale de la autoridad (sección 9). Con un corte de 20 s se supera `ConnectionTimeout` (10 s): el cliente debe volver al menú con el mensaje "Te desconectaste: Se perdió la conexión con Photon." y el resto de los jugadores resolver la partida como en la sección 7.

## 7. Salidas y desconexiones

| Evento | Qué pasa | Evidencia |
|---|---|---|
| Un jugador sale en plena partida, de forma ordenada | Se despawnea su `Player`. Si queda un solo jugador y no es modo prueba, gana el que queda (detalle "el resto se desconectó"), se reparte la Copa y se vuelve al lobby con la sala reabierta | Bots, escenario `happy`: la sala queda con 1 jugador y abierta |
| Un jugador desaparece de golpe (proceso muerto) | Igual que el caso anterior, pero Photon lo detecta al vencer `ConnectionTimeout` de 10 s | Bots, escenario `kill`: el host lo notó a los **10,1 s**, mostró resultados en el mismo instante, ganó el que quedaba y la sala se reabrió con 1 jugador |
| El host sale de forma ordenada | Photon reasigna el master client. El manager pasa al nuevo master sin demora visible, la partida en curso se cierra con ganador el que queda y el lobby se reabre. El modo prueba sigue funcionando | Bots, escenario `hostleaves`: nuevo master y State Authority del manager de inmediato; 7 comprobaciones correctas |
| El host desaparece de golpe | Igual, pero el nuevo master aparece al vencer el timeout | Bots, `hostleaves` con el proceso del host muerto: nuevo master a los **10,3 s**, el manager respondió de inmediato, resultados, lobby y modo prueba correctos |
| Alguien intenta entrar con la partida empezada | Se rechaza: la sala se cierra al empezar la cuenta atrás. Mensaje: "La partida ya empezó. Probá de nuevo cuando termine." | Bots, escenario `latejoin` con un tercer cliente |
| El propio cliente pierde la conexión | Fusion cierra el runner (`ConnectionTimeout`, `PhotonCloudTimeout` u `OperationTimeout`), el juego vuelve al menú y muestra "Te desconectaste: Se perdió la conexión con Photon." | Código en `NetworkRunnerHandler.OnShutdown`. Pendiente de prueba real con el perfil Corte |

El timeout de 10 s sale de `Network.ConnectionTimeout` en `NetworkProjectConfig.fusion` y coincide con lo medido. Acortarlo detectaría las bajas antes pero aumentaría las desconexiones falsas con redes inestables; se deja el valor por defecto.

## 8. Optimizaciones

### 8.1 Altura de la roca reconstruida

Antes, `LogRockY` era una propiedad de red que cambiaba en cada tick mientras caía una roca. Ahora la autoridad escribe `LogRockStartTime` y `LogRockSpeed` una vez por roca, y cada peer calcula la altura con `MatchTime`, que ya está sincronizado:

```csharp
private float RockYAt(float matchTime)
{
	float elapsed = Mathf.Max(0f, matchTime - _director.LogRockStartTime);
	return RockSpawnY - _director.LogRockSpeed * elapsed;
}
```

Se elimina una actualización de float por tick durante cada caída. `LogHoleMask` solo cambia al impactar.

### 8.2 Feedback global sin spam por banana

Recoger una banana no manda un toast global: el puntaje ya está en el estado del jugador y el efecto sale del contador de la sección 8.3. Los RPC globales (`RPC_ShowFeedback`) quedan para eventos de ronda y resultados. Evidencia medida en el escenario `rain`: 21 bananas recogidas entre los dos jugadores y el contador de RPC de feedback recibidos terminó en 3.

### 8.3 Efectos puntuales por contador en vez de RPC

Los efectos de un jugador (popup de puntos, OUT, golpe de pecho, progreso, destello de color y sonido) se disparan desde la State Authority del jugador subiendo un contador. Cada cliente lo ve cambiar y reproduce el efecto:

```csharp
[Networked] private int FxSeq { get; set; }
[Networked] private PlayerFx FxKind { get; set; }
[Networked] private int FxValue { get; set; }
```

Frente a un RPC a todos por evento:

- No hay mensaje extra: viaja con el estado del jugador que ya se replica, 12 bytes por jugador.
- Un cliente que entra o se reconecta no repite efectos viejos: inicializa el contador en `Spawned`.
- Si caen varios eventos entre dos snapshots, se reproduce el último. Los golpes de pecho de otros jugadores pueden ser decenas por segundo y así no saturan; el sonido ajeno además se limita a uno cada 0,12 s.

Medido con bots: en `rain`, el host vio 14 popups de banana del otro jugador y el cliente vio 14 propios más 7 del host. En `happy`, el host vio el OUT del cliente en el Tronco.

### 8.4 Propiedades de red muertas eliminadas

Quedaban cuatro propiedades `[Networked]` de una mecánica descartada (aturdimiento con cáscaras) que nadie escribía ni leía: `IsStunned` y `StunUntil` en cada jugador, `IsStunPeel` y `StunSeconds` en cada banana. Se eliminaron junto con sus RPC. Cada jugador replica 8 bytes menos y cada banana otros 8, y una banana pasó a tener solo `Consumed` y `NetPointValue`.

### 8.5 Reclamo único de un recurso compartido

La banana valida fase, jugador y distancia en su State Authority, y marca `Consumed` antes de enviar el efecto. Los reclamos que llegan después encuentran `Consumed == true` y no hacen nada, así que dos jugadores sobre la misma banana no la cobran dos veces:

```csharp
PlayerRef bananaAuthority = Object.StateAuthority;
int points = NetPointValue != 0 ? NetPointValue : _pointValue;
Consumed = true;

player.ReceiveBananaHit(points, bananaAuthority);
Runner.Despawn(Object);
```

### 8.6 Input por el canal de Fusion

El movimiento viaja como `NetworkInputData` por el canal de input; no hay un RPC por tecla ni por frame.

```csharp
public struct NetworkInputData : INetworkInput
{
	public float Horizontal;
	public NetworkButtons Buttons;
}
```

### 8.7 Decoración local en lugar de NetworkObjects

Los cocos de la avalancha del Parkour son decoración local que se anima desde `AvalancheX`. No se crean 144 `NetworkObject` ni se transmiten giros por tick; la autoridad solo sincroniza la posición de la avalancha, que es la que decide las eliminaciones.

### 8.8 Sonido y efectos locales

El sonido se sintetiza por código al arrancar y se reproduce de forma local: no hay audio ni efectos viajando por la red.

## 9. Validación en la autoridad

| Acción | Validación |
|---|---|
| `RPC_SetScore` | Solo en fase de juego, desde el Input Authority del jugador (o el manager en Parkour), con deltas de un punto en Memotest y Rompe el Árbol y rango de puestos en Parkour |
| `RPC_DeclareWinner` | Fase de juego, emisor igual al jugador nombrado, jugador vivo y umbral del minijuego cumplido (100 puntos, 9 pares, 30 golpes, 50 puntos). El texto de victoria lo decide la autoridad: un cliente ya no puede mandar el detalle que ven los demás |
| `RPC_AddCupScore` | Solo el State Authority del manager, entre 1 y 4 veces el multiplicador de Copa |
| `RPC_Eliminate`, `RPC_Teleport`, `RPC_PrepareForMatch`, `RPC_ResetForLobby`, `RPC_ForceUnready` | Solo el State Authority del manager (y el propio jugador para eliminarse), con la fase correcta |
| `RPC_SetReady` | Solo el Input Authority del jugador y solo en el lobby |
| Recoger una banana | Fase, jugador reclamante, distancia de 1,25 unidades o menos y `Consumed` falso |
| `RPC_ShowFeedback` | Solo lo puede enviar la State Authority del manager |
| Selección de minijuego | Solo jugadores conectados, en el lobby y con el minijuego habilitado |
| Chat | Solo el dueño del jugador, texto saneado y limitado a 100 caracteres |

Límite de la autoridad: en Shared Mode cada jugador es State Authority de su propio `Player`. Estas comprobaciones impiden que otros peers lo escriban y acotan lo que se acepta, pero no demuestran que el dueño no manipule su propia ejecución. No se agrega anticheat externo.

## 10. Lag Compensation e Interest Management

**Lag Compensation: no se aplica.** No hay disparos ni ataques entre jugadores que dependan de acertar a otro en una posición pasada. Las piedras y los agujeros son peligros ambientales que calcula la autoridad. Rebobinar solo al jugador, sin rebobinar la piedra y el tronco históricos, convertiría una esquiva actual en un impacto falso. En `NetworkProjectConfig.fusion` está desactivada (`LagCompensation.Enabled: false`). Si algún día se agrega una interacción de puntería directa, hay que reevaluarlo.

**Interest Management: no se aplica.** La sala admite 4 jugadores y una única arena: todos necesitan ver a los demás, las bananas y los peligros, incluso en Parkour para determinar posiciones y ganador. Con ese número de jugadores y objetos, partir la distribución espacial no reduce una carga relevante: el tráfico medido es de unos 2 KB/s.

## 11. Estado de los casos de prueba

| ID | Caso | Estado | Evidencia |
|---|---|---|---|
| N-01 | Conexión base | Ejecutado | Bots: crear sala, unirse y ver el mismo lobby, sin errores |
| N-02 | Input base | Pendiente | Prueba manual con dos instancias; los bots mueven al jugador por teletransporte |
| N-03 | RTT normal | Ejecutado | Sección 5 |
| N-04 | RTT adverso | Pendiente | Requiere Clumsy (sección 6) |
| N-05 | Pérdida de paquetes | Pendiente | Requiere Clumsy (sección 6) |
| O-01 | Altura de roca reconstruida | Pendiente | Prueba visual en el Tronco |
| O-02 | Feedback sin spam | Ejecutado | Sección 8.2: 21 bananas, 3 RPC de feedback |
| C-01 | Pickup simultáneo | Revisión de código | `Consumed` se marca antes de enviar el efecto (sección 8.5) |
| C-02 | Pickup fuera de rango | Revisión de código | `IsValidConsumeClaim` exige distancia de 1,25 o menos |
| A-01 | Score adulterado | Revisión de código | `RPC_SetScore` e `IsValidPlayerScoreChange` |
| A-02 | Fuente incorrecta | Revisión de código | Las comprobaciones de `info.Source` de la sección 9 |
| A-03 | Victoria prematura | Revisión de código | `RPC_DeclareWinner` e `IsValidWinnerClaim` |
| A-04 | Selección en lobby | Revisión de código | `RPC_RequestSelectGame` |
| A-05 | Feedback falsificado | Revisión de código | `RPC_ShowFeedback` es `RpcSources.StateAuthority` |
| A-06 | Desconexión | Ejecutado | Sección 7 |
| E-01 | Efectos visibles en el otro cliente | Ejecutado | Sección 8.3 |
| V-01 | Cocos locales | Pendiente | Prueba visual en Parkour |
| V-02 | AoI | Justificado | Sección 10 |

"Revisión de código" significa que la validación existe y se leyó, pero no se probó contra un cliente modificado.

## 12. Cómo repetir las pruebas

1. Generar el build (Windows, x64) y abrir dos instancias. Con Alt+Enter cada una pasa a ventana para verlas juntas. Crear la sala en una y unirse desde la otra.
2. Mostrar el overlay con **F3** en las dos y anotar RTT, variación y tráfico durante 60 s de partida en cada perfil.
3. Para el perfil Adverso: abrir Clumsy como Administrador, pegar el filtro de la sección 4.2, tildar Lag con 150 ms y Drop con 5%, Inbound y Outbound en ambos, y dar Start. Repetir las mismas acciones y anotar las diferencias, incluyendo si algún premio se duplica o los clientes ven resultados distintos.
4. Para Recuperación y Corte: Drop 100% con Inbound y Outbound, 5 s y 20 s. Anotar si el juego se recupera o vuelve al menú, y con qué mensaje.
5. Para las bajas abruptas: cerrar una instancia desde el Administrador de tareas en plena partida y medir cuánto tarda la otra en mostrar los resultados (deberían ser unos 10 s).
