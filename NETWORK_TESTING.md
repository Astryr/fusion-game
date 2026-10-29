# Pruebas de red

## Instrumentacion

En el Editor y en Development Builds, el juego muestra un overlay de red. **F3** lo oculta o muestra. El overlay toma una muestra de `NetworkRunner.GetPlayerRtt(PlayerRef.None)` cada 0.5 segundos y presenta RTT minimo, promedio y maximo en una ventana movil de hasta 30 segundos, mas una variacion suavizada del RTT.

La variacion mostrada no es jitter de llegada de paquetes medido en el transporte: es una estimacion a partir de diferencias entre muestras de RTT. La perdida se inyecta con Network Conditions; el porcentaje configurado es la referencia conocida. Observar tambien pausas, acciones repetidas/perdidas, resultados inconsistentes y desconexiones durante cada perfil.

## Perfiles

Abrir `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` con el inspector de Network Project Config. Cambiar Network Conditions y reiniciar ambos runners entre perfiles.

| Perfil | Enabled | Delay min/max | Additional jitter | Loss chance min/max |
|---|---:|---:|---:|---:|
| Normal | false | - | - | - |
| Adverso | true | 0.15 s / 0.15 s | 0.05 s | 0.05 / 0.05 |

El asset ya contiene los valores adversos, pero `Enabled` esta desactivado por defecto para no degradar partidas normales.

## Procedimiento

1. Conectar dos clientes a la misma sala de Photon, con la misma region y el mismo build. Mantener el panel Fusion Statistics visible en ambos.
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