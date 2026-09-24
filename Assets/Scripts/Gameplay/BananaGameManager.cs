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

    public int MinPlayersToStart => MiniGameNames.MinPlayersToStart;

    [Networked] public MatchPhase Phase { get; set; }
    [Networked] public MiniGameId SelectedGame { get; set; }
    [Networked] public float CountdownRemaining { get; set; }
    [Networked] public float MatchTime { get; set; }
    [Networked] public float OvertimeRemaining { get; set; }
    [Networked] public float AvalancheX { get; set; }
    [Networked] public float LogHalfWidth { get; set; }
    [Networked] public int RoundSeed { get; set; }
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
            SelectedGame = ReadSessionMiniGame();
            ApplySelectedGameToSession();
        }

        _renderedPhase = Phase;
        _renderedGame = SelectedGame;
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

        if (Phase == MatchPhase.Playing)
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
            _active?.Tick(Runner.DeltaTime, true);
        }
    }

    public PlayerController[] GetPlayers()
    {
        return FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
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
        if (players.Length < MiniGameNames.MinPlayersToStart)
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

    public void SetSelectedGame(MiniGameId id)
    {
        if (!Object.HasStateAuthority || Phase != MatchPhase.Lobby)
        {
            return;
        }

        SelectedGame = MiniGameNames.Clamp((int)id);
        ApplySelectedGameToSession();
        UnreadyEveryone();
        RPC_ShowFeedback($"Minijuego: {MiniGameNames.Display(SelectedGame)}", 1);
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
        if (!EveryoneReady() || ConnectedPlayerCount() < MiniGameNames.MinPlayersToStart)
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
        Phase = MatchPhase.Results;
        RPC_ShowFeedback($"{name} gana", 0);
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
        if (previous == MatchPhase.Playing)
        {
            _active?.OnMatchEnded();
            _active?.Cleanup();
        }

        if (next == MatchPhase.Playing)
        {
            _active = FindMinigame(SelectedGame);
            _active?.OnMatchStarted();
        }

        if (next == MatchPhase.Lobby)
        {
            RestoreDefaultCamera();
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
