using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

public enum SubmissionState
{
    None,
    Sending,
    Sent,
    Queued,
    Skipped,
}

public readonly struct SubmissionInfo
{
    public readonly SubmissionState State;
    public readonly int Points;
    public readonly string Detail;

    public SubmissionInfo(SubmissionState state, int points, string detail)
    {
        State = state;
        Points = points;
        Detail = detail ?? string.Empty;
    }
}

public readonly struct RankingEntry
{
    public readonly int Rank;
    public readonly string Name;
    public readonly long Score;
    public readonly bool IsMe;

    public RankingEntry(int rank, string name, long score, bool isMe)
    {
        Rank = rank;
        Name = name;
        Score = score;
        IsMe = isMe;
    }
}

public sealed class RankingResult
{
    public bool Ok;
    public string Error = string.Empty;
    public readonly List<RankingEntry> Entries = new List<RankingEntry>();
    public bool HasMine;
    public RankingEntry Mine;
    public int PendingPoints;
}

public static class RankingService
{
    public static string LeaderboardId = CloudConfig.LeaderboardId;

    public static SubmissionInfo LastSubmission { get; private set; }
    public static string LastError { get; private set; } = string.Empty;

    public static event Action<SubmissionInfo> SubmissionChanged;

    private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
    private static readonly float[] RetryDelays = { 8f, 25f, 70f };

    private static bool _matchCounts;
    private static string _skipReason = string.Empty;
    private static bool _credited;
    private static bool _retrying;
    private static long _lastTotal;
    private static int _lastRank;

    public static int PendingPoints => GetPending(CloudServices.Profile);

    public static void BeginMatch(int playerCount, bool soloPractice)
    {
        _matchCounts = false;
        _credited = false;
        if (!LiveConfig.LeaderboardEnabled)
        {
            _skipReason = "El ranking global está en mantenimiento";
        }
        else if (playerCount < 2 || soloPractice)
        {
            _skipReason = "Práctica en solitario: no suma al ranking global";
        }
        else
        {
            _matchCounts = true;
            _skipReason = string.Empty;
        }

        Publish(SubmissionState.None, 0, string.Empty);
    }

    public static void OnCupPointsAwarded(int points)
    {
        if (_credited || points < CloudConfig.MinMatchPoints || points > CloudConfig.MaxMatchPoints)
        {
            return;
        }

        _credited = true;
        if (!_matchCounts)
        {
            Publish(SubmissionState.Skipped, points, _skipReason);
            return;
        }

        _ = SubmitAsync(CloudServices.Profile, points);
    }

    public static Task<bool> FlushPendingAsync()
    {
        return FlushAsync(CloudServices.Profile);
    }

    public static async Task SyncAsync()
    {
        if (await CloudServices.EnsureOnlineAsync())
        {
            await FlushPendingAsync();
        }
    }

    public static async Task<RankingResult> FetchAsync(int limit = CloudConfig.RankingPageSize)
    {
        var result = new RankingResult();
        try
        {
            if (!await CloudServices.EnsureOnlineAsync())
            {
                result.Error = CloudServices.FailureReason;
                return result;
            }

            await FlushPendingAsync();

            LeaderboardScoresPage page = await CloudServices.WithTimeout(
                LeaderboardsService.Instance.GetScoresAsync(LeaderboardId, new GetScoresOptions { Limit = limit }),
                CloudConfig.RequestTimeoutSeconds);

            string myId = AuthenticationService.Instance.PlayerId;
            foreach (LeaderboardEntry entry in page.Results)
            {
                result.Entries.Add(new RankingEntry(entry.Rank + 1, CleanName(entry.PlayerName), (long)entry.Score, entry.PlayerId == myId));
            }

            try
            {
                LeaderboardEntry mine = await CloudServices.WithTimeout(
                    LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId),
                    CloudConfig.RequestTimeoutSeconds);
                result.HasMine = true;
                result.Mine = new RankingEntry(mine.Rank + 1, CleanName(mine.PlayerName), (long)mine.Score, true);
            }
            catch (LeaderboardsException ex) when (ex.Reason == LeaderboardsExceptionReason.EntryNotFound ||
                                                   ex.Reason == LeaderboardsExceptionReason.NotFound)
            {
            }

            result.Ok = true;
        }
        catch (Exception ex)
        {
            result.Error = Describe(ex);
            Debug.LogWarning($"[RankingService] No se pudo leer el ranking: {ex.GetType().Name}: {ex.Message}");
        }

