using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ChatController : MonoBehaviour
{
    private const int LogLines = 8;
    private const float FadeDelay = 9f;
    private const float FadeDuration = 1.5f;
    private const string HintText = "ENTER para chatear";
    private const string PlaceholderText = "Escribi tu mensaje...   ENTER envia · ESC cancela";

    private Canvas _canvas;
    private CanvasGroup _panelGroup;
    private Image _background;
    private Text _log;
    private GameObject _inputRow;
    private Text _inputText;
    private Text _placeholder;
    private Text _hint;

    private readonly StringBuilder _logBuilder = new StringBuilder(1024);
    private readonly StringBuilder _buffer = new StringBuilder(GameChat.MaxLength);
    private bool _typing;
    private bool _logDirty = true;
    private float _lastSentAt = -10f;
    private float _lastActivityAt = -100f;

    public bool IsTyping => _typing;

    public void Build()
    {
        if (_canvas != null)
        {
            return;
        }

        _canvas = UIFactory.CreateCanvas("ChatCanvas");
        _canvas.sortingOrder = 60;
        _canvas.transform.SetParent(transform, false);

        var root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(_canvas.transform, false);
        UIFactory.SetRect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, new Vector2(14f, 10f), new Vector2(414f, 186f));

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup));
        panel.transform.SetParent(root.transform, false);
        UIFactory.SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _panelGroup = panel.GetComponent<CanvasGroup>();
        _panelGroup.blocksRaycasts = false;
        _panelGroup.interactable = false;

        _background = UIStyle.CreateImage(panel.transform, "Background", new Color(0.02f, 0.06f, 0.04f, 0.4f), 12);
        _background.raycastTarget = false;
        UIFactory.SetRect(_background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var logMask = new GameObject("LogMask", typeof(RectTransform), typeof(RectMask2D));
        logMask.transform.SetParent(panel.transform, false);
        UIFactory.SetRect(logMask.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(10f, 40f), new Vector2(-10f, -8f));

        _log = UIFactory.CreateText(logMask.transform, string.Empty, 17, TextAnchor.LowerLeft, Color.white);
        _log.supportRichText = true;
        _log.raycastTarget = false;
        _log.horizontalOverflow = HorizontalWrapMode.Wrap;
        _log.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = _log.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1f, -1f);

        Image inputBackground = UIStyle.CreateImage(panel.transform, "InputRow", UIStyle.FieldFill, 9);
        inputBackground.raycastTarget = false;
        _inputRow = inputBackground.gameObject;
        UIFactory.SetRect(inputBackground.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 7f), new Vector2(-8f, 35f));

        var inputMask = new GameObject("InputMask", typeof(RectTransform), typeof(RectMask2D));
        inputMask.transform.SetParent(_inputRow.transform, false);
        UIFactory.SetRect(inputMask.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

        _inputText = UIFactory.CreateText(inputMask.transform, string.Empty, 17, TextAnchor.MiddleLeft, UIStyle.FieldText);
        _inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _inputText.verticalOverflow = VerticalWrapMode.Overflow;
        _inputText.supportRichText = false;
        _inputText.raycastTarget = false;

        _placeholder = UIFactory.CreateText(inputMask.transform, PlaceholderText, 14, TextAnchor.MiddleLeft, UIStyle.WithAlpha(UIStyle.FieldText, 0.45f));
        _placeholder.fontStyle = FontStyle.Italic;
        _placeholder.raycastTarget = false;
        _placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;

        _hint = UIFactory.CreateText(root.transform, HintText, 14, TextAnchor.MiddleLeft, UIStyle.WithAlpha(UIStyle.Muted, 0.85f));
        _hint.raycastTarget = false;
        _hint.fontStyle = FontStyle.Bold;
        UIFactory.SetRect(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 7f), new Vector2(-14f, 35f));
        var hintShadow = _hint.gameObject.AddComponent<Shadow>();
        hintShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        hintShadow.effectDistance = new Vector2(1f, -1f);

        _inputRow.SetActive(false);
        _logDirty = true;
    }

    private void OnEnable()
    {
        GameChat.MessageReceived += HandleMessage;
        _logDirty = true;
    }

    private void OnDisable()
    {
        GameChat.MessageReceived -= HandleMessage;
        SetTyping(false);
    }

    private void Update()
    {
        if (_canvas == null)
        {
            return;
        }

        if (_typing)
        {
            HandleTyping();
        }
        else if (EnterPressed() && CanChat())
        {
            SetTyping(true);
        }

        if (_logDirty)
        {
            RebuildLog();
        }

        ApplyVisibility();
    }

    private static bool EnterPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool CanChat()
    {
        PlayerController local = PlayerController.Local;
        return local != null && local.IsSpawned;
    }

    private void HandleTyping()
    {
        if (!CanChat() || Input.GetKeyDown(KeyCode.Escape))
        {
            SetTyping(false);
            return;
        }

        if (EnterPressed())
        {
            Submit();
            return;
        }

        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (control && Input.GetKeyDown(KeyCode.V))
        {
            AppendPasted(GUIUtility.systemCopyBuffer);
        }

        foreach (char c in Input.inputString)
        {
            if (c == '\b')
            {
                if (_buffer.Length > 0)
                {
                    _buffer.Length--;
                }
            }
            else
            {
                AppendTyped(c);
            }
        }

        RefreshInput();
    }

    private void AppendTyped(char c)
    {
        if (_buffer.Length >= GameChat.MaxLength || char.IsControl(c) || char.IsSurrogate(c))
        {
            return;
        }

        if (c == ' ')
        {
            if (_buffer.Length > 0 && _buffer[_buffer.Length - 1] != ' ')
            {
                _buffer.Append(' ');
            }

            return;
        }

        _buffer.Append(c == '<' ? '\u2039' : c == '>' ? '\u203A' : c);
    }

    private void AppendPasted(string raw)
    {
        string clean = GameChat.Sanitize(raw);
        int room = GameChat.MaxLength - _buffer.Length;
        if (clean.Length == 0 || room <= 0)
        {
            return;
        }

        _buffer.Append(clean.Length > room ? clean.Substring(0, room) : clean);
    }

    private void Submit()
    {
        string text = GameChat.Sanitize(_buffer.ToString());
        if (text.Length == 0)
        {
            SetTyping(false);
            return;
        }

        if (Time.unscaledTime - _lastSentAt < GameChat.SendCooldown)
        {
            return;
        }

        _lastSentAt = Time.unscaledTime;
        PlayerController.Local.SendChat(text);
        SetTyping(false);
    }

    private void SetTyping(bool typing)
    {
        _typing = typing;
        GameChat.IsTyping = typing;
        _buffer.Clear();
        _lastActivityAt = Time.unscaledTime;
        if (_inputRow != null)
        {
            _inputRow.SetActive(typing);
        }

        if (_hint != null)
        {
            _hint.gameObject.SetActive(!typing);
        }

        if (typing)
        {
            RefreshInput();
        }
    }

    private void RefreshInput()
    {
        if (_inputText == null)
        {
            return;
        }

        bool caret = ((int)(Time.unscaledTime * 2f)) % 2 == 0;
        _inputText.text = _buffer.ToString() + (caret ? "|" : " ");
        _placeholder.gameObject.SetActive(_buffer.Length == 0);

        float available = _inputText.rectTransform.rect.width;
        _inputText.alignment = _inputText.preferredWidth > available ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
    }

    private void HandleMessage(ChatEntry entry)
    {
        _lastActivityAt = Time.unscaledTime;
        _logDirty = true;
    }

    private void RebuildLog()
    {
        _logDirty = false;
        _logBuilder.Clear();

        var entries = GameChat.Entries;
        int start = Mathf.Max(0, entries.Count - LogLines);
        for (int i = start; i < entries.Count; i++)
        {
            ChatEntry entry = entries[i];
            if (_logBuilder.Length > 0)
            {
                _logBuilder.Append('\n');
            }

            _logBuilder.Append("<b><color=#")
                .Append(ColorUtility.ToHtmlStringRGB(entry.Color))
                .Append('>')
                .Append(entry.Sender)
                .Append("</color></b>  ")
                .Append(entry.Text);
        }

        _log.text = _logBuilder.ToString();
    }

    private void ApplyVisibility()
    {
        BananaGameManager manager = BananaGameManager.Instance;
        bool inLobby = manager != null && manager.IsInLobby;

        float alpha = 1f;
        if (!_typing && !inLobby)
        {
            float idle = Time.unscaledTime - _lastActivityAt;
            alpha = idle < FadeDelay ? 1f : Mathf.Clamp01(1f - (idle - FadeDelay) / FadeDuration);
        }

        _panelGroup.alpha = alpha;
        _background.color = new Color(0.02f, 0.06f, 0.04f, _typing ? 0.72f : 0.4f);

        if (_typing)
        {
            RefreshInput();
        }
    }
}
