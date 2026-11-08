# Documento de optimización y pruebas de red

Banana Rush · Photon Fusion 2.1.3 (build 2390) · Shared Mode sobre Photon Cloud (región `sa`) · DLL Debug de Fusion · hasta 4 jugadores por sala.

Todas las cifras de este documento son mediciones reales del 8/11/2026. Lo que no se pudo medir figura como pendiente, con el motivo.

## 1. Resumen

| Tema | Resultado |
|---|---|
| Condiciones normales (2 clientes, Photon Cloud) | RTT 77 ms, propagación host a cliente 142 ms, RPC de ida y vuelta 211 ms, entre 0,5 y 4,3 KB/s por cliente, 0 resimulaciones |
| Condiciones desfavorables | Ejecutadas con Network Conditions de Fusion (sección 6): el perfil del proyecto sube el RTT de 77 a 159 ms y el juego sigue con un solo resultado |
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

Las mediciones automáticas se hicieron con dos procesos del build (host y cliente) contra Photon Cloud, controlados por un harness de bots que no forma parte del proyecto entregado. Cada bot juega partidas completas y registra tiempos con marcas UTC. Los dos procesos corren en la misma PC, así que comparten reloj. Los escenarios usados fueron: `latency`, `happy`, `rain`, `parkour`, `hostleaves`, `kill`, `latejoin` y `freeze`. Todo se volvió a medir después de pasar de Fusion 2.1.1 a 2.1.3.

- **Propagación host a cliente:** el host cambia el minijuego seleccionado (`[Networked]` del manager) y anota la hora UTC; el cliente anota la hora en que ve el cambio. 6 muestras. Resolución de un frame (16,7 ms) por lado.
- **RPC de ida y vuelta:** el cliente llama a `RPC_RequestSelectGame` (RPC hacia la State Authority) y mide hasta ver el valor reflejado en su copia del estado. 6 muestras.
- **Bajas abruptas:** un script mata el proceso con `Stop-Process -Force`, sin cierre ordenado de la conexión.
- **Corte de red:** el cliente bloquea su hilo principal durante 5 s y después durante 20 s. Mientras está bloqueado no envía ni recibe nada, igual que un corte total de su conexión.

## 4. Simulación de red

Se usa Network Conditions de Fusion, en `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`. En la 2.1.1 no tenía efecto: con 400 ms de delay y 10% de pérdida, el RTT quedó en 77 ms, igual que sin simulación. Las notas de la 2.1.2 dicen que volvió a funcionar, y en la 2.1.3 se comprobó.

| Medición (6 muestras, Fusion 2.1.3) | Sin simulación | 400 ms de delay y 10% de pérdida, en los dos clientes |
|---|---:|---:|
| Propagación host a cliente | 142 ms | 351 ms |
| RPC de ida y vuelta | 211 ms | 656 ms |
| RTT promedio | 77 ms | 286 ms |

El asset trae el perfil adverso cargado y **apagado** (`Enabled: false`), para no degradar las partidas normales:

| Perfil | Enabled | Delay min/max | Additional jitter | Loss chance min/max |
|---|---:|---:|---:|---:|
| Normal | false | - | - | - |
| Adverso | true | 0,15 s / 0,15 s | 0,05 s | 0,05 / 0,05 |

Los cortes no se hacen con pérdida configurada: con pérdida al 100% Fusion sigue entregando estado, porque reintenta lo confiable. Un corte real es dejar de enviar y de recibir, y eso es lo que hace el bloqueo del hilo (sección 6).

## 5. Resultados en condiciones normales

Dos clientes en la misma PC contra Photon Cloud, build de desarrollo, 60 FPS, DLL Debug.

| Métrica | Resultado |
|---|---|
| RTT promedio / mínimo / máximo | 77 ms / 60-66 ms / 100-117 ms |
| Variación del RTT | 4,0 a 8,4 ms según la corrida |
| Propagación host a cliente (6 muestras) | 110, 155, 154, 154, 155, 121 ms. Promedio 142 ms |
| RPC de ida y vuelta (6 muestras) | 183, 217, 233, 217, 217, 200 ms. Promedio 211 ms |
| Paquetes | 32 por segundo entrantes, 16 por segundo salientes |
| Resimulaciones | 0 |

Tráfico por escenario (promedio de toda la corrida, incluido el lobby):

| Escenario | Host entra / sale | Cliente entra / sale |
|---|---:|---:|
| Solo lobby, 2 jugadores (`latency`) | 521 / 233 B/s | 693 / 185 B/s |
| Lluvia de bananas, partida completa (`rain`) | 778 / 1768 B/s | 4271 / 339 B/s |
| Tronco, 2 partidas de bananas y salida (`happy`) | 555 / 776 B/s | 1849 / 205 B/s |

El que más recibe es el cliente que no es master en la lluvia de bananas, porque el master spawnea cada banana. El pico medido es 4,3 KB/s. Todo se midió con 2 jugadores; con 4 se espera que el tráfico entrante crezca con la cantidad de jugadores remotos, pero no se midió.

Comprobaciones funcionales de las mismas corridas, todas correctas: lobby y listos consistentes, premios de Copa (+4 al ganador, +3 al segundo), ganador y puntaje iguales en ambos clientes, vuelta al lobby con el jugador reseteado, y los efectos de un jugador (popups de puntos y OUT) visibles en el otro cliente.

## 6. Resultados en condiciones desfavorables

Perfil adverso del asset (150 ms de delay, 50 ms de jitter y 5% de pérdida) activado en los dos clientes a la vez, contra Photon Cloud:

