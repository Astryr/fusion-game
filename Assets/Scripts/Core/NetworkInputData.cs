using Fusion;
using UnityEngine;

/// <summary>
/// Datos de input que se envian por red en cada tick de simulacion.
/// Fusion la completa en <see cref="NetworkRunnerHandler.OnInput"/> (del lado
/// de quien tiene el control) y la entrega a cada <c>NetworkBehaviour</c> a
/// traves de <c>GetInput(out NetworkInputData input)</c> dentro de
/// <c>FixedUpdateNetwork()</c>.
///
/// El gameplay de Banana Rush es 2D simple: mover a los lados y saltar.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    public float Horizontal;
    public bool JumpPressed;
}
