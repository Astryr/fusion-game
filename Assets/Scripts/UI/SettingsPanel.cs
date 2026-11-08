using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    private const float VolumeStep = 0.1f;

    private Text _musicValue;
    private Text _sfxValue;
    private Button _diagnosticsButton;

    public static SettingsPanel Create(Transform parent)
    {
        var go = new GameObject("SettingsPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        UIFactory.SetRect(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var panel = go.AddComponent<SettingsPanel>();
        panel.Build();
        go.SetActive(false);
        return panel;
    }

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    private void Build()
    {
        Image dim = UIStyle.CreateImage(transform, "Dim", new Color(0.01f, 0.04f, 0.02f, 0.72f), 2);
        dim.sprite = null;
        dim.type = Image.Type.Simple;
        UIFactory.SetRect(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var center = new Vector2(0.5f, 0.5f);
        UIStyle.Frame frame = UIStyle.CreateFrame(transform, "Frame", UIStyle.Banana, UIStyle.PanelFill, 22, 4f);
        UIFactory.SetRect(frame.Border.rectTransform, center, center, new Vector2(-330f, -270f), new Vector2(330f, 270f));
        Transform content = frame.Border.transform;

        Image tab = UIStyle.CreateImage(content, "Header", UIStyle.Banana, 14);
        tab.raycastTarget = false;
        UIFactory.SetRect(tab.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -64), new Vector2(-8, -8));

        Text title = UIStyle.CreateHeading(tab.transform, "AJUSTES", 38, UIStyle.Brown, new Color(1f, 0.93f, 0.6f, 0f), TextAnchor.MiddleLeft);
        title.GetComponent<Outline>().enabled = false;
        UIFactory.SetRect(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-18, 0));

        AddSectionLabel(content, "SONIDO", -76f);
        _musicValue = BuildVolumeRow(content, "MÚSICA", -108f, delta => ChangeMusic(delta));
        _sfxValue = BuildVolumeRow(content, "EFECTOS", -164f, delta => ChangeSfx(delta));

        AddSectionLabel(content, "CONTROLES", -234f);
        Text controls = UIStyle.CreateLabel(content,
            "A / D o flechas: moverse\nESPACIO: saltar y acción\nENTER: abrir el chat\nF3: panel de red\nESC: cerrar este panel", 19,
            Color.white, TextAnchor.UpperLeft, true);
        UIFactory.SetRect(controls.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(34, -376), new Vector2(-34, -258));

        AddSectionLabel(content, "PANEL DE RED (F3)", -392f);
        _diagnosticsButton = UIStyle.CreateButton(content, "VISIBLE: NO", UIStyle.Info, 20, 10, 4f);
        UIFactory.SetRect(_diagnosticsButton.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -460), new Vector2(214, -418));
        _diagnosticsButton.onClick.AddListener(ToggleDiagnostics);

        Button close = UIStyle.CreateButton(content, "CERRAR", UIStyle.Neutral, 24, 12, 5f);
        UIFactory.SetRect(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120f, 16f), new Vector2(120f, 66f));
        close.onClick.AddListener(Close);
    }

    private static void AddSectionLabel(Transform content, string text, float top)
    {
        Text label = UIStyle.CreateLabel(content, text, 14, UIStyle.Banana, TextAnchor.LowerLeft, true);
        UIFactory.SetRect(label.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(34, top - 22f), new Vector2(-34, top));
    }

    private static Text BuildVolumeRow(Transform content, string label, float top, System.Action<float> change)
    {
        Text name = UIStyle.CreateLabel(content, label, 22, Color.white, TextAnchor.MiddleLeft, true);
        UIFactory.SetRect(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, top - 44f), new Vector2(210, top));

        Button minus = UIStyle.CreateButton(content, "-", UIStyle.Neutral, 30, 10, 4f);
        UIFactory.SetRect(minus.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(250, top - 44f), new Vector2(310, top));
        minus.onClick.AddListener(() => change(-VolumeStep));

        Text value = UIStyle.CreateHeading(content, "50%", 30, Color.white, UIStyle.Brown);
        UIFactory.SetRect(value.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(316, top - 44f), new Vector2(424, top));

        Button plus = UIStyle.CreateButton(content, "+", UIStyle.Go, 30, 10, 4f);
        UIFactory.SetRect(plus.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(430, top - 44f), new Vector2(490, top));
        plus.onClick.AddListener(() => change(VolumeStep));

        return value;
    }

    private void Refresh()
    {
        _musicValue.text = Percent(GameAudio.MusicVolume);
        _sfxValue.text = Percent(GameSfx.Volume);

        NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
        _diagnosticsButton.gameObject.SetActive(handler != null);
        if (handler != null)
        {
            UIStyle.SetButtonLabel(_diagnosticsButton, handler.ShowDiagnostics ? "VISIBLE: SI" : "VISIBLE: NO");
        }
    }

    private void ChangeMusic(float delta)
    {
        GameAudio.MusicVolume = Mathf.Round((GameAudio.MusicVolume + delta) * 10f) / 10f;
        _musicValue.text = Percent(GameAudio.MusicVolume);
    }

    private void ChangeSfx(float delta)
    {
        GameSfx.Volume = Mathf.Round((GameSfx.Volume + delta) * 10f) / 10f;
        _sfxValue.text = Percent(GameSfx.Volume);
        GameSfx.Play(Sfx.Pickup);
    }

    private void ToggleDiagnostics()
    {
        NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
        if (handler == null)
        {
            return;
        }

        handler.ShowDiagnostics = !handler.ShowDiagnostics;
        Refresh();
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }
}
