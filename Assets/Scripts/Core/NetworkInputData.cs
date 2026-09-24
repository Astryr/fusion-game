using Fusion;

/// <summary>
/// Input que viaja por red cada tick. Fusion lo llena en
/// <see cref="NetworkRunnerHandler.OnInput"/> y cada
/// <c>NetworkBehaviour</c> lo lee con <c>GetInput</c>.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    public float Horizontal;
    public NetworkButtons Buttons;
}

public enum InputButton
{
    Jump = 0,
    Action = 1,
}
