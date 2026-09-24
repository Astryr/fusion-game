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
    [Networked] public int PuzzleLockedMask { get; set; }
    [Networked] public int PuzzleFlipA { get; set; }
    [Networked] public int PuzzleFlipB { get; set; }
    [Networked] public int PuzzleTurnId { get; set; }
    [Networked] public float PuzzleRevealUntil { get; set; }
    [Networked] public float PuzzleTurnStartedAt { get; set; }
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
            PuzzleFlipA = -1;
            PuzzleFlipB = -1;
            PuzzleRevealUntil = -1f;
            SelectedGame = ReadSessionMiniGame();
            ApplySelectedGameToSession();
        }

        _renderedPhase = Phase;
        _renderedGame = SelectedGame;
        if (Phase == MatchPhase.Playing)
        {
            _active = FindMinigame(SelectedGame);
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
        if (players.Length < MinPlayersToStart)
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

    public void ResetPuzzleState()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        PuzzleLockedMask = 0;
        PuzzleFlipA = -1;
        PuzzleFlipB = -1;
        PuzzleRevealUntil = -1f;
        PuzzleTurnStartedAt = 0f;
        PlayerController first = FindNextPuzzlePlayer(-1);
        PuzzleTurnId = first != null ? first.Object.InputAuthority.PlayerId : 0;
    }

    public bool IsPuzzleCellLocked(int index)
    {
        return index >= 0 && (PuzzleLockedMask & (1 << index)) != 0;
    }

    public bool IsPuzzleCellFlipped(int index)
    {
        return index == PuzzleFlipA || index == PuzzleFlipB;
    }

    public bool IsLocalPuzzleTurn()
    {
        return PlayerController.Local != null &&
               PlayerController.Local.Object != null &&
               PlayerController.Local.Object.InputAuthority.PlayerId == PuzzleTurnId;
    }

    public PlayerController FindPlayerById(int playerId)
    {
        foreach (var player in GetPlayers())
        {
            if (player != null && player.Object != null && player.Object.InputAuthority.PlayerId == playerId)
            {
                return player;
            }
        }

        return null;
    }

    public void TickPuzzle(float peekSeconds, float turnTimeout)
    {
        if (!Object.HasStateAuthority || Phase != MatchPhase.Playing)
        {
            return;
        }

        if (MatchTime < peekSeconds)
        {
            return;
        }

        if (PuzzleTurnStartedAt < peekSeconds)
        {
            PuzzleTurnStartedAt = peekSeconds;
        }

        if (PuzzleRevealUntil >= 0f && MatchTime >= PuzzleRevealUntil)
        {
            PuzzleFlipA = -1;
            PuzzleFlipB = -1;
            PuzzleRevealUntil = -1f;
            AdvancePuzzleTurn();
        }

        if (PuzzleRevealUntil < 0f && MatchTime - PuzzleTurnStartedAt >= turnTimeout)
        {
            PuzzleFlipA = -1;
            PuzzleFlipB = -1;
            AdvancePuzzleTurn();
            RPC_ShowFeedback("Turno pasado: se quedo quieto", 2);
        }

        if (CountLockedPuzzleCells() >= 16)
        {
            DeclareHighestScoreWinner("mas parejas");
            return;
        }

        if (MatchTime >= 90f)
        {
            DeclareHighestScoreWinner("mas parejas al corte");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestPuzzleFlip(int index, PlayerRef source)
    {
        if (Phase != MatchPhase.Playing || SelectedGame != MiniGameId.MemoryPuzzle)
        {
            return;
        }

        if (index < 0 || index > 15 || MatchTime < MemoryPuzzleMinigame.PeekSeconds)
        {
            return;
        }

        if (PuzzleRevealUntil >= 0f && MatchTime < PuzzleRevealUntil)
        {
            return;
        }

        PlayerController player = FindPlayerById(source.PlayerId);
        if (player == null || player.IsEliminated || player.Object.InputAuthority.PlayerId != PuzzleTurnId)
        {
            return;
        }

        if (IsPuzzleCellLocked(index) || IsPuzzleCellFlipped(index))
        {
            return;
        }

        if (PuzzleFlipA < 0)
        {
            PuzzleFlipA = index;
            return;
        }

        if (PuzzleFlipB >= 0)
        {
            return;
        }

        PuzzleFlipB = index;
        int pieceA = MemoryPuzzleMinigame.PieceAt(RoundSeed, PuzzleFlipA);
        int pieceB = MemoryPuzzleMinigame.PieceAt(RoundSeed, PuzzleFlipB);
        if (pieceA == pieceB)
        {
            PuzzleLockedMask |= (1 << PuzzleFlipA) | (1 << PuzzleFlipB);
            PuzzleFlipA = -1;
            PuzzleFlipB = -1;
            PuzzleTurnStartedAt = MatchTime;
            player.RPC_SetScore(player.Score + 1);
            RPC_ShowFeedback($"{player.DisplayName} encontro pareja", 0);
            if (CountLockedPuzzleCells() >= 16)
            {
                DeclareHighestScoreWinner("mas parejas");
            }
        }
        else
        {
            PuzzleRevealUntil = MatchTime + MemoryPuzzleMinigame.MismatchSeconds;
        }
    }

    private void AdvancePuzzleTurn()
    {
        PlayerController next = FindNextPuzzlePlayer(PuzzleTurnId);
        if (next != null)
        {
            PuzzleTurnId = next.Object.InputAuthority.PlayerId;
        }

        PuzzleTurnStartedAt = MatchTime;
    }

    private PlayerController FindNextPuzzlePlayer(int currentId)
    {
        PlayerController[] players = GetPlayers();
        PlayerController first = null;
        PlayerController after = null;
        int firstId = int.MaxValue;
        int afterId = int.MaxValue;

        foreach (var player in players)
        {
            if (player == null || player.Object == null || player.IsEliminated)
            {
                continue;
            }

            int id = player.Object.InputAuthority.PlayerId;
            if (id < firstId)
            {
                firstId = id;
                first = player;
            }

            if (id > currentId && id < afterId)
            {
                afterId = id;
                after = player;
            }
        }

        return after != null ? after : first;
    }

    private int CountLockedPuzzleCells()
    {
        int count = 0;
        int mask = PuzzleLockedMask;
        while (mask != 0)
        {
            count += mask & 1;
            mask >>= 1;
        }

        return count;
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
