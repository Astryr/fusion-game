/// <summary>
/// Minijuegos de Banana Rush (GDD). El host elige uno en el menu/lobby
/// y queda publicado en las Session Properties para que se vea en la lista
/// de salas.
/// </summary>
public enum MiniGameId
{
    BananaRain = 0,
    Parkour = 1,
    LogSurvive = 2,
    ChestBeat = 3,
    MemoryPuzzle = 4,
    TreeChop = 5,
}

public static class MiniGameNames
{
    public const string SessionPropertyKey = "g";
    public const int MaxPlayers = 4;
    public const int MinPlayersToStart = 2;

    public static readonly string[] DisplayNames =
    {
        "Lluvia de bananas",
        "Parkour de la jungla",
        "Tronco gigante",
        "Golpeo de pecho",
        "Puzzle de la jungla",
        "Rompe el arbol",
    };

    public static readonly string[] ShortHints =
    {
        "Atrapa bananas (+5). Evita las explosivas (-10). 100 pts o el mejor al corte.",
        "Corre a la meta. Si te come la avalancha, quedas fuera.",
        "Quedate en el tronco. Empuja a los demas y cuidado con las cascaras.",
        "Spamea ESPACIO con ritmo. Si te pasas, el pecho se pone rojo y perdes.",
        "Memoriza el cuadro 4x4. Una ficha mal mueve otra que estaba bien.",
        "Apreta la tecla que aparece. Las rojas son trampa. Primero a 30 gana.",
    };

    public static string Display(MiniGameId id)
    {
        int index = (int)id;
        return index >= 0 && index < DisplayNames.Length ? DisplayNames[index] : id.ToString();
    }

    public static string Hint(MiniGameId id)
    {
        int index = (int)id;
        return index >= 0 && index < ShortHints.Length ? ShortHints[index] : string.Empty;
    }

    public static MiniGameId Clamp(int raw)
    {
        if (raw < 0 || raw > (int)MiniGameId.TreeChop)
        {
            return MiniGameId.BananaRain;
        }

        return (MiniGameId)raw;
    }
}

public enum MatchPhase
{
    Lobby = 0,
    Countdown = 1,
    Playing = 2,
    Results = 3,
}

public enum PlayerControlMode
{
    Disabled = 0,
    Platformer = 1,
    ChestBeat = 2,
}
