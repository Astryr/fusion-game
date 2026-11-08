using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.Authentication;
using Unity.Services.RemoteConfig;
using UnityEngine;

public enum ConfigSource
{
    Defaults,
    Cache,
    Remote,
}

public static class LiveConfig
{
    private struct UserAttributes { }
    private struct AppAttributes { }

    public const float StaleSeconds = 45f;

    public static float FetchTimeout = 8f;

    public static MatchConfig Local { get; private set; } = LocalDefaults();
    public static string Motd { get; private set; } = string.Empty;
    public static string MinVersion { get; private set; } = "1.0";
    public static bool LeaderboardEnabled { get; private set; } = true;
    public static ConfigSource Source { get; private set; } = ConfigSource.Defaults;
    public static int PublishedKeys { get; private set; }
    public static string LastError { get; private set; } = string.Empty;
    public static IReadOnlyList<string> Rejected => _rejected;

    public static bool ForceOffline { get; set; }

    public static event Action Changed;

    private static List<string> _rejected = new List<string>();
    private static Task _refresh;
    private static float _lastAttempt = -1000f;

    public static MatchConfig Match
    {
        get
        {
            BananaGameManager manager = BananaGameManager.Instance;
            if (manager != null && manager.Object != null && manager.Object.IsValid)
            {
                MatchConfig shared = manager.Config;
                if (shared.Valid)
                {
                    return shared;
                }
            }

            return Local;
        }
    }

    public static bool IsVersionSupported
    {
        get
        {
            if (!Version.TryParse(MinVersion, out Version minimum) || !Version.TryParse(Application.version, out Version current))
            {
                return true;
            }

            return current >= minimum;
        }
    }

    public static string SourceLabel
    {
        get
        {
            switch (Source)
            {
                case ConfigSource.Remote: return "remota";
                case ConfigSource.Cache: return "en caché";
                default: return "por defecto";
            }
        }
    }

    public static string StatusLine
    {
        get
        {
            string revision = Local.Revision > 0 ? $" r{Local.Revision}" : string.Empty;
            string text = $"v{Application.version} · config {SourceLabel}{revision}";
            if (Source == ConfigSource.Remote)
            {
                text += PublishedKeys > 0 ? $" · {PublishedKeys} claves" : " · sin claves publicadas";
            }

            if (_rejected.Count > 0)
            {
                text += $" · {_rejected.Count} ignoradas";
            }

            if (!string.IsNullOrEmpty(LastError))
            {
                text += " · sin conexión";
            }

            return text;
        }
    }

