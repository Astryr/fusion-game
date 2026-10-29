# Pruebas de red

## Instrumentacion

En el Editor y en cualquier build Windows (`UNITY_STANDALONE_WIN`), el juego muestra un overlay de red. **F3** lo oculta o muestra. El overlay toma una muestra de `NetworkRunner.GetPlayerRtt(PlayerRef.None)` cada 0.5 segundos y presenta RTT minimo, promedio y maximo en una ventana movil de hasta 30 segundos, mas una variacion suavizada del RTT y el total local acumulado de RPC de feedback recibidos durante la conexion. La ventana RTT se reinicia al reconectar.

La variacion mostrada no es jitter de llegada de paquetes medido en el transporte: es una estimacion a partir de diferencias entre muestras de RTT. La perdida se inyecta con Network Conditions; el porcentaje configurado es la referencia conocida. Observar tambien pausas, acciones repetidas/perdidas, resultados inconsistentes y desconexiones durante cada perfil.

## Perfiles

Abrir `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` con el inspector de Network Project Config. Cambiar Network Conditions y reiniciar ambos runners entre perfiles.

| Perfil | Enabled | Delay min/max | Additional jitter | Loss chance min/max |
|---|---:|---:|---:|---:|
| Normal | false | - | - | - |
| Adverso | true | 0.15 s / 0.15 s | 0.05 s | 0.05 / 0.05 |

El asset ya contiene los valores adversos, pero `Enabled` esta desactivado por defecto para no degradar partidas normales.

## Procedimiento

1. Conectar dos clientes a la misma sala de Photon, con la misma region y el mismo build. Mantener visible el overlay **RED - DIAGNOSTICO** en ambos.
2. Ejecutar una ronda de **Tronco gigante** con el perfil Normal. Durante 60 segundos, moverse, saltar, esquivar una piedra y observar un impacto que abra un agujero. Anotar RTT minimo/promedio/maximo, variacion RTT, continuidad de movimiento, resultado del impacto y desconexiones.
3. Cambiar al perfil Adverso, reiniciar ambos runners y repetir exactamente las mismas acciones. Anotar los mismos datos y cualquier diferencia de estado entre clientes.
4. Repetir con otro minijuego que use interacciones entre jugadores y registrar si la latencia cambia el resultado, no solo la animacion.

## Registro de ejecucion

| Perfil | RTT min/prom/max y variacion | Observaciones de juego | Problemas reproducidos |
|---|---|---|---|
| Normal | No medida | Prueba no realizada; requiere dos clientes | Sin observaciones de ejecucion |
| Adverso | No medida | Prueba no realizada; requiere dos clientes | Sin observaciones de ejecucion |

Observacion de configuracion: Network Conditions estaba desactivado en el asset al iniciar este registro. No se completaron pruebas en vivo porque este entorno no tiene dos clientes de Fusion conectados; no se atribuyen valores de RTT ni fallos no medidos.

## Lag compensation

No se aplica lag compensation a este juego. No hay disparos hitscan ni ataques del jugador que dependan de acertar a otro jugador en una posicion pasada. Las piedras y los agujeros son peligros ambientales: la autoridad de estado calcula su avance y las eliminaciones en ticks de simulacion. Rebobinar solo la posicion del jugador, sin rebobinar tambien el estado historico de la piedra y del tronco, podria convertir una esquiva actual en un impacto falso. Si se agrega una interaccion de punteria directa, volver a evaluar el requisito con hitboxes historicos y el modo de Fusion utilizado.

## Sincronizacion, autoridad y costo

