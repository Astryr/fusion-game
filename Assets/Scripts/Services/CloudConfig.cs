public static class CloudConfig
{
    public const string LeaderboardId = "copa_banana";
    public const string DefaultNickname = "Jugador";
    public const string NicknamePref = "br.nickname";
    public const string PendingPrefPrefix = "br.pending.";
    public const string InstallIdPref = "br.install";

    public const float InitTimeoutSeconds = 12f;
    public const float RequestTimeoutSeconds = 10f;

    public const int RankingPageSize = 10;
    public const int MinMatchPoints = 1;

    // 4 puntos del primer puesto por el multiplicador de Copa maximo (cup_points_multiplier = 5).
    public const int MaxMatchPoints = 20;
}
