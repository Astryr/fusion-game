using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD de la partida: barra superior (sala + jugadores + salir), tabla de
/// puntajes en la esquina superior izquierda (se refresca sola mirando el
/// <c>Score</c>/<c>Nickname</c> [Networked] de cada <see cref="PlayerController"/>)
/// y la pantalla final en negro con el nombre del ganador cuando
/// <see cref="BananaGameManager.IsGameOver"/> se pone en true.
/// </summary>
public class GameHUDController : MonoBehaviour
{
    private const float ScoreboardRefreshInterval = 0.25f;

    private Text _infoText;
    private RectTransform _scoreboardContent;
    private readonly List<Text> _scoreRows = new List<Text>();

    private GameObject _endScreen;
    private Text _endScreenText;

    private float _scoreboardTimer;
    private bool _gameOverShown;

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

            int currentCount = handler.Runner != null && handler.Runner.SessionInfo != null
                ? handler.Runner.SessionInfo.PlayerCount
                : 1;
            UpdateInfoText(currentCount);
        }
    }

    private void OnDisable()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            handler.OnPlayerCountChangedEvent -= HandlePlayerCountChanged;
            handler.OnDisconnectedEvent -= HandleDisconnected;
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

        CheckGameOver();
    }

    private void BuildUI()
    {
        var canvas = UIFactory.CreateCanvas("GameHUDCanvas");

        var topBar = UIFactory.CreatePanel(canvas.transform, "TopBar", new Color(0, 0, 0, 0.45f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), Vector2.zero);

        _infoText = UIFactory.CreateText(topBar, "Conectado", 18, TextAnchor.MiddleLeft, Color.white);
        UIFactory.SetRect(_infoText.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-150, 0));

        var leaveButton = UIFactory.CreateButton(topBar, "Salir", new Color(0.8f, 0.25f, 0.25f));
        UIFactory.SetRect(leaveButton.GetComponent<RectTransform>(),
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-140, -18), new Vector2(-10, 18));
        leaveButton.onClick.AddListener(HandleLeaveClicked);

        BuildScoreboard(canvas.transform);
        BuildEndScreen(canvas.transform);
    }

    private void BuildScoreboard(Transform canvasTransform)
    {
        var panel = UIFactory.CreatePanel(canvasTransform, "Scoreboard", new Color(0, 0, 0, 0.45f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -260), new Vector2(230, -60));

        var title = UIFactory.CreateText(panel, "Puntajes", 16, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
        UIFactory.SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -26), new Vector2(-4, -2));

        var contentGO = new GameObject("Rows", typeof(RectTransform));
        contentGO.transform.SetParent(panel, false);
        _scoreboardContent = contentGO.GetComponent<RectTransform>();
        UIFactory.SetRect(_scoreboardContent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -170), new Vector2(-4, -28));
    }

    private void BuildEndScreen(Transform canvasTransform)
    {
        _endScreen = UIFactory.CreatePanel(canvasTransform, "EndScreen", Color.black,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        _endScreenText = UIFactory.CreateText(_endScreen.transform, string.Empty, 48, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
        UIFactory.SetRect(_endScreenText.rectTransform, Vector2.zero, Vector2.one, new Vector2(40, 0), new Vector2(-40, 0));

        _endScreen.SetActive(false);
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
            string name = players[i].Nickname.ToString();
            if (string.IsNullOrEmpty(name))
            {
                name = $"Jugador {players[i].Object.InputAuthority.PlayerId + 1}";
            }

            row.text = $"{i + 1}. {name} - {players[i].Score}";
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

    private void CheckGameOver()
    {
        if (_gameOverShown)
        {
            return;
        }

        var manager = BananaGameManager.Instance;
        if (manager == null || !manager.IsGameOver)
        {
            return;
        }

        _gameOverShown = true;
        string winner = manager.WinnerName.ToString();
        if (string.IsNullOrEmpty(winner))
        {
            winner = "??";
        }

        _endScreenText.text = $"\u00a1{winner} gana!";
        _endScreen.SetActive(true);
    }

    private void HandlePlayerCountChanged(int count)
    {
        UpdateInfoText(count);
    }

    private void UpdateInfoText(int count)
    {
        var handler = NetworkRunnerHandler.Instance;
        string sessionName = handler != null && !string.IsNullOrEmpty(handler.CurrentSessionName)
            ? handler.CurrentSessionName
            : "-";
        _infoText.text = $"Sala: {sessionName}    |    Jugadores conectados: {count}";
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
