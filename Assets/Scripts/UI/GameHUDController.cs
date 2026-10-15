using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameHUDController : MonoBehaviour
{
    private const float ScoreboardRefreshInterval = 0.25f;
    private const float EndScreenHoldSeconds = 4.5f;

    private Text _infoText;
    private Text _toastText;
    private float _toastLife;
    private bool _toastInLobby;
    private GameObject _topBar;
    private RectTransform _scoreboardContent;
    private GameObject _scoreboardPanel;
    private readonly List<Text> _scoreRows = new List<Text>();

    private LobbyScreen _lobby;
    private readonly LobbyData _lobbyData = new LobbyData();
    private ChatController _chat;

    private GameObject _banner;
    private Text _bannerText;

    private GameObject _endScreen;
    private Text _endScreenText;
    private Button _backToLobbyButton;
    private float _endScreenShownAt = -1f;

    private GameObject _chestPanel;
    private Image _heatFill;
    private Text _heatLabel;

    private float _scoreboardTimer;
    private int _playerCount = 1;
    private bool _subscribedFeedback;

    private void Awake()
    {
        UIFactory.EnsureCamera();
        UIFactory.EnsureEventSystem();
        BuildUI();
    }

    private void OnEnable()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            handler.OnPlayerCountChangedEvent += HandlePlayerCountChanged;
            handler.OnDisconnectedEvent += HandleDisconnected;

            _playerCount = handler.Runner != null && handler.Runner.SessionInfo != null
                ? handler.Runner.SessionInfo.PlayerCount
                : 1;
        }

        GameFeedback.ToastRaised += HandleToast;
        GameFeedback.WorldPopupRaised += HandleWorldPopup;
        _subscribedFeedback = true;
    }

    private void OnDisable()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            handler.OnPlayerCountChangedEvent -= HandlePlayerCountChanged;
            handler.OnDisconnectedEvent -= HandleDisconnected;
        }

        if (_subscribedFeedback)
        {
            GameFeedback.ToastRaised -= HandleToast;
            GameFeedback.WorldPopupRaised -= HandleWorldPopup;
        }
    }

    private void Update()
    {
        _scoreboardTimer -= Time.deltaTime;
        if (_scoreboardTimer <= 0f)
        {
            _scoreboardTimer = ScoreboardRefreshInterval;
            RefreshScoreboard();
        }

        if (_toastLife > 0f)
        {
            _toastLife -= Time.deltaTime;
            if (_toastLife <= 0f && _toastText != null)
            {
                _toastText.text = string.Empty;
            }
        }

        UpdateLobby();
        UpdateBanner();
        UpdateChestHud();
        UpdateEndScreen();
        UpdateInfoExtra();
        UpdateMusic();
    }

    private void BuildUI()
    {
        var canvas = UIFactory.CreateCanvas("GameHUDCanvas");

        BuildTopBar(canvas.transform);
        BuildScoreboard(canvas.transform);
        BuildLobby(canvas.transform);
        BuildBanner(canvas.transform);
        BuildChest(canvas.transform);
        BuildToast(canvas.transform);
        BuildEndScreen(canvas.transform);

        _chat = gameObject.AddComponent<ChatController>();
        _chat.Build();
    }

    private void BuildTopBar(Transform canvasTransform)
    {
        Image bar = UIStyle.CreateImage(canvasTransform, "TopBar", new Color(0.02f, 0.07f, 0.05f, 0.82f), 2);
        bar.sprite = null;
        bar.type = Image.Type.Simple;
        bar.raycastTarget = false;
        _topBar = bar.gameObject;
        UIFactory.SetRect(bar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), Vector2.zero);

        Image line = UIStyle.CreateImage(bar.transform, "Line", UIStyle.Banana, 2);
        line.sprite = null;
        line.type = Image.Type.Simple;
        line.raycastTarget = false;
        UIFactory.SetRect(line.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f));

        _infoText = UIFactory.CreateText(bar.transform, "Conectado", 17, TextAnchor.MiddleLeft, Color.white);
        _infoText.fontStyle = FontStyle.Bold;
        _infoText.raycastTarget = false;
        _infoText.supportRichText = false;
        UIFactory.SetRect(_infoText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-150, 0));

        Button leaveButton = UIStyle.CreateButton(bar.transform, "SALIR", UIStyle.Danger, 22, 10, 4f);
        UIFactory.SetRect(leaveButton.GetComponent<RectTransform>(),
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-132, -17), new Vector2(-12, 17));
        leaveButton.onClick.AddListener(HandleLeaveClicked);
    }

    private void BuildScoreboard(Transform canvasTransform)
    {
        UIStyle.Frame frame = UIStyle.CreateFrame(canvasTransform, "Scoreboard", UIStyle.WithAlpha(UIStyle.Banana, 0.9f),
            new Color(0.03f, 0.09f, 0.06f, 0.78f), 14, 2f);
        _scoreboardPanel = frame.Border.gameObject;
        frame.Border.raycastTarget = false;
        frame.Fill.raycastTarget = false;
        UIFactory.SetRect(frame.Border.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -270), new Vector2(262, -62));

        Text title = UIStyle.CreateHeading(frame.Border.transform, "JUGADORES", 24, UIStyle.Banana, UIStyle.Brown);
        UIFactory.SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -34), new Vector2(-4, -4));

        var contentGO = new GameObject("Rows", typeof(RectTransform));
        contentGO.transform.SetParent(frame.Border.transform, false);
        _scoreboardContent = contentGO.GetComponent<RectTransform>();
        UIFactory.SetRect(_scoreboardContent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -196), new Vector2(-8, -38));
    }

    private void BuildLobby(Transform canvasTransform)
    {
        _lobby = LobbyScreen.Create(canvasTransform);
        _lobby.ReadyClicked += HandleReadyClicked;
        _lobby.LeaveClicked += HandleLeaveClicked;
        _lobby.SoloClicked += HandleSoloClicked;
        _lobby.GameChosen += index => RequestLobbyGame(MiniGameNames.Clamp(index));
        _lobby.GameCycled += CycleLobbyGame;
    }

    private void BuildBanner(Transform canvasTransform)
    {
        Image pill = UIStyle.CreateImage(canvasTransform, "Banner", new Color(0.02f, 0.07f, 0.05f, 0.55f), 28);
        pill.raycastTarget = false;
        _banner = pill.gameObject;
        UIFactory.SetRect(pill.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(-230, -54), new Vector2(230, 54));
        _bannerText = UIStyle.CreateHeading(_banner.transform, string.Empty, 72, UIStyle.Banana, UIStyle.Brown);
        UIFactory.SetRect(_bannerText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _banner.SetActive(false);
    }

    private void BuildChest(Transform canvasTransform)
    {
        Image panel = UIStyle.CreateImage(canvasTransform, "ChestHud", new Color(0.02f, 0.07f, 0.05f, 0.7f), 14);
        panel.raycastTarget = false;
        _chestPanel = panel.gameObject;
        UIFactory.SetRect(panel.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 20), new Vector2(180, 74));

        _heatLabel = UIFactory.CreateText(_chestPanel.transform, "Calor del pecho", 15, TextAnchor.MiddleCenter, Color.white);
        _heatLabel.fontStyle = FontStyle.Bold;
        UIFactory.SetRect(_heatLabel.rectTransform, new Vector2(0, 0.52f), new Vector2(1, 1), new Vector2(8, 0), new Vector2(-8, -4));

        Image back = UIStyle.CreateImage(_chestPanel.transform, "HeatBack", new Color(0.18f, 0.09f, 0.08f, 1f), 8);
        UIFactory.SetRect(back.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(12, 8), new Vector2(-12, -6));

        Image fill = UIStyle.CreateImage(back.transform, "HeatFill", new Color(1f, 0.3f, 0.15f, 1f), 8);
        UIFactory.SetRect(fill.rectTransform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
        _heatFill = fill;
        _chestPanel.SetActive(false);
    }

    private void BuildToast(Transform canvasTransform)
    {
        _toastText = UIFactory.CreateText(canvasTransform, string.Empty, 22, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.45f));
        _toastText.fontStyle = FontStyle.Bold;
        _toastText.raycastTarget = false;
        _toastText.supportRichText = false;
        var outline = _toastText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.05f, 0.02f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        PlaceToast(false);
    }

    private void PlaceToast(bool inLobby)
    {
        _toastInLobby = inLobby;
        Vector2 min = inLobby ? new Vector2(0.37f, 0.19f) : new Vector2(0.15f, 0.62f);
        Vector2 max = inLobby ? new Vector2(0.98f, 0.255f) : new Vector2(0.85f, 0.72f);
        UIFactory.SetRect(_toastText.rectTransform, min, max, Vector2.zero, Vector2.zero);
    }

    private void BuildEndScreen(Transform canvasTransform)
    {
        Image overlay = UIStyle.CreateImage(canvasTransform, "EndScreen", new Color(0.01f, 0.05f, 0.03f, 0.88f), 2);
        overlay.sprite = null;
        overlay.type = Image.Type.Simple;
        _endScreen = overlay.gameObject;
        UIFactory.SetRect(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Sprite emblem = Resources.Load<Sprite>("Menu/BananaCupEmblem");
        if (emblem != null)
        {
            Image icon = UIStyle.CreateSprite(_endScreen.transform, "Emblem", emblem, Color.white);
            UIFactory.SetRect(icon.rectTransform, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(-90, -90), new Vector2(90, 90));
            var bob = icon.gameObject.AddComponent<UiBob>();
            bob.PositionAmplitude = new Vector2(0f, 8f);
            bob.RotationAmplitude = 3f;
        }

        _endScreenText = UIFactory.CreateTitleText(_endScreen.transform, string.Empty, 48,
            new Color(1f, 0.82f, 0.20f), new Color(0.35f, 0.17f, 0.05f));
        _endScreenText.raycastTarget = false;
        UIFactory.SetRect(_endScreenText.rectTransform, new Vector2(0, 0.3f), new Vector2(1, 0.68f), new Vector2(40, 0), new Vector2(-40, 0));

        _backToLobbyButton = UIStyle.CreateButton(_endScreen.transform, "VOLVER AL LOBBY", UIStyle.Info, 28, 14, 5f);
        UIFactory.SetRect(_backToLobbyButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.14f), new Vector2(-170, -27), new Vector2(170, 27));
        _backToLobbyButton.onClick.AddListener(() => BananaGameManager.Instance?.RPC_RequestReturnToLobby());
        _endScreen.SetActive(false);
    }

    private void UpdateLobby()
    {
        var manager = BananaGameManager.Instance;
        bool show = manager != null && manager.Phase == MatchPhase.Lobby;
        _lobby.SetVisible(show);
        if (_topBar.activeSelf == show)
        {
            _topBar.SetActive(!show);
        }

        if (_toastInLobby != show)
        {
            PlaceToast(show);
        }

        if (!show)
        {
            return;
        }

        PlayerController[] players = manager.GetPlayers()
            .OrderBy(p => p.Object.InputAuthority.PlayerId)
            .ToArray();

        PlayerRef hostRef = manager.Object != null ? manager.Object.StateAuthority : PlayerRef.None;
        PlayerController local = PlayerController.Local;
        var data = _lobbyData;
        var handler = NetworkRunnerHandler.Instance;

        data.SessionName = handler != null && !string.IsNullOrEmpty(handler.CurrentSessionName) ? handler.CurrentSessionName : "-";
        data.ConnectedCount = _playerCount;
        data.SelectedGame = (int)manager.SelectedGame;
        data.Hint = MiniGameNames.Hint(manager.SelectedGame);
        data.IsMaster = manager.Object != null && manager.Object.HasStateAuthority;
        data.SoloOn = manager.AllowSoloPractice;
        data.LocalReady = local != null && local.IsReady;

        int readyCount = 0;
        for (int i = 0; i < data.Slots.Length; i++)
        {
            if (i >= players.Length)
            {
                data.Slots[i] = default;
                continue;
            }

            PlayerController player = players[i];
            if (player.IsReady)
            {
                readyCount++;
            }

            data.Slots[i] = new LobbySlot
            {
                Occupied = true,
                Name = player.DisplayName,
                Tint = player.TintColor,
                IsHost = hostRef != PlayerRef.None && player.Object.InputAuthority == hostRef,
                IsLocal = player == local,
                IsReady = player.IsReady,
                CupScore = player.CupScore,
            };
        }

        int minimum = manager.MinPlayersToStart;
        bool everyoneReady = players.Length >= minimum && players.Length >= _playerCount && readyCount == players.Length;
        if (manager.AllowSoloPractice)
        {
            data.StatusMain = "Modo prueba: con ESTOY LISTO arranca aunque estes solo.";
        }
        else if (players.Length < minimum)
        {
            data.StatusMain = "Falta gente. Si estas solo, usa PROBAR SOLO.";
        }
        else if (!everyoneReady)
        {
            data.StatusMain = "Cuando TODOS apreten LISTO, arranca el conteo.";
        }
        else
        {
            data.StatusMain = "Todos listos...";
        }

        data.StatusSub = $"Listos {readyCount}/{players.Length}   ·   minimo {minimum}   ·   maximo {MiniGameNames.MaxPlayers}";
        _lobby.Refresh(data);
    }

    private void UpdateBanner()
    {
        var manager = BananaGameManager.Instance;
        if (manager == null)
        {
            _banner.SetActive(false);
            return;
        }

        if (manager.Phase == MatchPhase.Countdown)
        {
            int seconds = Mathf.CeilToInt(manager.CountdownRemaining);
            _bannerText.text = seconds > 0 ? seconds.ToString() : "YA";
            _banner.SetActive(true);
            return;
        }

        if (manager.Phase == MatchPhase.Playing)
        {
            if (manager.SelectedGame == MiniGameId.BananaRain && manager.OvertimeRemaining >= 0f)
            {
                _bannerText.text = $"CORTE {Mathf.CeilToInt(manager.OvertimeRemaining)}";
                _banner.SetActive(true);
                return;
            }
        }

        _banner.SetActive(false);
    }

    private void UpdateChestHud()
    {
        var manager = BananaGameManager.Instance;
        bool show = manager != null && manager.Phase == MatchPhase.Playing &&
                    manager.SelectedGame == MiniGameId.ChestBeat && PlayerController.Local != null;
        _chestPanel.SetActive(show);
        if (!show)
        {
            return;
        }

        float heat = PlayerController.Local.Heat;
        _heatFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(heat), 1f);
        _heatFill.color = Color.Lerp(new Color(0.4f, 0.85f, 0.3f), new Color(1f, 0.15f, 0.1f), heat);
        _heatLabel.text = heat >= 0.78f ? "¡CUIDADO! El pecho se pone rojo" : "Spamea ESPACIO con ritmo";
    }

    private void UpdateEndScreen()
    {
        var manager = BananaGameManager.Instance;
        bool show = manager != null && manager.Phase == MatchPhase.Results;
        if (!show)
        {
            _endScreenShownAt = -1f;
            _endScreen.SetActive(false);
            return;
        }

        _endScreen.SetActive(true);

        if (_endScreenShownAt < 0f)
        {
            _endScreenShownAt = Time.unscaledTime;
        }

        string winner = manager.WinnerName.ToString();
        if (string.IsNullOrEmpty(winner))
        {
            winner = "Nadie";
        }

        if (manager.SelectedGame == MiniGameId.Parkour)
        {
            _endScreenText.fontSize = 30;
            string board = manager.ResultsBoard.ToString();
            _endScreenText.text = string.IsNullOrEmpty(board) ? BuildParkourResults(manager.WinnerName.ToString()) : board;
        }
        else
        {
            _endScreenText.fontSize = 48;
            _endScreenText.text = $"{winner.ToUpper()} GANO\nPUNTOS: {manager.WinnerScore}";
        }

        bool canReturn = Time.unscaledTime - _endScreenShownAt >= EndScreenHoldSeconds;
        _backToLobbyButton.gameObject.SetActive(canReturn);
    }

    private static string BuildParkourResults(string winnerName)
    {
        var players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
            .Where(p => p != null && p.IsSpawned)
            .ToArray();
        var finishers = players
            .Where(p => !p.IsEliminated || (!string.IsNullOrEmpty(winnerName) && p.DisplayName == winnerName))
            .OrderByDescending(p => !string.IsNullOrEmpty(winnerName) && p.DisplayName == winnerName)
            .ThenByDescending(p => p.Score)
            .ThenByDescending(p => p.transform.position.x)
            .ToArray();
        var fallen = players
            .Where(p => p.IsEliminated && (string.IsNullOrEmpty(winnerName) || p.DisplayName != winnerName))
            .Select(p => p.DisplayName)
            .ToArray();

        var lines = new List<string>();
        string[] places = { "1er Lugar", "2do lugar", "3er lugar", "4to lugar" };
        for (int i = 0; i < finishers.Length && i < places.Length; i++)
        {
            lines.Add($"{places[i]}: {finishers[i].DisplayName}.");
        }

        if (fallen.Length > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"Caidos: {string.Join(", ", fallen)}.");
        }

        return lines.Count > 0 ? string.Join("\n", lines) : "Nadie termino.";
    }

    private static void UpdateMusic()
    {
        var manager = BananaGameManager.Instance;
        if (manager == null || manager.Phase == MatchPhase.Lobby)
        {
            GameAudio.PlayMenu();
            return;
        }

        GameAudio.PlayMinigames();
    }

    private void UpdateInfoExtra()
    {
        var handler = NetworkRunnerHandler.Instance;
        string sessionName = handler != null && !string.IsNullOrEmpty(handler.CurrentSessionName)
            ? handler.CurrentSessionName
            : "-";

        string extra = string.Empty;
        var manager = BananaGameManager.Instance;
        if (manager != null)
        {
            extra = $"    |    {MiniGameNames.Display(manager.SelectedGame)}";
            if (manager.Phase == MatchPhase.Playing)
            {
                extra += $"    |    {Mathf.FloorToInt(manager.MatchTime)}s";
            }
        }

        _infoText.text = $"Sala: {sessionName}    |    Jugadores: {_playerCount}/{MiniGameNames.MaxPlayers}{extra}";
    }

    private void RefreshScoreboard()
    {
        if (_scoreboardContent == null)
        {
            return;
        }

        bool hideForLobby = BananaGameManager.Instance != null &&
                            (BananaGameManager.Instance.Phase == MatchPhase.Lobby ||
                             BananaGameManager.Instance.Phase == MatchPhase.Results);
        if (_scoreboardPanel != null)
        {
            _scoreboardPanel.SetActive(!hideForLobby);
        }

        if (hideForLobby)
        {
            return;
        }

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
            .Where(p => p != null && p.IsSpawned)
            .OrderByDescending(p => p.Score)
            .ToList();

        for (int i = 0; i < players.Count; i++)
        {
            Text row = GetOrCreateRow(i);
            string ready = players[i].IsReady ? " ✓" : string.Empty;
            string outMark = players[i].IsEliminated ? " OUT" : string.Empty;
            row.text = $"{i + 1}. {players[i].DisplayName} - {players[i].Score}  (Copa {players[i].CupScore}){ready}{outMark}";
            row.color = players[i].IsEliminated ? new Color(1f, 0.5f, 0.45f) : Color.white;
            row.gameObject.SetActive(true);
        }

        for (int i = players.Count; i < _scoreRows.Count; i++)
        {
            _scoreRows[i].gameObject.SetActive(false);
        }
    }

    private Text GetOrCreateRow(int index)
    {
        if (index < _scoreRows.Count)
        {
            return _scoreRows[index];
        }

        Text row = UIFactory.CreateText(_scoreboardContent, string.Empty, 15, TextAnchor.MiddleLeft, Color.white);
        row.raycastTarget = false;
        row.supportRichText = false;
        const float rowHeight = 22f;
        float top = -(index * rowHeight);
        float bottom = -((index + 1) * rowHeight);
        UIFactory.SetRect(row.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(2, bottom), new Vector2(-2, top));
        _scoreRows.Add(row);
        return row;
    }

    private void CycleLobbyGame(int direction)
    {
        var manager = BananaGameManager.Instance;
        int current = manager != null ? (int)manager.SelectedGame : 0;
        int count = MiniGameNames.DisplayNames.Length;
        int next = (current + direction) % count;
        if (next < 0)
        {
            next += count;
        }

        RequestLobbyGame(MiniGameNames.Clamp(next));
    }

    private static void RequestLobbyGame(MiniGameId id)
    {
        var manager = BananaGameManager.Instance;
        if (manager == null || manager.Phase != MatchPhase.Lobby)
        {
            return;
        }

        manager.RPC_RequestSelectGame((int)id);
    }

    private void HandleReadyClicked()
    {
        PlayerController.Local?.RequestToggleReady();
    }

    private void HandleSoloClicked()
    {
        var manager = BananaGameManager.Instance;
        if (manager == null || !manager.Object.HasStateAuthority)
        {
            return;
        }

        manager.SetSoloPractice(!manager.AllowSoloPractice);
    }

    private void HandleToast(string message, Color color)
    {
        if (_toastText == null)
        {
            return;
        }

        _toastText.text = message;
        _toastText.color = color;
        _toastLife = 2.4f;
    }

    private static void HandleWorldPopup(Vector3 position, string message, Color color)
    {
        WorldPopup.Spawn(position, message, color);
    }

    private void HandlePlayerCountChanged(int count)
    {
        _playerCount = count;
    }

    private void HandleLeaveClicked()
    {
        NetworkRunnerHandler.Instance?.Disconnect();
    }

    private void HandleDisconnected()
    {
        SceneManager.LoadScene(0);
    }
}
