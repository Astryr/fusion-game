/// <summary>
/// Un minijuego del GDD. Los que se mueven en horizontal (hoy parkour)
/// tienen que usar <see cref="StageBackdrop.BeginScrollingStage"/> para
/// el parallax; los de arena fija dejan el fondo de la escena.
/// </summary>
public interface IMiniGame
{
    MiniGameId Id { get; }
    void Setup(BananaGameManager director);
    void OnMatchStarted();
    void Tick(float deltaTime, bool hasAuthority);
    void OnMatchEnded();
    void Cleanup();
}