| Dato | Actualizacion/autoridad | Tratamiento |
|---|---|---|
| Input horizontal y botones | Enviado por Fusion desde el Input Authority para la simulacion | No se reemplaza por RPC por boton/frame. |
| Transform del jugador | `NetworkTransform` del prefab `Player`; actualizado durante movimiento | Necesario para interpolacion, caidas y posicion visible a los otros 3 jugadores. |
| Puntaje, ready, eliminado y stun | Estado del objeto Player; el owner actualiza su puntaje al recoger bananas y al realizar acciones; el manager escribe premios de Parkour/Copa | RPC verifica Input Authority o State Authority del manager; solicitudes de score se limitan a fase, juego y delta permitido. Cupos y eliminaciones remotas requieren al manager. |
| Fase, ganador y resultados | Objeto `BananaGameManager`; State Authority | Solo el manager cierra la partida. Un reclamo remoto debe venir del jugador nombrado, seguir vivo y cumplir el umbral del minijuego. |
| Roca del tronco | `LogHoleMask` cambia al impactar; activo, X, hora de inicio y velocidad cambian al aparecer/desaparecer | Y se reconstruye localmente desde `MatchTime`; no se envia cada tick. |
| Feedback global | Solo lo envia el State Authority del manager | La recoleccion de bananas ya no emite un toast global redundante: el puntaje sincronizado y el popup local bastan. El overlay cuenta los RPC globales restantes. |
| Banana individual | NetworkObject mientras cae; el manager la crea y su State Authority la consume | Es necesaria por movimiento y colision independientes. La recoleccion verifica fase, Input Authority reclamante y distancia <= 1.25 unidades. |

**Optimizacion concreta:** antes, `LogRockY` era una propiedad de red que cambiaba en cada tick mientras caia una roca. Se sustituyo por `LogRockStartTime` y `LogRockSpeed`, escritos una vez por roca; como `MatchTime` ya es el reloj sincronizado, cada cliente reconstruye Y. Se elimina una actualizacion de float por tick durante cada caida. La instrumentacion tambien cuenta RPC de feedback para observar su frecuencia; el toast por banana era un mensaje global redundante con el estado de puntaje.

**Acceso simultaneo a una banana:** el primer reclamo que llega al State Authority de esa banana se valida por fuente, fase y distancia. La autoridad marca `Consumed` antes de enviar el efecto al jugador; las solicitudes posteriores se rechazan, por lo que un objeto compartido no puede dar dos recompensas.

**Interest Management / AoI:** no se activa. La sala admite como maximo 4 jugadores y una unica arena; todos necesitan observar a los demas, las bananas y los peligros, incluso en Parkour para determinar posiciones y ganador. La poblacion y el numero de objetos no justifican particionar la distribucion espacial en este juego.

**Limite de autoridad:** en Fusion Shared Mode cada jugador conserva State Authority de su propio Player. Las comprobaciones impiden que otros peers escriban ese objeto y limitan acciones recibidas; no pretenden demostrar que el jugador propietario no manipula su propia ejecucion. No se agrega anticheat externo.

## Optimizaciones con evidencia

Los fragmentos siguientes corresponden al codigo actual. Se incluyen para explicar las decisiones y facilitar la revision; no son resultados de pruebas ejecutadas.

### 1. Altura de roca reconstruida

`LogRockY` ya no es un campo de red que cambia todos los ticks. Al aparecer una roca, la autoridad publica su X, hora de inicio y velocidad. Cada peer reconstruye Y con el reloj de partida sincronizado:

```csharp
private float RockYAt(float matchTime)
{
	float elapsed = Mathf.Max(0f, matchTime - _director.LogRockStartTime);
	return RockSpawnY - _director.LogRockSpeed * elapsed;
}
```

La posicion se inicializa por evento, no mediante una escritura de float por tick durante toda la caida. `LogHoleMask` tambien se modifica solamente al impacto. El objetivo de la prueba es comprobar que todos ven una trayectoria equivalente y que el agujero aparece una sola vez.

### 2. Feedback global sin spam por banana

El puntaje se actualiza en el estado del jugador y el popup de puntos es local. Por eso recoger una banana no necesita ademas mandar un toast global por cada colision. Los toasts globales restantes se limitan a eventos de ronda, resultados y avisos puntuales:

```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void RPC_ShowFeedback(NetworkString<_64> message, int tone)
{
	FeedbackRpcCount++;
	// Mostrar el toast local en cada peer.
}
```

