using System;
using UnityEngine;
using UnityEngine.UI;

public struct LobbySlot
{
    public bool Occupied;
    public string Name;
    public Color Tint;
    public bool IsHost;
    public bool IsLocal;
    public bool IsReady;
    public int CupScore;
}

public sealed class LobbyData
{
    public string SessionName = "-";
    public int ConnectedCount;
    public int SelectedGame;
    public string Hint = string.Empty;
    public bool IsMaster;
    public bool SoloOn;
    public bool LocalReady;
    public string StatusMain = string.Empty;
    public string StatusSub = string.Empty;
    public readonly LobbySlot[] Slots = new LobbySlot[MiniGameNames.MaxPlayers];
}

public class LobbyScreen : MonoBehaviour
{
    private const float HeaderHeight = 56f;
    private const float CardWidth = 270f;
    private const float CardHeight = 294f;
    private const float CardGap = 20f;
    private const float CardTop = 146f;

    private static readonly Color CardFill = new Color(0.04f, 0.10f, 0.07f, 0.95f);
    private static readonly Color EmptyBorder = new Color(0.45f, 0.58f, 0.50f, 0.45f);
    private static readonly Color EmptyFill = new Color(0.04f, 0.09f, 0.06f, 0.55f);
    private static readonly Color ReadyColor = new Color(0.70f, 1f, 0.25f, 1f);
    private static readonly Color ReadyOutline = new Color(0.10f, 0.28f, 0.05f, 1f);
    private static readonly Color WaitingColor = new Color(1f, 0.34f, 0.28f, 1f);
    private static readonly Color WaitingOutline = new Color(0.30f, 0.05f, 0.03f, 1f);
    private static readonly Color TextOutline = new Color(0.04f, 0.06f, 0.04f, 0.9f);

    private sealed class Card
    {
        public RectTransform Root;
        public Image Glow;
        public UIStyle.Frame Frame;
        public Image Crown;
        public Text Name;
        public Image Disc;
        public Image PortraitShadow;
        public Image Portrait;
        public RectTransform PortraitRect;
        public Text Cup;
        public Text Status;
        public Text Waiting;
        public Button ReadyButton;
        public bool Occupied;
        public bool Ready;
    }

    public event Action ReadyClicked;
    public event Action LeaveClicked;
    public event Action SoloClicked;
    public event Action<int> GameChosen;
    public event Action<int> GameCycled;

    private readonly Card[] _cards = new Card[MiniGameNames.MaxPlayers];
    private Text _sessionText;
    private Text _hintText;
    private Dropdown _gameDropdown;
    private Button _soloButton;
    private Text _statusMain;
    private Text _statusSub;
    private Sprite _portraitSprite;
    private string _cupHex;
    private bool _built;

    public static LobbyScreen Create(Transform parent)
    {
        var go = new GameObject("LobbyScreen", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        UIFactory.SetRect(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var screen = go.AddComponent<LobbyScreen>();
        screen.Build();
        return screen;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }
    }

    public void Build()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        _cupHex = ColorUtility.ToHtmlStringRGB(UIStyle.Banana);
        _portraitSprite = LoadPortraitSprite();

        BuildDim();
        BuildHeader();
        BuildGameStrip();
        for (int i = 0; i < _cards.Length; i++)
        {
            _cards[i] = BuildCard(i);
        }

        BuildFooter();
    }

