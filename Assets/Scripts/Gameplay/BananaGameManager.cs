using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Director de la sala en Shared Mode: lobby + ready check + minijuego
/// elegido + resultados. Lo spawnea el Master Client. El estado de fase,
/// minijuego y temporizadores es [Networked]; el ready de cada mono vive
/// en <see cref="PlayerController.IsReady"/>.
/// </summary>
public class BananaGameManager : NetworkBehaviour
{
    public static BananaGameManager Instance { get; private set; }

    [SerializeField] private float _countdownDuration = 5f;

    [Networked] public NetworkBool AllowSoloPractice { get; set; }

    public int MinPlayersToStart => AllowSoloPractice ? MiniGameNames.SoloPracticeMinPlayers : MiniGameNames.MinPlayersToStart;

    [Networked] public MatchPhase Phase { get; set; }
    [Networked] public MiniGameId SelectedGame { get; set; }
    [Networked] public float CountdownRemaining { get; set; }
    [Networked] public float MatchTime { get; set; }
    [Networked] public float OvertimeRemaining { get; set; }
    [Networked] public float AvalancheX { get; set; }
    [Networked] public float LogHalfWidth { get; set; }
    [Networked] public int RoundSeed { get; set; }
    [Networked] public int WinnerScore { get; set; }
    [Networked] public NetworkString<_32> WinnerName { get; set; }
    [Networked] public NetworkString<_16> WinnerDetail { get; set; }

    public bool MatchStarted => Phase == MatchPhase.Playing;
    public bool IsGameOver => Phase == MatchPhase.Results;
    public bool IsInLobby => Phase == MatchPhase.Lobby;

    private readonly List<IMiniGame> _minigames = new List<IMiniGame>();
    private IMiniGame _active;
    private MatchPhase _renderedPhase = (MatchPhase)(-1);
    private MiniGameId _renderedGame;