`FeedbackRpcCount` es un contador de ejecuciones de este RPC en la instancia local. No cuenta todos los RPC, objetos, bytes ni paquetes. La prueba compara el valor antes y despues de recoger varias bananas: recogerlas no deberia incrementarlo; un evento global, como comenzar la ronda, si.

### 3. Reclamo unico del recurso compartido

La banana valida fase, jugador y distancia en su State Authority. Al aceptar el primer reclamo, marca `Consumed` antes de enviar el efecto y despawnear el objeto:

```csharp
PlayerRef bananaAuthority = Object.StateAuthority;
int points = NetPointValue != 0 ? NetPointValue : _pointValue;
Consumed = true;
player.ReceiveBananaHit(points, bananaAuthority);
Runner.Despawn(Object);
```

El mismo camino aplica stun para las cascaras. Solicitudes repetidas encuentran `Consumed == true` y no vuelven a aplicar el efecto. El resultado esperado ante dos reclamos concurrentes es una sola banana consumida y una sola recompensa.

### 4. Validacion de score por fuente y minijuego

La RPC no acepta cualquier entero enviado por cualquier peer. En fase de juego, el origen debe ser el Input Authority correspondiente, salvo el score de Parkour que escribe el manager State Authority:

```csharp
else if (info.Source != Object.InputAuthority || !IsValidPlayerScoreChange(manager, score))
{
	return;
}
```

Los deltas del jugador se limitan a un punto para Memotest y Rompe el Arbol; el score de Parkour se limita al intervalo de puestos. La prueba intenta un score negativo, un salto de muchos puntos, un score en otra fase y una fuente que no sea el owner. Ninguno debe cambiar `Score`.

### 5. Victoria verificada por el manager

El manager comprueba identidad del emisor, que el jugador exista y siga vivo, y el umbral correspondiente al minijuego:

```csharp
if (Phase != MatchPhase.Playing || info.Source != player)
	return;

if (winner == null || winner.IsEliminated || !IsValidWinnerClaim(winner))
	return;
```

Umbrales comprobados: 100 puntos en Lluvia de bananas, 9 pares en Memotest, 30 golpes en Rompe el Arbol y 50 puntos en Golpeo de pecho. En Parkour y Tronco gigante el resultado se decide desde la autoridad de la partida. La prueba envia reclamos prematuros y de jugadores eliminados: la fase debe seguir en `Playing`.

### 6. Input separado de RPC

El input de movimiento es pequeno y se entrega por el canal de input de Fusion:

```csharp
public struct NetworkInputData : INetworkInput
{
	public float Horizontal;
	public NetworkButtons Buttons;
}
```

El movimiento no genera un RPC manual por tecla o frame. Fusion replica la transformacion del prefab Player con `NetworkTransform`; los estados de score, ready y eliminacion se mantienen en el objeto Player. La prueba busca movimiento remoto continuo sin una RPC por cada pulsacion.

### 7. Decoracion local, no NetworkObjects

Los cocos de la avalancha de Parkour son decoracion local y se animan desde `AvalancheX`; no se crean 144 NetworkObjects ni se transmiten giros por tick. La autoridad sincroniza la posicion de la avalancha que se usa para la eliminacion. La prueba debe confirmar que los cocos se ven animados en ambos clientes, que las eliminaciones siguen coincidiendo con `AvalancheX` y que no aparecen cocos en el inventario de NetworkObjects.

## Casos de prueba propuestos

No se realizaron los casos de esta tabla: requieren una sesion con dos clientes conectados. Los resultados indicados son esperados, no observaciones medidas.

