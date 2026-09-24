public interface IMiniGame
{
    MiniGameId Id { get; }
    void Setup(BananaGameManager director);
    void OnMatchStarted();
    void Tick(float deltaTime, bool hasAuthority);
    void OnMatchEnded();
    void Cleanup();
}
