using Fusion;
using UnityEngine;

/// <summary>
/// Datos de input que se envian por red en cada tick de simulacion.
/// Fusion la completa en <see cref="NetworkRunnerHandler.OnInput"/> (del lado
/// de quien tiene el control) y la entrega a cada <c>NetworkBehaviour</c> a
/// traves de <c>GetInput(out NetworkInputData input)</c> dentro de
/// <c>FixedUpdateNetwork()</c>.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    public Vector2 MoveDirection;
}
