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
| Normal | Pendiente | Pendiente de ejecutar con dos clientes | Sin datos de ejecucion |
| Adverso | Pendiente | Pendiente de ejecutar con dos clientes | Sin datos de ejecucion |

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