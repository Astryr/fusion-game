using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const float RowHeight = 64f;
    private const float RowGap = 8f;

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
        Camera camera = UIFactory.EnsureCamera();
        UIFactory.EnsureEventSystem();

        var ambience = new GameObject("MenuAmbience").AddComponent<MenuAmbience>();
        ambience.Build(camera);

        BuildUI();
    }

    private async void Start()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            string lastMessage = handler.ConsumeDisconnectMessage();
            if (!string.IsNullOrEmpty(lastMessage))
            {
                ShowStatus(lastMessage, new Color(1f, 0.62f, 0.5f));
            }
            else
            {
                ShowStatus("Buscando salas...", new Color(0.85f, 0.92f, 0.75f));
            }

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
        Canvas canvas = UIFactory.CreateCanvas("MainMenuCanvas");
        BuildTitle(canvas.transform);
        BuildCreatePanel(canvas.transform);
        BuildBrowserPanel(canvas.transform);

        _statusText = UIFactory.CreateText(canvas.transform, string.Empty, 20, TextAnchor.MiddleCenter, new Color(1f, 0.65f, 0.65f));
        _statusText.fontStyle = FontStyle.Bold;
        _statusText.raycastTarget = false;
        var statusOutline = _statusText.gameObject.AddComponent<Outline>();
        statusOutline.effectColor = new Color(0.05f, 0.09f, 0.04f, 0.95f);
        statusOutline.effectDistance = new Vector2(1.5f, -1.5f);
        UIFactory.SetRect(_statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -616f), new Vector2(-40f, -586f));

        Text credits = UIFactory.CreateText(canvas.transform, "Thiago Lima  ·  Santino Jorge  ·  Guadalupe Muiños  ·  Facundo Batastini", 13,
            TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.85f));
        credits.raycastTarget = false;
        var creditsShadow = credits.gameObject.AddComponent<Shadow>();
        creditsShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        creditsShadow.effectDistance = new Vector2(1f, -1f);
        UIFactory.SetRect(credits.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(20f, 6f), new Vector2(-20f, 28f));

        RefreshHint();
        ShowRooms(new List<RoomRow>());
    }

    private void BuildTitle(Transform parent)
    {
        var anchor = new Vector2(0.5f, 1f);

        Sprite emblem = Resources.Load<Sprite>("Menu/BananaCupEmblem");
        if (emblem != null)
        {
            Image icon = UIStyle.CreateSprite(parent, "Emblem", emblem, Color.white);
            UIFactory.SetRect(icon.rectTransform, anchor, anchor, new Vector2(-470f, -190f), new Vector2(-290f, -10f));
            var bob = icon.gameObject.AddComponent<UiBob>();
            bob.PositionAmplitude = new Vector2(0f, 7f);
            bob.RotationAmplitude = 3f;
        }

        Text title = UIStyle.CreateHeading(parent, "BANANA RUSH", 120, UIStyle.Banana, UIStyle.Brown);
        UIFactory.SetRect(title.rectTransform, anchor, anchor, new Vector2(-270f, -150f), new Vector2(440f, -12f));
        title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
        var titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        titleShadow.effectDistance = new Vector2(0f, -9f);

        Text subtitle = UIStyle.CreateHeading(parent, "LA COPA BANANA DE LA JUNGLA", 30, Color.white, new Color(0.10f, 0.22f, 0.08f));
        UIFactory.SetRect(subtitle.rectTransform, anchor, anchor, new Vector2(-270f, -192f), new Vector2(440f, -148f));
        subtitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
    }

    private void BuildCreatePanel(Transform parent)
    {
        var anchor = new Vector2(0.5f, 1f);
        UIStyle.Frame frame = UIStyle.CreateFrame(parent, "CreatePanel", UIStyle.Banana, UIStyle.PanelFill, 22, 4f);
        UIFactory.SetRect(frame.Border.rectTransform, anchor, anchor, new Vector2(-570f, -572f), new Vector2(-120f, -208f));
        Transform content = frame.Border.transform;

        AddPanelHeader(content, "JUGAR", 0f);

        AddFieldLabel(content, "TU NOMBRE", -64f);
        _nicknameInput = UIStyle.CreateInputField(content, "ej: Tomi");
        _nicknameInput.characterLimit = 14;
        UIFactory.SetRect(_nicknameInput.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -122), new Vector2(-22, -84));

        AddFieldLabel(content, "NOMBRE DE LA SALA", -130f);
        _sessionNameInput = UIStyle.CreateInputField(content, "ej: Jungla1");
        _sessionNameInput.characterLimit = 20;
        UIFactory.SetRect(_sessionNameInput.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -188), new Vector2(-22, -150));

        AddFieldLabel(content, "MINIJUEGO INICIAL", -196f);
        _miniGameDropdown = UIStyle.CreateDropdown(content, MiniGameNames.DisplayNames);
        UIFactory.SetRect(_miniGameDropdown.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -254), new Vector2(-22, -216));
        _miniGameDropdown.onValueChanged.AddListener(_ => RefreshHint());

        _hintText = UIStyle.CreateLabel(content, MiniGameNames.Hint(MiniGameId.BananaRain), 14, UIStyle.Muted, TextAnchor.UpperLeft);
        UIFactory.SetRect(_hintText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -292), new Vector2(-24, -258));

        _createButton = UIStyle.CreateButton(content, "CREAR SALA", UIStyle.Go, 28, 14, 5f);
        UIFactory.SetRect(_createButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(22, 18), new Vector2(-6, 66));
        _createButton.onClick.AddListener(HandleCreateClicked);

        _joinTypedButton = UIStyle.CreateButton(content, "UNIRSE POR NOMBRE", UIStyle.Info, 24, 14, 5f);
        UIFactory.SetRect(_joinTypedButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(6, 18), new Vector2(-22, 66));
        _joinTypedButton.onClick.AddListener(HandleJoinTypedClicked);
    }

    private void BuildBrowserPanel(Transform parent)
    {
        var anchor = new Vector2(0.5f, 1f);
        UIStyle.Frame frame = UIStyle.CreateFrame(parent, "BrowserPanel", UIStyle.Banana, UIStyle.PanelFill, 22, 4f);
        UIFactory.SetRect(frame.Border.rectTransform, anchor, anchor, new Vector2(-90f, -572f), new Vector2(570f, -208f));
        Transform content = frame.Border.transform;

        AddPanelHeader(content, "SALAS ABIERTAS", 0f);

        _refreshButton = UIStyle.CreateButton(content, "ACTUALIZAR", UIStyle.Info, 20, 10, 4f);
        UIFactory.SetRect(_refreshButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -48), new Vector2(-14, -12));
        _refreshButton.onClick.AddListener(HandleRefreshClicked);

        Image listBackground = UIStyle.CreateImage(content, "List", new Color(0.02f, 0.06f, 0.04f, 0.75f), 14);
        UIFactory.SetRect(listBackground.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(16, 16), new Vector2(-16, -62));

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(listBackground.transform, false);
        UIFactory.SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(4, 6), new Vector2(-4, -6));

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewport.transform, false);
        _sessionListContent = contentGO.GetComponent<RectTransform>();
        _sessionListContent.anchorMin = new Vector2(0f, 1f);
        _sessionListContent.anchorMax = new Vector2(1f, 1f);
        _sessionListContent.pivot = new Vector2(0.5f, 1f);
        _sessionListContent.anchoredPosition = Vector2.zero;
        _sessionListContent.sizeDelta = new Vector2(0f, 10f);

        var scroll = listBackground.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = _sessionListContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
    }

    private static void AddPanelHeader(Transform content, string title, float topOffset)
    {
        Image tab = UIStyle.CreateImage(content, "Header", UIStyle.Banana, 14);
        tab.raycastTarget = false;
        UIFactory.SetRect(tab.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -52 + topOffset), new Vector2(-8, -8 + topOffset));

        Text text = UIStyle.CreateHeading(tab.transform, title, 32, UIStyle.Brown, new Color(1f, 0.93f, 0.6f, 0.0f), TextAnchor.MiddleLeft);
        text.GetComponent<Outline>().enabled = false;
        UIFactory.SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-170, 0));
    }

    private static void AddFieldLabel(Transform content, string label, float top)
    {
        Text text = UIStyle.CreateLabel(content, label, 13, UIStyle.Muted, TextAnchor.LowerLeft, true);
        UIFactory.SetRect(text.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, top - 20f), new Vector2(-24, top));
    }

    private void RefreshHint()
    {
        _hintText.text = MiniGameNames.Hint(MiniGameNames.Clamp(_miniGameDropdown.value));
    }

    private void ShowStatus(string message, Color color)
    {
        if (_statusText == null)
        {
            return;
        }

        _statusText.color = color;
        _statusText.text = message;
    }

    private async void HandleCreateClicked()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler == null)
        {
            ShowStatus("Falta el NetworkRunnerHandler en la escena.", new Color(1f, 0.5f, 0.5f));
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
            ShowStatus("Escribi el nombre de la sala o elegila de la lista.", new Color(1f, 0.55f, 0.5f));
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

        ShowStatus("Actualizando salas...", new Color(0.85f, 0.92f, 0.75f));
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
            ShowStatus(sessions.Count == 0 ? "No hay salas abiertas. Crea una." : $"{sessions.Count} sala(s) encontrada(s).",
                new Color(0.85f, 0.95f, 0.8f));
        }
    }

    public readonly struct RoomRow
    {
        public readonly string Name;
        public readonly int Players;
        public readonly int MaxPlayers;
        public readonly bool Open;
        public readonly MiniGameId Game;

        public RoomRow(string name, int players, int maxPlayers, bool open, MiniGameId game)
        {
            Name = name;
            Players = players;
            MaxPlayers = maxPlayers;
            Open = open;
            Game = game;
        }
    }

    private void RenderSessionList(List<SessionInfo> sessions)
    {
        var rooms = new List<RoomRow>();
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

            rooms.Add(new RoomRow(info.Name, info.PlayerCount, info.MaxPlayers, info.IsOpen, game));
        }

        ShowRooms(rooms);
    }

    public void ShowRooms(List<RoomRow> rooms)
    {
        foreach (var row in _sessionRows)
        {
            if (row != null)
            {
                Destroy(row);
            }
        }

        _sessionRows.Clear();

        for (int i = 0; i < rooms.Count; i++)
        {
            _sessionRows.Add(CreateSessionRow(rooms[i], i));
        }

        int slots = rooms.Count;
        if (rooms.Count == 0)
        {
            Text empty = UIStyle.CreateLabel(_sessionListContent, "No hay partidas en este momento.\nCrea una sala y pasale el nombre a tus amigos.", 17,
                UIStyle.Muted, TextAnchor.MiddleCenter, true);
            UIFactory.SetRect(empty.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -140), new Vector2(-16, -40));
            _sessionRows.Add(empty.gameObject);
            slots = 2;
        }

        _sessionListContent.sizeDelta = new Vector2(0f, slots * (RowHeight + RowGap) + RowGap);
    }

    private GameObject CreateSessionRow(RoomRow room, int index)
    {
        bool full = room.Players >= room.MaxPlayers;
        bool canJoin = room.Open && !full;
        string caption = !room.Open ? "EN PARTIDA" : full ? "LLENA" : "UNIRSE";
        Color color = canJoin ? new Color(0.16f, 0.36f, 0.22f, 1f) : new Color(0.20f, 0.24f, 0.22f, 1f);

        Button button = UIStyle.CreateButton(_sessionListContent, room.Name, color, 26, 12, 4f);
        float top = -RowGap - index * (RowHeight + RowGap);
        UIFactory.SetRect(button.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(6, top - RowHeight), new Vector2(-6, top));
        button.interactable = canJoin;

        string sessionName = room.Name;
        button.onClick.AddListener(() => HandleJoinSession(sessionName));

        Text nameText = button.GetComponentInChildren<Text>();
        nameText.supportRichText = false;
        nameText.alignment = TextAnchor.UpperLeft;
        nameText.resizeTextMaxSize = 28;
        UIFactory.SetRect(nameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-190, -6));

        Transform face = button.targetGraphic.transform;
        Text gameText = UIStyle.CreateLabel(face, MiniGameNames.Display(room.Game), 15, UIStyle.Muted, TextAnchor.LowerLeft, true);
        UIFactory.SetRect(gameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-190, -6));
        gameText.verticalOverflow = VerticalWrapMode.Truncate;

        Text count = UIStyle.CreateHeading(face, $"{room.Players}/{room.MaxPlayers}", 32, canJoin ? UIStyle.Banana : UIStyle.Muted, UIStyle.Brown);
        UIFactory.SetRect(count.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-176, 22), new Vector2(-12, -2));

        Text state = UIStyle.CreateLabel(face, caption, 14, canJoin ? new Color(0.7f, 1f, 0.7f) : UIStyle.Muted, TextAnchor.MiddleCenter, true);
        UIFactory.SetRect(state.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-176, 2), new Vector2(-12, -36));

        return button.gameObject;
    }

    private void SetBusy(bool busy, string message)
    {
        _createButton.interactable = !busy;
        _joinTypedButton.interactable = !busy;
        _refreshButton.interactable = !busy;
        if (!string.IsNullOrEmpty(message))
        {
            ShowStatus(message, new Color(0.85f, 0.92f, 1f));
        }
    }

    private void HandleConnectionFailed(string reason)
    {
        if (_statusText == null)
        {
            return;
        }

        ShowStatus($"No se pudo conectar: {reason}", new Color(1f, 0.55f, 0.5f));
        SetBusy(false, null);
    }
}