    public static void LoadCached()
    {
        if (Source != ConfigSource.Defaults)
        {
            return;
        }

        try
        {
            // appConfig no refleja la cache hasta el primer fetch; GetConfig si.
            RuntimeConfig runtime = RemoteConfigService.Instance.GetConfig("settings");
            if (runtime != null && runtime.GetKeys().Length > 0)
            {
                Apply(runtime.config, ConfigSource.Cache);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LiveConfig] No se pudo leer la caché: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static Task RefreshAsync()
    {
        if (_refresh == null || _refresh.IsCompleted)
        {
            _refresh = FetchAsync();
        }

        return _refresh;
    }

    public static Task RefreshIfStaleAsync()
    {
        return Time.realtimeSinceStartup - _lastAttempt < StaleSeconds ? Task.CompletedTask : RefreshAsync();
    }

    public static void ApplyJson(string json, ConfigSource source)
    {
        Apply(JObject.Parse(json), source);
    }

    private static MatchConfig LocalDefaults()
    {
        MatchConfig defaults = MatchConfig.Defaults();
        defaults.Valid = true;
        return defaults;
    }

    private static async Task FetchAsync()
    {
        _lastAttempt = Time.realtimeSinceStartup;
        if (ForceOffline)
        {
            LastError = "Modo sin conexión forzado";
            return;
        }

        try
        {
            await CloudServices.InitializeAsync();
            ProvidePlayerIdWithoutSession();
            RuntimeConfig runtime = await CloudServices.WithTimeout(
                RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes()),
                FetchTimeout);

            bool fresh = RemoteConfigService.Instance.requestStatus == ConfigRequestStatus.Success && runtime.origin == ConfigOrigin.Remote;
            JObject json = runtime.config;
            if (fresh)
            {
                LastError = string.Empty;
                Apply(json, ConfigSource.Remote);
                return;
            }

            LastError = "El servicio de configuración no respondió";
            if (Source == ConfigSource.Defaults && json.Count > 0)
            {
                Apply(json, ConfigSource.Cache);
            }
        }
        catch (Exception ex)
        {
            LastError = CloudServices.Describe(ex);
            Debug.LogWarning($"[LiveConfig] No se pudo actualizar: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Sin sesion, Remote Config avisa "Auth Service not initialized" en cada pedido aunque la respuesta venga completa:
    // la config base no depende del jugador. Se le da un id de instalacion anonimo; con sesion el SDK usa el PlayerId real.
    private static void ProvidePlayerIdWithoutSession()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            return;
        }

        string id = PlayerPrefs.GetString(CloudConfig.InstallIdPref, string.Empty);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(CloudConfig.InstallIdPref, id);
            PlayerPrefs.Save();
        }

        RemoteConfigService.Instance.SetPlayerID(id);
    }

    private static void Apply(JObject json, ConfigSource source)
    {
        var rejected = new List<string>();
        MatchConfig defaults = LocalDefaults();
        MatchConfig next = defaults;

        next.Revision = ReadInt(json, "config_revision", defaults.Revision, 0, 1000000, rejected);
        next.BananaTargetScore = ReadInt(json, "banana_target_score", defaults.BananaTargetScore, 30, 300, rejected);
        next.BananaNormalPoints = ReadInt(json, "banana_points_normal", defaults.BananaNormalPoints, 1, 20, rejected);
        next.BananaGreenPoints = ReadInt(json, "banana_points_green", defaults.BananaGreenPoints, 2, 60, rejected);
        next.BananaExplosivePoints = ReadInt(json, "banana_points_explosive", defaults.BananaExplosivePoints, -40, -1, rejected);
        next.BananaGreenChance = ReadFloat(json, "banana_green_chance", defaults.BananaGreenChance, 0f, 0.3f, rejected);
        next.BananaSpawnIntervalScale = ReadFloat(json, "banana_spawn_interval_scale", defaults.BananaSpawnIntervalScale, 0.5f, 2f, rejected);
        next.BananaTimeLimit = ReadFloat(json, "banana_time_limit_seconds", defaults.BananaTimeLimit, 30f, 300f, rejected);
        next.ParkourAvalancheSpeed = ReadFloat(json, "parkour_avalanche_speed", defaults.ParkourAvalancheSpeed, 1f, 5f, rejected);
        next.LogMatchSeconds = ReadFloat(json, "log_match_seconds", defaults.LogMatchSeconds, 30f, 180f, rejected);
        next.ChestTarget = ReadInt(json, "chest_target", defaults.ChestTarget, 20, 120, rejected);
        next.MemotestPeekSeconds = ReadFloat(json, "memotest_peek_seconds", defaults.MemotestPeekSeconds, 2f, 15f, rejected);
        next.TreeChopTarget = ReadInt(json, "treechop_target", defaults.TreeChopTarget, 10, 80, rejected);
        next.CountdownSeconds = ReadFloat(json, "countdown_seconds", defaults.CountdownSeconds, 3f, 10f, rejected);
        next.CupMultiplier = ReadInt(json, "cup_points_multiplier", defaults.CupMultiplier, 1, 5, rejected);
        next.SoloPracticeEnabled = ReadBool(json, "solo_practice_enabled", defaults.SoloPracticeEnabled, rejected);
        next.ChatEnabled = ReadBool(json, "chat_enabled", defaults.ChatEnabled, rejected);
        next.DisabledMinigames = ReadDisabledMinigames(json, rejected);

        if (next.BananaGreenPoints <= next.BananaNormalPoints)
        {
            rejected.Add("banana_points_green debe ser mayor que banana_points_normal");
            next.BananaNormalPoints = defaults.BananaNormalPoints;
            next.BananaGreenPoints = defaults.BananaGreenPoints;
        }

        string motd = ReadString(json, "motd", string.Empty, rejected);
        string minVersion = ReadString(json, "min_version", "1.0", rejected);
        if (!Version.TryParse(minVersion, out _))
        {
            rejected.Add($"min_version='{minVersion}': no es una versión (ej: 1.0)");
            minVersion = "1.0";
        }

        Local = next;
        Motd = Clean(motd, 90);
        MinVersion = minVersion;
        LeaderboardEnabled = ReadBool(json, "leaderboard_enabled", true, rejected);
        Source = source;
        PublishedKeys = json.Count;
        _rejected = rejected;

        foreach (string issue in rejected)
        {
            Debug.LogWarning($"[LiveConfig] Clave ignorada, se usa el valor por defecto: {issue}");
        }

        Changed?.Invoke();
    }

    private static int ReadInt(JObject json, string key, int fallback, int min, int max, List<string> rejected)
    {
        JToken token = json[key];
        if (token == null)
        {
            return fallback;
        }

        if (!IsNumber(token, out double number) || Math.Abs(number - Math.Round(number)) > 1e-6)
        {
            rejected.Add($"{key}: se esperaba un entero");
            return fallback;
        }

        double rounded = Math.Round(number);
        if (rounded < min || rounded > max)
        {
            rejected.Add($"{key}={rounded}: fuera de rango [{min}, {max}]");
            return fallback;
        }

        return (int)rounded;
    }

    private static float ReadFloat(JObject json, string key, float fallback, float min, float max, List<string> rejected)
    {
        JToken token = json[key];
        if (token == null)
        {
            return fallback;
        }

        if (!IsNumber(token, out double number))
        {
            rejected.Add($"{key}: se esperaba un número");
            return fallback;
        }

        if (number < min || number > max)
        {
            rejected.Add($"{key}={number}: fuera de rango [{min}, {max}]");
            return fallback;
        }

        return (float)number;
    }

    private static bool ReadBool(JObject json, string key, bool fallback, List<string> rejected)
    {
        JToken token = json[key];
        if (token == null)
        {
            return fallback;
        }

        if (token.Type != JTokenType.Boolean)
        {
            rejected.Add($"{key}: se esperaba true o false");
            return fallback;
        }

        return token.Value<bool>();
    }

    private static string ReadString(JObject json, string key, string fallback, List<string> rejected)
    {
        JToken token = json[key];
        if (token == null)
        {
            return fallback;
        }

        if (token.Type != JTokenType.String)
        {
            rejected.Add($"{key}: se esperaba un texto");
            return fallback;
        }

        return token.Value<string>() ?? fallback;
    }

    private static int ReadDisabledMinigames(JObject json, List<string> rejected)
    {
        string raw = ReadString(json, "disabled_minigames", string.Empty, rejected);
        int mask = 0;
        foreach (string part in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Enum.TryParse(part, true, out MiniGameId id) && Enum.IsDefined(typeof(MiniGameId), id))
            {
                mask |= 1 << (int)id;
            }
            else
            {
                rejected.Add($"disabled_minigames: '{part}' no es un minijuego");
            }
        }

        if (mask == MatchConfig.AllMinigamesMask)
        {
            rejected.Add("disabled_minigames: no se pueden desactivar todos los minijuegos");
            return 0;
        }

        return mask;
    }

    private static bool IsNumber(JToken token, out double value)
    {
        value = 0d;
        if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
        {
            return false;
        }

        value = token.Value<double>();
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string Clean(string text, int maxLength)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (!char.IsControl(c))
            {
                builder.Append(c);
            }

            if (builder.Length >= maxLength)
            {
                break;
            }
        }

        return builder.ToString().Trim();
    }
}
