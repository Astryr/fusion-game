public interface IMiniGame
{
    MiniGameId Id { get; }
    void Setup(BananaGameManager director);
    void OnCountdownStarted();
    void OnMatchStarted();
    void Tick(float deltaTime, bool hasAuthority);
    void OnMatchEnded();
    void Cleanup();
}
