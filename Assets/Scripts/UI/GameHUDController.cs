using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD: lobby con ready check, scoreboard, toasts, resultados.
/// </summary>
public class GameHUDController : MonoBehaviour
{
    private const float ScoreboardRefreshInterval = 0.25f;

    private Text _infoText;
    private Text _toastText;
    private float _toastLife;
    private RectTransform _scoreboardContent;
    private readonly List<Text> _scoreRows = new List<Text>();

    private GameObject _lobbyPanel;
    private Text _lobbyTitle;
    private Text _lobbyHint;
    private Text _lobbyPlayers;
    private Dropdown _lobbyGameDropdown;
    private Button _readyButton;
    private Text _readyButtonLabel;

    private GameObject _banner;
    private Text _bannerText;

    private GameObject _endScreen;
    private Text _endScreenText;
    private Button _backToLobbyButton;

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
    }

    private void BuildUI()
    {
        var canvas = UIFactory.CreateCanvas("GameHUDCanvas");

        var topBar = UIFactory.CreatePanel(canvas.transform, "TopBar", new Color(0, 0, 0, 0.45f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), Vector2.zero);

        _infoText = UIFactory.CreateText(topBar, "Conectado", 16, TextAnchor.MiddleLeft, Color.white);
        UIFactory.SetRect(_infoText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-150, 0));

        var leaveButton = UIFactory.CreateButton(topBar, "Salir", new Color(0.8f, 0.25f, 0.25f));
        UIFactory.SetRect(leaveButton.GetComponent<RectTransform>(),
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-140, -18), new Vector2(-10, 18));
        leaveButton.onClick.AddListener(HandleLeaveClicked);

        BuildScoreboard(canvas.transform);
        BuildLobby(canvas.transform);
        BuildBanner(canvas.transform);
        BuildChest(canvas.transform);
        BuildToast(canvas.transform);
        BuildEndScreen(canvas.transform);
    }

    private void BuildScoreboard(Transform canvasTransform)
    {
        var panel = UIFactory.CreatePanel(canvasTransform, "Scoreboard", new Color(0, 0, 0, 0.45f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -270), new Vector2(250, -60));

        var title = UIFactory.CreateText(panel, "Jugadores", 16, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
        UIFactory.SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -26), new Vector2(-4, -2));

        var contentGO = new GameObject("Rows", typeof(RectTransform));
        contentGO.transform.SetParent(panel, false);
        _scoreboardContent = contentGO.GetComponent<RectTransform>();
        UIFactory.SetRect(_scoreboardContent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -200), new Vector2(-4, -28));
    }

    private void BuildLobby(Transform canvasTransform)
    {
        _lobbyPanel = UIFactory.CreatePanel(canvasTransform, "LobbyPanel", new Color(0.07f, 0.08f, 0.12f, 0.92f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280, -210), new Vector2(280, 180)).gameObject;

        _lobbyTitle = UIFactory.CreateTitleText(_lobbyPanel.transform, "LOBBY", 36, new Color(1f, 0.82f, 0.2f), new Color(0.25f, 0.12f, 0.05f));
        UIFactory.SetRect(_lobbyTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -70), new Vector2(-16, -8));

        _lobbyGameDropdown = UIFactory.CreateDropdown(_lobbyPanel.transform, MiniGameNames.DisplayNames);
        UIFactory.SetRect(_lobbyGameDropdown.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-180, -112), new Vector2(180, -76));
        _lobbyGameDropdown.onValueChanged.AddListener(HandleLobbyGameChanged);

        _lobbyHint = UIFactory.CreateText(_lobbyPanel.transform, MiniGameNames.Hint(MiniGameId.BananaRain), 14, TextAnchor.MiddleCenter, new Color(0.8f, 0.82f, 0.86f));
        UIFactory.SetRect(_lobbyHint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -168), new Vector2(-20, -118));

        _lobbyPlayers = UIFactory.CreateText(_lobbyPanel.transform, "Esperando jugadores...", 16, TextAnchor.UpperLeft, Color.white);
        UIFactory.SetRect(_lobbyPlayers.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 70), new Vector2(-24, -176));

        _readyButton = UIFactory.CreateButton(_lobbyPanel.transform, "ESTOY LISTO", new Color(0.22f, 0.55f, 0.28f));
        UIFactory.SetRect(_readyButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 16), new Vector2(130, 58));
        _readyButton.onClick.AddListener(HandleReadyClicked);
        _readyButtonLabel = _readyButton.GetComponentInChildren<Text>();
    }

    private void BuildBanner(Transform canvasTransform)
    {
        _banner = UIFactory.CreatePanel(canvasTransform, "Banner", new Color(0, 0, 0, 0.35f),
            new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(-260, -40), new Vector2(260, 40)).gameObject;
        _bannerText = UIFactory.CreateTitleText(_banner.transform, string.Empty, 40, new Color(1f, 0.86f, 0.3f), new Color(0.2f, 0.1f, 0.04f));
        _banner.SetActive(false);
    }

    private void BuildChest(Transform canvasTransform)
    {
        _chestPanel = UIFactory.CreatePanel(canvasTransform, "ChestHud", new Color(0, 0, 0, 0.45f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 20), new Vector2(180, 70)).gameObject;
        _heatLabel = UIFactory.CreateText(_chestPanel.transform, "Calor del pecho", 14, TextAnchor.MiddleCenter, Color.white);
        UIFactory.SetRect(_heatLabel.rectTransform, new Vector2(0, 0.55f), new Vector2(1, 1), new Vector2(8, 0), new Vector2(-8, -4));
        var back = UIFactory.CreatePanel(_chestPanel.transform, "HeatBack", new Color(0.2f, 0.1f, 0.1f, 1f),
            new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(12, 8), new Vector2(-12, -8));
        var fill = UIFactory.CreatePanel(back, "HeatFill", new Color(1f, 0.3f, 0.15f, 1f),
            new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
        _heatFill = fill.GetComponent<Image>();
        _chestPanel.SetActive(false);
    }

    private void BuildToast(Transform canvasTransform)
    {
        _toastText = UIFactory.CreateText(canvasTransform, string.Empty, 20, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.45f));
        UIFactory.SetRect(_toastText.rectTransform, new Vector2(0.15f, 0.62f), new Vector2(0.85f, 0.72f), Vector2.zero, Vector2.zero);
    }

    private void BuildEndScreen(Transform canvasTransform)
    {
        _endScreen = UIFactory.CreatePanel(canvasTransform, "EndScreen", new Color(0, 0, 0, 0.88f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        _endScreenText = UIFactory.CreateTitleText(_endScreen.transform, string.Empty, 48,
            new Color(1f, 0.82f, 0.20f), new Color(0.35f, 0.17f, 0.05f));
        UIFactory.SetRect(_endScreenText.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 0.75f), new Vector2(40, 0), new Vector2(-40, 0));

        _backToLobbyButton = UIFactory.CreateButton(_endScreen.transform, "Volver al lobby", new Color(0.24f, 0.5f, 0.85f));
        UIFactory.SetRect(_backToLobbyButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(-140, -24), new Vector2(140, 24));
        _backToLobbyButton.onClick.AddListener(() => BananaGameManager.Instance?.RPC_RequestReturnToLobby());
        _endScreen.SetActive(false);
    }

    private void UpdateLobby()
    {
        var manager = BananaGameManager.Instance;
        bool show = manager != null && manager.Phase == MatchPhase.Lobby;
        if (_lobbyPanel.activeSelf != show)
        {
            _lobbyPanel.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        bool isMaster = manager.Object != null && manager.Object.HasStateAuthority;
        _lobbyGameDropdown.interactable = isMaster;
        if ((int)manager.SelectedGame != _lobbyGameDropdown.value)
        {
            _lobbyGameDropdown.SetValueWithoutNotify((int)manager.SelectedGame);
        }

        _lobbyHint.text = MiniGameNames.Hint(manager.SelectedGame);

        var players = manager.GetPlayers().OrderBy(p => p.Object.InputAuthority.PlayerId).ToArray();
        var lines = new List<string>();
        foreach (var player in players)
        {
            string ready = player.IsReady ? "<color=#7CFF7C>LISTO</color>" : "<color=#FFB36B>esperando</color>";
            lines.Add($"• {player.DisplayName}  {ready}");
        }

        int readyCount = manager.ReadyCount();
        lines.Add(string.Empty);
        lines.Add($"Ready {readyCount}/{players.Length}   |   minimo {MiniGameNames.MinPlayersToStart}   |   maximo {MiniGameNames.MaxPlayers}");
        if (players.Length < MiniGameNames.MinPlayersToStart)
        {
            lines.Add("Falta gente. El juego NO arranca solo.");
        }
        else if (!manager.EveryoneReady())
        {
            lines.Add("Cuando TODOS apreten LISTO, arranca el conteo.");
        }

        _lobbyPlayers.supportRichText = true;
        _lobbyPlayers.text = string.Join("\n", lines);

        if (PlayerController.Local != null && _readyButtonLabel != null)
        {
            bool ready = PlayerController.Local.IsReady;
            _readyButtonLabel.text = ready ? "CANCELAR READY" : "ESTOY LISTO";
            _readyButton.GetComponent<Image>().color = ready ? new Color(0.55f, 0.28f, 0.2f) : new Color(0.22f, 0.55f, 0.28f);
        }
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
        _endScreen.SetActive(show);
        if (!show)
        {
            return;
        }

        string winner = manager.WinnerName.ToString();
        if (string.IsNullOrEmpty(winner))
        {
            winner = "Nadie";
        }

        string detail = manager.WinnerDetail.ToString();
        _endScreenText.text = string.IsNullOrEmpty(detail) ? $"¡{winner} gana!" : $"¡{winner} gana!\n{detail}";
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

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
            .OrderByDescending(p => p.Score)
            .ToList();

        for (int i = 0; i < players.Count; i++)
        {
            Text row = GetOrCreateRow(i);
            string ready = players[i].IsReady ? " ✓" : string.Empty;
            string outMark = players[i].IsEliminated ? " OUT" : string.Empty;
            row.text = $"{i + 1}. {players[i].DisplayName} - {players[i].Score}{ready}{outMark}";
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
        const float rowHeight = 22f;
        float top = -(index * rowHeight);
        float bottom = -((index + 1) * rowHeight);
        UIFactory.SetRect(row.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(2, bottom), new Vector2(-2, top));
        _scoreRows.Add(row);
        return row;
    }

    private void HandleLobbyGameChanged(int index)
    {
        var manager = BananaGameManager.Instance;
        if (manager == null || !manager.Object.HasStateAuthority)
        {
            return;
        }

        manager.SetSelectedGame(MiniGameNames.Clamp(index));
    }

    private void HandleReadyClicked()
    {
        PlayerController.Local?.RequestToggleReady();
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
