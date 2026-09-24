using Fusion;

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