    public override void Spawned()
    {
        Instance = this;
        BuildMinigames();

        if (Object.HasStateAuthority)
        {
            Phase = MatchPhase.Lobby;
            CountdownRemaining = -1f;
            OvertimeRemaining = -1f;
            WinnerScore = 0;
            SelectedGame = ReadSessionMiniGame();
            ApplySelectedGameToSession();
        }

        _renderedPhase = Phase;
        _renderedGame = SelectedGame;
        _active = FindMinigame(SelectedGame);
        if (Phase == MatchPhase.Countdown)
        {
            _active?.OnCountdownStarted();
        }
        else if (Phase == MatchPhase.Playing)
        {
            _active?.OnMatchStarted();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _active?.Cleanup();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void Render()
    {
        if (_renderedPhase != Phase || _renderedGame != SelectedGame)
        {
            HandlePhaseVisuals(_renderedPhase, Phase);
            _renderedPhase = Phase;
            _renderedGame = SelectedGame;
        }

        if (Phase == MatchPhase.Playing || Phase == MatchPhase.Countdown)
        {
            _active?.Tick(Time.deltaTime, false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (Phase == MatchPhase.Lobby)
        {
            UpdateLobby();
            return;
        }

        if (Phase == MatchPhase.Countdown)
        {
            UpdateCountdown();
            return;
        }

        if (Phase == MatchPhase.Playing)
        {
            MatchTime += Runner.DeltaTime;
            HandleDisconnectDuringMatch();
            if (Phase != MatchPhase.Playing)
            {
                return;
            }

            _active?.Tick(Runner.DeltaTime, true);
        }
    }

    public PlayerController[] GetPlayers()
    {
        var found = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].IsSpawned)
            {
                count++;
            }
        }

        var players = new PlayerController[count];
        int write = 0;
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].IsSpawned)
            {
                players[write++] = found[i];
            }
        }

        return players;
    }

    public int ConnectedPlayerCount()
    {
        return Runner != null && Runner.SessionInfo != null ? Runner.SessionInfo.PlayerCount : 0;
    }

    public int ReadyCount()
    {
        int ready = 0;
        foreach (var player in GetPlayers())
        {
            if (player.IsReady)
            {
                ready++;
            }
        }

        return ready;
    }

    public bool EveryoneReady()
    {
        var players = GetPlayers();
        if (players.Length < MinPlayersToStart)
        {
            return false;
        }

        if (players.Length < ConnectedPlayerCount())
        {
            return false;
        }

        foreach (var player in players)
        {
            if (!player.IsReady)
            {
                return false;
            }
        }

        return true;
    }

    public void SetSoloPractice(bool enabled)
    {
        if (!Object.HasStateAuthority || Phase != MatchPhase.Lobby)
        {
            return;
        }

        AllowSoloPractice = enabled;
        RPC_ShowFeedback(enabled ? "Modo prueba: podes arrancar solo" : "Modo normal: minimo 2 listos", 1);
    }

    public void SetSelectedGame(MiniGameId id)
    {
        if (!Object.HasStateAuthority || Phase != MatchPhase.Lobby)
        {
            return;
        }

        MiniGameId next = MiniGameNames.Clamp((int)id);
        if (SelectedGame == next)
        {
            return;
        }

        SelectedGame = next;
        ApplySelectedGameToSession();
        UnreadyEveryone();
        RPC_ShowFeedback($"Minijuego: {MiniGameNames.Display(SelectedGame)}", 1);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSelectGame(int rawId)
    {
        SetSelectedGame(MiniGameNames.Clamp(rawId));
    }

    public void DeclareWinner(PlayerController winner, string detail)
    {
        if (Phase != MatchPhase.Playing || winner == null)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            FinishMatch(winner, detail);
            return;
        }

        RPC_DeclareWinner(winner.Object.InputAuthority, detail);
    }

    public void DeclareHighestScoreWinner(string detail)
    {
        if (Phase != MatchPhase.Playing)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            FinishMatch(FindHighestScore(), detail);
            return;
        }

        RPC_DeclareHighestScore(detail);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DeclareWinner(PlayerRef player, string detail)
    {
        if (Phase != MatchPhase.Playing)
        {
            return;
        }

        PlayerController winner = null;
        foreach (var candidate in GetPlayers())
        {
            if (candidate.Object.InputAuthority == player)
            {
                winner = candidate;
                break;
            }
        }

        FinishMatch(winner, detail);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DeclareHighestScore(string detail)
    {
        if (Phase == MatchPhase.Playing)
        {
            FinishMatch(FindHighestScore(), detail);
        }
    }

    private PlayerController FindHighestScore()
    {
        PlayerController best = null;
        foreach (var player in GetPlayers())
        {
            if (player.IsEliminated)
            {
                continue;
            }

            if (best == null || player.Score > best.Score)
            {
                best = player;
            }
        }

        return best;
    }

    public void ReturnToLobby()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        Phase = MatchPhase.Lobby;
        CountdownRemaining = -1f;
        OvertimeRemaining = -1f;
        MatchTime = 0f;
        WinnerName = default;
        WinnerDetail = default;
        WinnerScore = 0;
        ResetPlayersForLobby();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestReturnToLobby()
    {
        if (Phase == MatchPhase.Results)
        {
            ReturnToLobby();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ShowFeedback(NetworkString<_64> message, int tone)
    {
        Color color = tone switch
        {
            0 => new Color(0.55f, 1f, 0.60f),
            2 => new Color(1f, 0.45f, 0.40f),
            _ => new Color(1f, 0.85f, 0.35f),
        };
        GameFeedback.Toast(message.ToString(), color);
    }

    private void UpdateLobby()
    {
        if (!EveryoneReady())
        {
            CountdownRemaining = -1f;
            return;
        }

        Phase = MatchPhase.Countdown;
        CountdownRemaining = _countdownDuration;
        RPC_ShowFeedback("Todos listos. Arranca el conteo...", 1);
    }

    private void UpdateCountdown()
    {
        if (!EveryoneReady() || ConnectedPlayerCount() < MinPlayersToStart)
        {
            Phase = MatchPhase.Lobby;
            CountdownRemaining = -1f;
            RPC_ShowFeedback("Se cancelo el conteo", 2);
            return;
        }

        CountdownRemaining -= Runner.DeltaTime;
        if (CountdownRemaining > 0f)
        {
            return;
        }

        BeginMatch();
    }

    private void BeginMatch()
    {
        Phase = MatchPhase.Playing;
        CountdownRemaining = 0f;
        MatchTime = 0f;
        OvertimeRemaining = -1f;
        RoundSeed = Random.Range(1, int.MaxValue);
        WinnerName = default;
        WinnerDetail = default;
        WinnerScore = 0;

        foreach (var player in GetPlayers())
        {
            player.PrepareForMatch(SelectedGame);
        }

        RPC_ShowFeedback($"¡{MiniGameNames.Display(SelectedGame)}!", 1);
    }

    private void FinishMatch(PlayerController winner, string detail)
    {
        string name = winner != null ? winner.DisplayName : "Nadie";
        WinnerName = name;
        WinnerDetail = string.IsNullOrEmpty(detail) ? "Ganador" : detail;
        int score = winner != null ? winner.Score : 0;
        if (SelectedGame == MiniGameId.MemoryPuzzle)
        {
            score = Mathf.Max(score, MemoryPuzzleMinigame.PairCount);
        }

        WinnerScore = score;
        AwardCupPoints();
        Phase = MatchPhase.Results;
        RPC_ShowFeedback($"{name} gana", 0);
    }

    private void AwardCupPoints()
    {
        var ranked = new List<PlayerController>(GetPlayers());
        ranked.Sort((a, b) =>
        {
            bool aOut = a.IsEliminated;
            bool bOut = b.IsEliminated;
            if (aOut != bOut)
            {
                return aOut ? 1 : -1;
            }

            return b.Score.CompareTo(a.Score);
        });

        for (int i = 0; i < ranked.Count; i++)
        {
            int cup = Mathf.Clamp(4 - i, 1, 4);
            ranked[i].RPC_AddCupScore(cup);
        }
    }

    private void HandleDisconnectDuringMatch()
    {
        var players = GetPlayers();
        if (AllowSoloPractice)
        {
            return;
        }

        if (players.Length == 0)
        {
            ReturnToLobby();
            return;
        }

        if (players.Length == 1 && MatchTime > 1f)
        {
            FinishMatch(players[0], "el resto se desconecto");
        }
    }

    private void ResetPlayersForLobby()
    {
        foreach (var player in GetPlayers())
        {
            player.ResetForLobby();
        }
    }

    private void UnreadyEveryone()
    {
        foreach (var player in GetPlayers())
        {
            player.ForceUnready();
        }
    }

    private void HandlePhaseVisuals(MatchPhase previous, MatchPhase next)
    {
        bool leavingPlayArea = previous == MatchPhase.Playing ||
                               (previous == MatchPhase.Countdown && next != MatchPhase.Playing);
        if (leavingPlayArea)
        {
            _active?.OnMatchEnded();
            _active?.Cleanup();
        }

        if (next == MatchPhase.Countdown)
        {
            _active = FindMinigame(SelectedGame);
            _active?.OnCountdownStarted();
        }

        if (next == MatchPhase.Playing)
        {
            _active = FindMinigame(SelectedGame);
            _active?.OnMatchStarted();
        }

        if (next == MatchPhase.Lobby)
        {
            RestoreDefaultCamera();
            StageBackdrop.BeginStaticStage();
            foreach (var player in GetPlayers())
            {
                player.SetControlMode(PlayerControlMode.Platformer);
                player.SetWorldBounds(-BananaRushConfig.PlayerClampX, BananaRushConfig.PlayerClampX, BananaRushConfig.GroundTopY, -8f);
                player.SetPushEnabled(false);
            }
        }
    }

    private void BuildMinigames()
    {
        _minigames.Clear();
        _minigames.Add(GetOrAdd<BananaRainMinigame>());
        _minigames.Add(GetOrAdd<ParkourMinigame>());
        _minigames.Add(GetOrAdd<LogSurviveMinigame>());
        _minigames.Add(GetOrAdd<ChestBeatMinigame>());
        _minigames.Add(GetOrAdd<MemoryPuzzleMinigame>());
        _minigames.Add(GetOrAdd<TreeChopMinigame>());

        foreach (var minigame in _minigames)
        {
            minigame.Setup(this);
        }
    }

    private T GetOrAdd<T>() where T : MonoBehaviour, IMiniGame
    {
        T existing = GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    private IMiniGame FindMinigame(MiniGameId id)
    {
        foreach (var minigame in _minigames)
        {
            if (minigame.Id == id)
            {
                return minigame;
            }
        }

        return _minigames.Count > 0 ? _minigames[0] : null;
    }

    private MiniGameId ReadSessionMiniGame()
    {
        if (Runner.SessionInfo != null &&
            Runner.SessionInfo.Properties != null &&
            Runner.SessionInfo.Properties.TryGetValue(MiniGameNames.SessionPropertyKey, out SessionProperty value))
        {
            return MiniGameNames.Clamp((int)value);
        }

        return NetworkRunnerHandler.Instance != null
            ? NetworkRunnerHandler.Instance.PendingMiniGame
            : MiniGameId.BananaRain;
    }

    private void ApplySelectedGameToSession()
    {
        if (Runner.SessionInfo == null)
        {
            return;
        }

        Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
        {
            { MiniGameNames.SessionPropertyKey, (int)SelectedGame },
        });
    }

    private static void RestoreDefaultCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.orthographicSize = BananaRushConfig.CameraOrthographicSize;
        cam.transform.position = new Vector3(0f, BananaRushConfig.CameraCenterY, -10f);
    }
}
