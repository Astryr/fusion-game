# Carpeta Resources

Esta carpeta especial de Unity permite cargar assets en tiempo de ejecucion con
`Resources.Load<T>("nombre")`, sin necesidad de asignarlos a mano en el Inspector.

`NetworkRunnerHandler` carga el prefab del jugador con:

```csharp
Resources.Load<NetworkObject>("Player");
```

Por eso, una vez que crees el prefab del jugador (ver `SETUP.md`, seccion
"Crear el prefab del jugador"), arrastralo **exactamente a esta carpeta** y
llamalo **`Player`**. Así el código lo encuentra solo, sin tener que
arrastrarlo manualmente en ningún componente del Inspector.

Si mas adelante agregan otros prefabs de red (enemigos, items, proyectiles),
pueden seguir el mismo patron y guardarlos aca tambien.
