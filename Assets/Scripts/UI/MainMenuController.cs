using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu: nickname, crear sala + minijuego, y lista de salas disponibles.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private InputField _nicknameInput;
    private InputField _sessionNameInput;
    private Dropdown _miniGameDropdown;
    private Text _statusText;
    private Text _hintText;
    private Button _createButton;
    private Button _joinTypedButton;
    private Button _refreshButton;
    private RectTransform _sessionListContent;
    private readonly List<GameObject> _sessionRows = new List<GameObject>();

    private void Awake()
    {
        UIFactory.EnsureCamera();
        UIFactory.EnsureEventSystem();
        BuildUI();
    }

    private async void Start()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            _statusText.color = new Color(0.8f, 0.85f, 0.7f);
            _statusText.text = "Buscando salas...";
            GameAudio.PlayMenu();
            await handler.StartBrowsingAsync();
        }
    }

    private void OnEnable()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            handler.OnConnectionFailedEvent += HandleConnectionFailed;
            handler.OnSessionListUpdatedEvent += HandleSessionList;
        }
    }

    private void OnDisable()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            handler.OnConnectionFailedEvent -= HandleConnectionFailed;
            handler.OnSessionListUpdatedEvent -= HandleSessionList;
        }
    }

    private void BuildUI()
    {
        var canvas = UIFactory.CreateCanvas("MainMenuCanvas");

        var title = UIFactory.CreateTitleText(canvas.transform, "BANANA RUSH", 64,
            new Color(1f, 0.82f, 0.20f), new Color(0.35f, 0.17f, 0.05f));
        UIFactory.SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -120), new Vector2(-20, -16));

        var left = UIFactory.CreatePanel(canvas.transform, "CreatePanel", new Color(0.10f, 0.11f, 0.16f, 0.96f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -250), new Vector2(-20, 170));

        var leftTitle = UIFactory.CreateText(left, "Crear o unirse", 20, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f));
        UIFactory.SetRect(leftTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -36), new Vector2(-12, -8));

        _nicknameInput = UIFactory.CreateInputField(left, "Tu nombre (ej: Tomi)");
        _nicknameInput.characterLimit = 14;
        UIFactory.SetRect(_nicknameInput.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -84), new Vector2(-24, -48));

        _sessionNameInput = UIFactory.CreateInputField(left, "Nombre de sala (ej: Jungla1)");
        UIFactory.SetRect(_sessionNameInput.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -130), new Vector2(-24, -94));

        _miniGameDropdown = UIFactory.CreateDropdown(left, MiniGameNames.DisplayNames);
        UIFactory.SetRect(_miniGameDropdown.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -176), new Vector2(-24, -140));
        _miniGameDropdown.onValueChanged.AddListener(_ => RefreshHint());

        _hintText = UIFactory.CreateText(left, MiniGameNames.Hint(MiniGameId.BananaRain), 13, TextAnchor.UpperLeft, new Color(0.75f, 0.78f, 0.82f));
        UIFactory.SetRect(_hintText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -250), new Vector2(-24, -182));

        _createButton = UIFactory.CreateButton(left, "Crear sala", new Color(0.24f, 0.55f, 0.28f));
        UIFactory.SetRect(_createButton.GetComponent<RectTransform>(),
            new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(24, 56), new Vector2(-8, 100));
        _createButton.onClick.AddListener(HandleCreateClicked);

        _joinTypedButton = UIFactory.CreateButton(left, "Unirse a esta", new Color(0.24f, 0.45f, 0.85f));
        UIFactory.SetRect(_joinTypedButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(8, 56), new Vector2(-24, 100));
        _joinTypedButton.onClick.AddListener(HandleJoinTypedClicked);

        var right = UIFactory.CreatePanel(canvas.transform, "BrowserPanel", new Color(0.10f, 0.11f, 0.16f, 0.96f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20, -250), new Vector2(520, 170));

        var rightTitle = UIFactory.CreateText(right, "Salas disponibles", 20, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.4f));
        UIFactory.SetRect(rightTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -36), new Vector2(-110, -8));

        _refreshButton = UIFactory.CreateButton(right, "Refresh", new Color(0.28f, 0.32f, 0.4f));
        UIFactory.SetRect(_refreshButton.GetComponent<RectTransform>(),
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-100, -36), new Vector2(-16, -8));
        _refreshButton.onClick.AddListener(HandleRefreshClicked);

        var listHost = UIFactory.CreatePanel(right, "List", new Color(0.06f, 0.07f, 0.1f, 0.65f),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(16, 16), new Vector2(-16, -48));
        _sessionListContent = listHost;

        _statusText = UIFactory.CreateText(canvas.transform, string.Empty, 16, TextAnchor.MiddleCenter, new Color(1f, 0.65f, 0.65f));
        UIFactory.SetRect(_statusText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 16), new Vector2(-40, 52));

        RefreshHint();
        RenderSessionList(new List<SessionInfo>());
    }

    private void RefreshHint()
    {
        _hintText.text = MiniGameNames.Hint(MiniGameNames.Clamp(_miniGameDropdown.value));
    }

    private async void HandleCreateClicked()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler == null)
        {
            _statusText.text = "Falta el NetworkRunnerHandler en la escena.";
            return;
        }

        SetBusy(true, "Creando sala...");
        bool success = await handler.CreateSessionAsync(
            _sessionNameInput.text,
            _nicknameInput.text,
            MiniGameNames.Clamp(_miniGameDropdown.value));

        if (!success)
        {
            SetBusy(false, null);
            await handler.StartBrowsingAsync();
        }
    }

    private async void HandleJoinTypedClicked()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_sessionNameInput.text))
        {
            _statusText.color = new Color(1f, 0.55f, 0.5f);
            _statusText.text = "Escribi el nombre de la sala o elegila de la lista.";
            return;
        }

        SetBusy(true, "Uniendose...");
        bool success = await handler.JoinSessionAsync(_sessionNameInput.text, _nicknameInput.text);
        if (!success)
        {
            SetBusy(false, null);
            await handler.StartBrowsingAsync();
        }
    }

    private async void HandleRefreshClicked()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler == null)
        {
            return;
        }

        _statusText.color = new Color(0.8f, 0.85f, 0.7f);
        _statusText.text = "Actualizando salas...";
        await handler.StopBrowsingAsync();
        await handler.StartBrowsingAsync();
    }

    private async void HandleJoinSession(string sessionName)
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler == null)
        {
            return;
        }

        _sessionNameInput.text = sessionName;
        SetBusy(true, $"Uniendose a {sessionName}...");
        bool success = await handler.JoinSessionAsync(sessionName, _nicknameInput.text);
        if (!success)
        {
            SetBusy(false, null);
            await handler.StartBrowsingAsync();
        }
    }

    private void HandleSessionList(List<SessionInfo> sessions)
    {
        RenderSessionList(sessions);
        if (_statusText != null && (_statusText.text.StartsWith("Buscando") || _statusText.text.StartsWith("Actualizando")))
        {
            _statusText.color = new Color(0.75f, 0.8f, 0.75f);
            _statusText.text = sessions.Count == 0 ? "No hay salas abiertas. Crea una." : $"{sessions.Count} sala(s) encontrada(s).";
        }
    }

    private void RenderSessionList(List<SessionInfo> sessions)
    {
        foreach (var row in _sessionRows)
        {
            if (row != null)
            {
                Destroy(row);
            }
        }

        _sessionRows.Clear();

        int visible = 0;
        for (int i = 0; i < sessions.Count; i++)
        {
            SessionInfo info = sessions[i];
            if (info == null || !info.IsValid || !info.IsVisible)
            {
                continue;
            }

            MiniGameId game = MiniGameId.BananaRain;
            if (info.Properties != null && info.Properties.TryGetValue(MiniGameNames.SessionPropertyKey, out SessionProperty value))
            {
                game = MiniGameNames.Clamp((int)value);
            }

            string label = $"{info.Name}   {info.PlayerCount}/{info.MaxPlayers}   {MiniGameNames.Display(game)}";
            var button = UIFactory.CreateButton(_sessionListContent, label, new Color(0.18f, 0.22f, 0.3f));
            float top = -8f - visible * 46f;
            UIFactory.SetRect(button.GetComponent<RectTransform>(),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, top - 38f), new Vector2(-8, top));

            string sessionName = info.Name;
            bool canJoin = info.IsOpen && info.PlayerCount < info.MaxPlayers;
            button.interactable = canJoin;
            button.onClick.AddListener(() => HandleJoinSession(sessionName));
            _sessionRows.Add(button.gameObject);
            visible++;
        }

        if (visible == 0)
        {
            var empty = UIFactory.CreateText(_sessionListContent, "No hay partidas en este momento.", 15, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.75f));
            UIFactory.SetRect(empty.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -80), new Vector2(-10, -20));
            _sessionRows.Add(empty.gameObject);
        }
    }

    private void SetBusy(bool busy, string message)
    {
        _createButton.interactable = !busy;
        _joinTypedButton.interactable = !busy;
        _refreshButton.interactable = !busy;
        if (!string.IsNullOrEmpty(message))
        {
            _statusText.color = new Color(0.8f, 0.85f, 0.9f);
            _statusText.text = message;
        }
    }

    private void HandleConnectionFailed(string reason)
    {
        if (_statusText == null)
        {
            return;
        }

        _statusText.color = new Color(1f, 0.5f, 0.5f);
        _statusText.text = $"No se pudo conectar: {reason}";
        SetBusy(false, null);
    }
}