    public void Refresh(LobbyData data)
    {
        if (!_built)
        {
            return;
        }

        _sessionText.text = $"SALA  <color=#{_cupHex}>{UIStyle.Escape(data.SessionName)}</color>      JUGADORES  <color=#{_cupHex}>{data.ConnectedCount}/{MiniGameNames.MaxPlayers}</color>";
        _hintText.text = data.Hint;

        bool dropdownOpen = _gameDropdown.template != null && _gameDropdown.template.gameObject.activeInHierarchy;
        if (!dropdownOpen && data.SelectedGame != _gameDropdown.value)
        {
            _gameDropdown.SetValueWithoutNotify(data.SelectedGame);
            _gameDropdown.RefreshShownValue();
        }

        _soloButton.gameObject.SetActive(data.IsMaster);
        if (data.IsMaster)
        {
            UIStyle.SetButtonLabel(_soloButton, data.SoloOn ? "SOLO: ON" : "PROBAR SOLO");
            UIStyle.SetButtonColor(_soloButton, data.SoloOn ? UIStyle.Go : UIStyle.Warn);
        }

        _statusMain.text = data.StatusMain;
        _statusSub.text = data.StatusSub;

        for (int i = 0; i < _cards.Length; i++)
        {
            ApplySlot(_cards[i], data.Slots[i], data);
        }
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < _cards.Length; i++)
        {
            Card card = _cards[i];
            if (card == null)
            {
                continue;
            }

            if (!card.Occupied)
            {
                int dots = 1 + (int)(t * 2f + i) % 3;
                card.Waiting.text = "ESPERANDO" + new string('.', dots);
                continue;
            }

            float bob = Mathf.Sin(t * 2.2f + i * 1.3f) * 3f;
            float hop = card.Ready ? Mathf.Abs(Mathf.Sin(t * 4.5f + i)) * 9f : 0f;
            card.PortraitRect.anchoredPosition = new Vector2(0f, -98f + bob + hop);

            float pulse = card.Ready ? 0.38f + Mathf.Sin(t * 3f + i) * 0.1f : 0.1f;
            Color glow = card.Glow.color;
            glow.a = pulse;
            card.Glow.color = glow;
        }
    }

    private void BuildDim()
    {
        Color dim = new Color(0.01f, 0.06f, 0.04f, 0.62f);

        Image top = UIStyle.CreateImage(transform, "DimTop", dim, 2);
        top.sprite = null;
        top.type = Image.Type.Simple;
        top.raycastTarget = false;
        UIFactory.SetRect(top.rectTransform, new Vector2(0f, 0.42f), Vector2.one, Vector2.zero, Vector2.zero);

        Image fade = UIStyle.CreateSprite(transform, "DimFade", UIStyle.VerticalFade(), dim, false);
        UIFactory.SetRect(fade.rectTransform, new Vector2(0f, 0.26f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);
    }

    private void BuildHeader()
    {
        Image bar = UIStyle.CreateImage(transform, "Header", new Color(0.02f, 0.07f, 0.05f, 0.94f), 2);
        bar.sprite = null;
        bar.type = Image.Type.Simple;
        bar.raycastTarget = false;
        UIFactory.SetRect(bar.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -HeaderHeight), Vector2.zero);

        Image line = UIStyle.CreateImage(bar.transform, "Line", UIStyle.Banana, 2);
        line.sprite = null;
        line.type = Image.Type.Simple;
        line.raycastTarget = false;
        UIFactory.SetRect(line.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f));

        Sprite emblem = Resources.Load<Sprite>("Menu/BananaCupEmblem");
        if (emblem != null)
        {
            Image icon = UIStyle.CreateSprite(bar.transform, "Emblem", emblem, Color.white);
            UIFactory.SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -24f), new Vector2(66f, 24f));
        }

        Text title = UIStyle.CreateHeading(bar.transform, "LOBBY", 42, UIStyle.Banana, UIStyle.Brown, TextAnchor.MiddleLeft);
        UIFactory.SetRect(title.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(78f, 0f), new Vector2(300f, 0f));

        _sessionText = UIStyle.CreateLabel(bar.transform, string.Empty, 19, Color.white, TextAnchor.MiddleRight, true);
        _sessionText.supportRichText = true;
        _sessionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.SetRect(_sessionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(320f, 0f), new Vector2(-170f, 0f));

        Button leave = UIStyle.CreateButton(bar.transform, "SALIR", UIStyle.Danger, 24, 12, 4f);
        UIFactory.SetRect(leave.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-140f, -19f), new Vector2(-18f, 19f));
        leave.onClick.AddListener(() => LeaveClicked?.Invoke());
    }

    private void BuildGameStrip()
    {
        var anchor = new Vector2(0.5f, 1f);

        Text label = UIStyle.CreateLabel(transform, "MINIJUEGO  (podes cambiarlo ahora)", 13, UIStyle.Muted, TextAnchor.MiddleCenter, true);
        AddTextShadow(label);
        UIFactory.SetRect(label.rectTransform, anchor, anchor, new Vector2(-260f, -80f), new Vector2(260f, -62f));

        _gameDropdown = UIStyle.CreateDropdown(transform, MiniGameNames.DisplayNames);
        UIFactory.SetRect(_gameDropdown.GetComponent<RectTransform>(), anchor, anchor, new Vector2(-190f, -118f), new Vector2(190f, -80f));
        _gameDropdown.onValueChanged.AddListener(index => GameChosen?.Invoke(index));

        Button prev = UIStyle.CreateButton(transform, "<", UIStyle.Neutral, 26, 10, 4f);
        UIFactory.SetRect(prev.GetComponent<RectTransform>(), anchor, anchor, new Vector2(-246f, -118f), new Vector2(-200f, -80f));
        prev.onClick.AddListener(() => GameCycled?.Invoke(-1));

        Button next = UIStyle.CreateButton(transform, ">", UIStyle.Neutral, 26, 10, 4f);
        UIFactory.SetRect(next.GetComponent<RectTransform>(), anchor, anchor, new Vector2(200f, -118f), new Vector2(246f, -80f));
        next.onClick.AddListener(() => GameCycled?.Invoke(1));

        _hintText = UIStyle.CreateLabel(transform, string.Empty, 15, Color.white, TextAnchor.MiddleCenter);
        AddTextShadow(_hintText);
        UIFactory.SetRect(_hintText.rectTransform, anchor, anchor, new Vector2(-470f, -144f), new Vector2(470f, -120f));

        _soloButton = UIStyle.CreateButton(transform, "PROBAR SOLO", UIStyle.Warn, 22, 10, 4f);
        UIFactory.SetRect(_soloButton.GetComponent<RectTransform>(), anchor, anchor, new Vector2(380f, -118f), new Vector2(560f, -80f));
        _soloButton.onClick.AddListener(() => SoloClicked?.Invoke());
    }

    private Card BuildCard(int index)
    {
        var card = new Card();
        float totalWidth = _cards.Length * CardWidth + (_cards.Length - 1) * CardGap;
        float left = -totalWidth * 0.5f + index * (CardWidth + CardGap);
        var anchor = new Vector2(0.5f, 1f);

        var rootGO = new GameObject("Card" + (index + 1), typeof(RectTransform));
        rootGO.transform.SetParent(transform, false);
        card.Root = rootGO.GetComponent<RectTransform>();
        UIFactory.SetRect(card.Root, anchor, anchor, new Vector2(left, -(CardTop + CardHeight)), new Vector2(left + CardWidth, -CardTop));

        card.Glow = UIStyle.CreateSprite(card.Root, "Glow", UIStyle.Glow(), Color.white, false);
        UIFactory.SetRect(card.Glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-46f, -46f), new Vector2(46f, 46f));

        card.Frame = UIStyle.CreateFrame(card.Root, "Frame", EmptyBorder, EmptyFill, 22, 4f);
        UIFactory.SetRect(card.Frame.Border.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        card.Frame.Border.raycastTarget = false;
        card.Frame.Fill.raycastTarget = false;
        Transform content = card.Frame.Border.transform;

        card.Name = UIStyle.CreateHeading(content, string.Empty, 28, Color.white, TextOutline);
        card.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
        card.Name.resizeTextForBestFit = true;
        card.Name.resizeTextMinSize = 14;
        card.Name.resizeTextMaxSize = 28;
        UIFactory.SetRect(card.Name.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -40f), new Vector2(-40f, -8f));

        card.Crown = UIStyle.CreateSprite(content, "Crown", UIStyle.Crown(), UIStyle.Banana);
        UIFactory.SetRect(card.Crown.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -36f), new Vector2(40f, -14f));
        var crownOutline = card.Crown.gameObject.AddComponent<Outline>();
        crownOutline.effectColor = UIStyle.Brown;
        crownOutline.effectDistance = new Vector2(1.5f, -1.5f);

        card.Disc = UIStyle.CreateSprite(content, "Disc", UIStyle.Glow(), Color.white, false);
        UIFactory.SetRect(card.Disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-92f, -176f), new Vector2(92f, -12f));

        card.PortraitShadow = UIStyle.CreateSprite(content, "Shadow", UIStyle.Glow(), new Color(0f, 0f, 0f, 0.5f), false);
        UIFactory.SetRect(card.PortraitShadow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-54f, -158f), new Vector2(54f, -140f));

        card.Portrait = UIStyle.CreateSprite(content, "Portrait", _portraitSprite, Color.white);
        card.PortraitRect = card.Portrait.rectTransform;
        card.PortraitRect.anchorMin = new Vector2(0.5f, 1f);
        card.PortraitRect.anchorMax = new Vector2(0.5f, 1f);
        card.PortraitRect.pivot = new Vector2(0.5f, 0.5f);
        card.PortraitRect.sizeDelta = new Vector2(124f, 124f);
        card.PortraitRect.anchoredPosition = new Vector2(0f, -98f);
        var portraitOutline = card.Portrait.gameObject.AddComponent<Outline>();
        portraitOutline.effectColor = new Color(0.02f, 0.05f, 0.03f, 0.75f);
        portraitOutline.effectDistance = new Vector2(2f, -2f);

        card.Cup = UIStyle.CreateHeading(content, string.Empty, 32, Color.white, TextOutline);
        card.Cup.supportRichText = true;
        UIFactory.SetRect(card.Cup.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -186f), new Vector2(-10f, -152f));

        card.Status = UIStyle.CreateHeading(content, string.Empty, 38, WaitingColor, WaitingOutline);
        UIFactory.SetRect(card.Status.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -232f), new Vector2(-10f, -190f));

        card.ReadyButton = UIStyle.CreateButton(content, "ESTOY LISTO", UIStyle.Go, 26, 12, 5f);
        UIFactory.SetRect(card.ReadyButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -278f), new Vector2(-16f, -240f));
        card.ReadyButton.onClick.AddListener(() => ReadyClicked?.Invoke());

        card.Waiting = UIStyle.CreateHeading(content, "ESPERANDO...", 26, UIStyle.WithAlpha(UIStyle.Muted, 0.8f), TextOutline);
        UIFactory.SetRect(card.Waiting.rectTransform, new Vector2(0f, 0f), Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

        return card;
    }

    private void BuildFooter()
    {
        _statusMain = UIStyle.CreateHeading(transform, string.Empty, 30, Color.white, TextOutline, TextAnchor.LowerCenter);
        _statusMain.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIFactory.SetRect(_statusMain.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(474f, 56f), new Vector2(-30f, 118f));

        _statusSub = UIStyle.CreateLabel(transform, string.Empty, 16, UIStyle.Muted, TextAnchor.UpperCenter, true);
        AddTextShadow(_statusSub);
        UIFactory.SetRect(_statusSub.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(474f, 14f), new Vector2(-30f, 54f));
    }

    private void ApplySlot(Card card, LobbySlot slot, LobbyData data)
    {
        card.Occupied = slot.Occupied;
        card.Ready = slot.Occupied && slot.IsReady;

        card.Waiting.gameObject.SetActive(!slot.Occupied);
        card.Name.gameObject.SetActive(slot.Occupied);
        card.Portrait.gameObject.SetActive(slot.Occupied);
        card.Disc.gameObject.SetActive(slot.Occupied);
        card.PortraitShadow.gameObject.SetActive(slot.Occupied);
        card.Cup.gameObject.SetActive(slot.Occupied);
        card.Status.gameObject.SetActive(slot.Occupied);
        card.Crown.gameObject.SetActive(slot.Occupied && slot.IsHost);
        card.ReadyButton.gameObject.SetActive(slot.Occupied && slot.IsLocal);
        card.Glow.gameObject.SetActive(slot.Occupied);

        if (!slot.Occupied)
        {
            card.Frame.Border.color = EmptyBorder;
            card.Frame.Fill.color = EmptyFill;
            return;
        }

        Color tint = slot.Tint;
        card.Frame.Border.color = slot.IsReady ? tint : UIStyle.Shade(tint, 0.78f);
        card.Frame.Fill.color = CardFill;
        card.Glow.color = new Color(tint.r, tint.g, tint.b, card.Glow.color.a);

        card.Name.text = UIStyle.Escape(slot.Name);
        card.Name.color = Color.Lerp(tint, Color.white, 0.25f);
        card.Portrait.color = Color.Lerp(Color.white, tint, 0.6f);
        card.Disc.color = new Color(tint.r, tint.g, tint.b, 0.55f);
        card.Cup.text = $"<color=#{ColorUtility.ToHtmlStringRGB(UIStyle.Muted)}>COPA</color> <color=#{_cupHex}>{slot.CupScore}</color>";

        card.Status.text = slot.IsReady ? UIStyle.Exclaim("LISTO") : "NO LISTO";
        card.Status.color = slot.IsReady ? ReadyColor : WaitingColor;
        card.Status.GetComponent<Outline>().effectColor = slot.IsReady ? ReadyOutline : WaitingOutline;

        if (slot.IsLocal)
        {
            UIStyle.SetButtonLabel(card.ReadyButton, data.LocalReady ? "CANCELAR" : "ESTOY LISTO");
            UIStyle.SetButtonColor(card.ReadyButton, data.LocalReady ? UIStyle.Warn : UIStyle.Go);
        }
    }

    private static void AddTextShadow(Text text)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private static Sprite LoadPortraitSprite()
    {
        var prefab = Resources.Load<GameObject>("Player");
        if (prefab != null)
        {
            var renderer = prefab.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                return renderer.sprite;
            }
        }

        return null;
    }
}