| ID | Caso | Pasos | Resultado esperado |
|---|---|---|---|
| N-01 | Conexion base | Abrir dos builds Windows, crear y buscar la misma sala, unirse y esperar a ambos jugadores. | Ambos ven el mismo lobby; sin errores ni desconexion. |
| N-02 | Input base | Moverse, saltar y cambiar de direccion desde ambos clientes. | Cada cliente controla su player; el otro ve movimiento interpolado, sin RPC manual por tecla. |
| N-03 | RTT normal | Network Conditions desactivado; jugar 60 s con overlay visible. | RTT registra muestras; anotar min/prom/max y variacion; acciones y posiciones convergen. |
| N-04 | RTT adverso | Activar perfil Adverso y reiniciar ambos runners; repetir N-03. | RTT/variacion reflejan la condicion impuesta; el resultado final sigue siendo unico y consistente. |
| N-05 | Perdida de paquetes | Con el perfil Adverso, jugar una ronda completa y observar inputs, pickups y fase final. | Registrar pausas o correcciones; no deben duplicarse recompensas ni declararse dos ganadores. |
| O-01 | Altura reconstruida | En Tronco gigante, seguir una roca desde el spawn hasta el impacto en ambos clientes. | Las rocas se mueven hacia abajo; impacto y agujero coinciden aunque Y no sea un campo de red por tick. |
| O-02 | Feedback | Anotar el contador; recoger 5 bananas y luego provocar un aviso global de ronda. | Recoger bananas no suma RPC de feedback; el aviso global suma una ejecucion local por peer. |
| C-01 | Pickup simultaneo | Hacer que dos jugadores toquen la misma banana en el mismo intervalo de simulacion. | Solo un reclamo valido consume la banana; una sola recompensa o stun. |
| C-02 | Pickup fuera de rango | Enviar una solicitud cuando el player este a mas de 1.25 unidades de la banana. | La autoridad rechaza el reclamo; no cambia score/stun y la banana sigue activa. |
| A-01 | Score adulterado | Probar en una build de test score negativo, incremento mayor al permitido y score en lobby. | El estado de `Score` queda igual; no se declara ganador. |
| A-02 | Fuente incorrecta | Un peer solicita modificar el Player de otro peer. | La autoridad rechaza el RPC; el objeto del otro jugador no cambia. |
| A-03 | Victoria prematura | Reclamar ganador sin alcanzar el umbral o luego de ser eliminado. | `Phase` permanece en `Playing`; no se abre Results ni se premian puntos de Copa. |
| A-04 | Seleccion en lobby | Un jugador conectado solicita cambiar minijuego en Lobby; repetir desde una fuente no conectada/fase Playing. | La solicitud valida se acepta solo en Lobby; fuentes ajenas o fase incorrecta no cambian `SelectedGame`. |
| A-05 | Feedback falsificado | Intentar invocar `RPC_ShowFeedback` desde una fuente que no sea State Authority. | Fusion rechaza la fuente o no se muestra el toast; el contador no aumenta por el intento invalido. |
| A-06 | Desconexion | Desconectar un cliente durante lobby y durante partida. | La lista se actualiza; se conserva el flujo existente de reabrir lobby o terminar si queda un jugador. |
| V-01 | Cocos locales | Comparar la avalancha en los dos clientes durante Parkour. | La decoracion cae localmente; el valor autoritativo de `AvalancheX` determina eliminacion y ganador. |
| V-02 | AoI | Revisar objetos visibles con el maximo actual de 4 jugadores y una arena compartida. | Todos necesitan los mismos players y hazards; no se habilita AoI porque no reduce una carga relevante. |

## Plantilla de resultados

Completar con mediciones de una ejecucion real:

| Caso | Build/region | Perfil de red | RTT min/prom/max | Feedback RPC | Resultado funcional | Incidente |
|---|---|---|---|---:|---|---|
| N-03 normal | No realizada | Normal | No medida | No medido | No registrado | No registrado |
| N-04 adverso | No realizada | Adverso | No medida | No medido | No registrado | No registrado |
| C-01 simultaneo | No realizada | Normal/adverso | No medida | No medido | No registrado | No registrado |

Estas pruebas no se hicieron en esta sesion: no hay resultados multicliente registrados. Completar la tabla con mediciones reales antes de afirmar que se verificaron latencia, perdida o concurrencia.