| Perfil | RTT prom / mín / máx | Propagación host a cliente | RPC de ida y vuelta | Tráfico del cliente entra / sale | Resultado funcional |
|---|---|---|---|---|---|
| Normal | 77 / 60 / 117 ms | 142 ms | 211 ms | 693 / 185 B/s (lobby) | Correcto (sección 5) |
| Adverso | 159 / 100 / 200 ms | 213 ms (223, 198, 213, 181, 232, 232) | 369 ms (450, 367, 283, 417, 316, 383) | 710 / 187 B/s (lobby) | Parkour completo: 12 de 12 en cada cliente, mismo ganador y mismo tablero |
| Corte de 5 s | se recupera | el estado vuelve a llegar | - | - | Los dos siguen en la sala y el cliente recibe el cambio que mandó el host |
| Corte de 20 s | - | - | - | - | El cliente pierde la sesión. El host lo detecta a los 10,2 s |

La variación del RTT pasó de 4,0-8,4 ms en normal a 12,8-17,2 ms en adverso. No hubo resimulaciones en ninguno de los dos perfiles.

En la lluvia de bananas, con el perfil adverso, el cliente que no es master no llegó a cobrar bananas: su pantalla va atrás de la posición que valida la autoridad y la banana ya pasó. Los dos clientes vieron el mismo ganador. En una conexión real a `sa` (77 ms) el mismo recorrido cobra bananas y termina la partida, así que queda como límite de juego con latencia agregada y no como una desincronización.

El corte de 5 s no alcanza el `ConnectionTimeout` de 10 s, y la sala sigue. El de 20 s lo supera: la sesión del cliente termina y el host resuelve la partida como en la sección 7. El texto que muestra el menú en ese caso no quedó registrado por el bot.

## 7. Salidas y desconexiones

| Evento | Qué pasa | Evidencia |
|---|---|---|
| Un jugador sale en plena partida, de forma ordenada | Se despawnea su `Player`. Si queda un solo jugador y no es modo prueba, gana el que queda (detalle "el resto se desconectó"), se reparte la Copa y se vuelve al lobby con la sala reabierta | Bots, escenario `happy`: la sala queda con 1 jugador y abierta |
| Un jugador desaparece de golpe (proceso muerto) | Igual que el caso anterior, pero Photon lo detecta al vencer `ConnectionTimeout` de 10 s | Bots, escenario `kill`: el host lo notó a los **10,1 s**, mostró resultados en el mismo instante, ganó el que quedaba y la sala se reabrió con 1 jugador |
| El host sale de forma ordenada | Photon reasigna el master client. El manager pasa al nuevo master sin demora visible, la partida en curso se cierra con ganador el que queda y el lobby se reabre. El modo prueba sigue funcionando | Bots, escenario `hostleaves`: nuevo master y State Authority del manager de inmediato; 7 comprobaciones correctas |
| El host desaparece de golpe | Igual, pero el nuevo master aparece al vencer el timeout | Bots, `hostleaves` con el proceso del host muerto, repetido en Fusion 2.1.3: nuevo master a los **10,2 s**, el manager respondió de inmediato, resultados, lobby y modo prueba correctos |
| Alguien intenta entrar con la partida empezada | Se rechaza: la sala se cierra al empezar la cuenta atrás. Mensaje: "La partida ya empezó. Probá de nuevo cuando termine." | Bots, escenario `latejoin` con un tercer cliente |
| El propio cliente pierde la conexión | Fusion cierra el runner (`ConnectionTimeout`, `PhotonCloudTimeout` u `OperationTimeout`) y el juego vuelve al menú con "Te desconectaste: ...". | Código en `NetworkRunnerHandler.OnShutdown`. El corte de 20 s (sección 6) termina la sesión; el texto del menú no quedó en el log del bot |

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

**Interest Management: no se aplica.** La sala admite 4 jugadores y una única arena: todos necesitan ver a los demás, las bananas y los peligros, incluso en Parkour para determinar posiciones y ganador. Con ese número de jugadores y objetos, partir la distribución espacial no reduce una carga relevante: el tráfico medido no pasa de 4,3 KB/s.

## 11. Estado de los casos de prueba

| ID | Caso | Estado | Evidencia |
|---|---|---|---|
| N-01 | Conexión base | Ejecutado | Bots: crear sala, unirse y ver el mismo lobby, sin errores |
| N-02 | Input base | Pendiente | Prueba manual con dos instancias; los bots mueven al jugador por teletransporte |
| N-03 | RTT normal | Ejecutado | Sección 5 |
| N-04 | RTT adverso | Ejecutado | Sección 6: el RTT pasa de 77 a 159 ms con el perfil del proyecto |
| N-05 | Pérdida de paquetes | Ejecutado | Sección 6: 5% de pérdida junto con el delay; el Parkour termina igual en los dos clientes. Un corte total de 5 s se recupera y uno de 20 s desconecta |
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
2. Mostrar el overlay con **F3** en las dos y anotar RTT, variación y tráfico durante 60 s de partida.
3. Para el perfil adverso: en `NetworkProjectConfig.fusion`, poner `NetworkConditions.Enabled` en true (los valores ya son 150 ms, 50 ms de jitter y 5% de pérdida). Reiniciar las dos instancias, repetir las mismas acciones y anotar si los dos ven el mismo ganador. **Volver a dejar `Enabled` en false antes de generar el build que se entrega.**
4. Para un corte: desconectar la red de una de las PCs (o pausar el proceso) durante 5 s y después durante 20 s. A los 5 s la partida tiene que seguir; a los 20 s esa instancia vuelve al menú y la otra resuelve la partida.
5. Para las bajas abruptas: cerrar una instancia desde el Administrador de tareas en plena partida y medir cuánto tarda la otra en mostrar los resultados (unos 10 s).