        result.PendingPoints = PendingPoints;
        return result;
    }

    public static string CleanName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return CloudConfig.DefaultNickname;
        }

        string name = raw;
        int hash = raw.LastIndexOf('#');
        if (hash > 0 && hash < raw.Length - 1 && AllDigits(raw, hash + 1))
        {
            name = raw.Substring(0, hash);
        }

        name = name.Replace('_', ' ').Trim();
        return name.Length > 0 ? name : CloudConfig.DefaultNickname;
    }

    private static async Task<bool> FlushAsync(string profile)
    {
        if (GetPending(profile) <= 0)
        {
            return true;
        }

        await Gate.WaitAsync();
        try
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int pending = GetPending(profile);
                if (pending <= 0)
                {
                    return true;
                }

                if (CloudServices.Profile != profile)
                {
                    LastError = "Cambiaste de jugador: los puntos se envían cuando vuelvas a usar ese nombre";
                    return false;
                }

                if (!await CloudServices.EnsureOnlineAsync())
                {
                    LastError = CloudServices.FailureReason;
                    return false;
                }

                if (CloudServices.SignedProfile != profile)
                {
                    LastError = "Cambiaste de jugador mientras se conectaba";
                    return false;
                }

                try
                {
                    LeaderboardEntry entry = await CloudServices.WithTimeout(
                        LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, pending),
                        CloudConfig.RequestTimeoutSeconds);
                    SetPending(profile, GetPending(profile) - pending);
                    _lastTotal = (long)entry.Score;
                    _lastRank = entry.Rank + 1;
                    LastError = string.Empty;
                    return true;
                }
                catch (LeaderboardsException ex) when (ex.Reason == LeaderboardsExceptionReason.Unauthorized && attempt == 0)
                {
                    CloudServices.InvalidateSession();
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            LastError = Describe(ex);
            Debug.LogWarning($"[RankingService] No se pudo enviar al ranking: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task SubmitAsync(string profile, int points)
    {
        SetPending(profile, GetPending(profile) + points);
        Publish(SubmissionState.Sending, points, "Guardando en el ranking global...");

        if (await FlushAsync(profile))
        {
            Publish(SubmissionState.Sent, points, SentDetail(points));
            return;
        }

        Publish(SubmissionState.Queued, points, $"Sin conexión: {GetPending(profile)} pts pendientes, se envían solos");
        _ = RetryLaterAsync(profile);
    }

    private static async Task RetryLaterAsync(string profile)
    {
        if (_retrying)
        {
            return;
        }

        _retrying = true;
        try
        {
            foreach (float delay in RetryDelays)
            {
                await Task.Delay(TimeSpan.FromSeconds(delay));
                int pending = GetPending(profile);
                if (pending <= 0)
                {
                    return;
                }

                if (await FlushAsync(profile))
                {
                    Publish(SubmissionState.Sent, pending, SentDetail(pending));
                    return;
                }
            }
        }
        finally
        {
            _retrying = false;
        }
    }

    private static string SentDetail(int points)
    {
        return _lastRank > 0
            ? $"+{points} pts enviados · total {_lastTotal} · puesto #{_lastRank}"
            : $"+{points} pts enviados";
    }

    private static string Describe(Exception ex)
    {
        if (ex is LeaderboardsException leaderboards)
        {
            switch (leaderboards.Reason)
            {
                case LeaderboardsExceptionReason.LeaderboardNotFound:
                    return $"No existe el ranking '{LeaderboardId}' en Unity Cloud";
                case LeaderboardsExceptionReason.NoInternetConnection:
                    return "Sin conexión a internet";
                case LeaderboardsExceptionReason.TooManyRequests:
                    return "Demasiadas solicitudes, probá en unos segundos";
                case LeaderboardsExceptionReason.Unauthorized:
                    return "La sesión venció";
                case LeaderboardsExceptionReason.ServiceUnavailable:
                    return "El servicio de ranking no está disponible ahora";
                default:
                    return leaderboards.Message;
            }
        }

        return CloudServices.Describe(ex);
    }

    private static int GetPending(string profile)
    {
        return PlayerPrefs.GetInt(CloudConfig.PendingPrefPrefix + profile, 0);
    }

    private static void SetPending(string profile, int value)
    {
        PlayerPrefs.SetInt(CloudConfig.PendingPrefPrefix + profile, Mathf.Max(0, value));
        PlayerPrefs.Save();
    }

    private static void Publish(SubmissionState state, int points, string detail)
    {
        LastSubmission = new SubmissionInfo(state, points, detail);
        SubmissionChanged?.Invoke(LastSubmission);
    }

    private static bool AllDigits(string text, int start)
    {
        for (int i = start; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
