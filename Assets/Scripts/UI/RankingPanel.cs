using UnityEngine;
using UnityEngine.UI;

public class RankingPanel : MonoBehaviour
{
    private const int RowCount = CloudConfig.RankingPageSize;
    private const float RowHeight = 34f;
    private const float RowGap = 2f;
    private const float RowsTop = -100f;

    private static readonly Color RowEven = new Color(0.02f, 0.06f, 0.04f, 0.70f);
    private static readonly Color RowOdd = new Color(0.04f, 0.10f, 0.07f, 0.70f);
    private static readonly Color RowMine = new Color(1f, 0.82f, 0.22f, 0.22f);
    private static readonly Color WarnText = new Color(1f, 0.66f, 0.40f, 1f);

    private sealed class Row
    {
        public GameObject Root;
        public Image Back;
        public Text Rank;
        public Text Name;
        public Text Score;
    }

    private readonly Row[] _rows = new Row[RowCount];
    private Text _message;
    private Text _footer;
    private Button _refreshButton;
    private int _ticket;

    public static RankingPanel Create(Transform parent)
    {
        var go = new GameObject("RankingPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        UIFactory.SetRect(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var panel = go.AddComponent<RankingPanel>();
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
        _ticket++;
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
        UIFactory.SetRect(frame.Border.rectTransform, center, center, new Vector2(-380f, -280f), new Vector2(380f, 280f));
        Transform content = frame.Border.transform;

        Image tab = UIStyle.CreateImage(content, "Header", UIStyle.Banana, 14);
        tab.raycastTarget = false;
        UIFactory.SetRect(tab.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -64), new Vector2(-8, -8));

        Text title = UIStyle.CreateHeading(tab.transform, "RANKING GLOBAL", 38, UIStyle.Brown, new Color(1f, 0.93f, 0.6f, 0f), TextAnchor.MiddleLeft);
        title.GetComponent<Outline>().enabled = false;
        UIFactory.SetRect(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-250, 0));

        Text caption = UIStyle.CreateLabel(tab.transform, "Puntos de Copa acumulados", 15, UIStyle.Brown, TextAnchor.MiddleRight, true);
        UIFactory.SetRect(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(300, 0), new Vector2(-16, 0));

        var topLeft = new Vector2(0f, 1f);
        var topRight = new Vector2(1f, 1f);
        AddColumnLabel(content, "PUESTO", topLeft, new Vector2(30f, -98f), new Vector2(110f, -74f), TextAnchor.LowerLeft);
        AddColumnLabel(content, "JUGADOR", topLeft, new Vector2(120f, -98f), new Vector2(400f, -74f), TextAnchor.LowerLeft);
        AddColumnLabel(content, "PUNTOS", topRight, new Vector2(-200f, -98f), new Vector2(-32f, -74f), TextAnchor.LowerRight);

        for (int i = 0; i < RowCount; i++)
        {
            _rows[i] = BuildRow(content, i);
        }

        _message = UIStyle.CreateLabel(content, string.Empty, 20, UIStyle.Muted, TextAnchor.MiddleCenter, true);
        UIFactory.SetRect(_message.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, RowsTop - 340f), new Vector2(-40, RowsTop - 10f));

        _footer = UIStyle.CreateLabel(content, string.Empty, 17, UIStyle.Banana, TextAnchor.MiddleCenter, true);
        UIFactory.SetRect(_footer.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 76), new Vector2(-24, 118));

        _refreshButton = UIStyle.CreateButton(content, "ACTUALIZAR", UIStyle.Info, 24, 12, 5f);
        UIFactory.SetRect(_refreshButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(24, 16), new Vector2(-8, 66));
        _refreshButton.onClick.AddListener(Refresh);

        Button close = UIStyle.CreateButton(content, "CERRAR", UIStyle.Neutral, 24, 12, 5f);
        UIFactory.SetRect(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(8, 16), new Vector2(-24, 66));
        close.onClick.AddListener(Close);
    }

    private static void AddColumnLabel(Transform content, string text, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax, TextAnchor alignment)
    {
        Text label = UIStyle.CreateLabel(content, text, 13, UIStyle.Muted, alignment, true);
        UIFactory.SetRect(label.rectTransform, anchor, anchor, offsetMin, offsetMax);
    }

    private Row BuildRow(Transform content, int index)
    {
        float top = RowsTop - 4f - index * (RowHeight + RowGap);
        var row = new Row();

        row.Back = UIStyle.CreateImage(content, "Row" + index, index % 2 == 0 ? RowEven : RowOdd, 8);
        row.Back.raycastTarget = false;
        row.Root = row.Back.gameObject;
        UIFactory.SetRect(row.Back.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, top - RowHeight), new Vector2(-20, top));

        row.Rank = UIStyle.CreateHeading(row.Back.transform, string.Empty, 26, UIStyle.Banana, UIStyle.Brown, TextAnchor.MiddleLeft);
        UIFactory.SetRect(row.Rank.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-640, 0));

        row.Name = UIStyle.CreateLabel(row.Back.transform, string.Empty, 21, Color.white, TextAnchor.MiddleLeft, true);
        row.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.SetRect(row.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(100, 0), new Vector2(-190, 0));

        row.Score = UIStyle.CreateHeading(row.Back.transform, string.Empty, 26, Color.white, UIStyle.Brown, TextAnchor.MiddleRight);
        UIFactory.SetRect(row.Score.rectTransform, Vector2.zero, Vector2.one, new Vector2(520, 0), new Vector2(-12, 0));

        row.Root.SetActive(false);
        return row;
    }

    private async void Refresh()
    {
        int ticket = ++_ticket;
        HideRows();
        _footer.text = string.Empty;
        if (!LiveConfig.LeaderboardEnabled)
        {
            SetMessage("El ranking global está en mantenimiento.\nVolvé a probar más tarde.", WarnText);
            _refreshButton.interactable = true;
            return;
        }

        _refreshButton.interactable = false;
        SetMessage("Cargando ranking...", UIStyle.Muted);

        RankingResult result = await RankingService.FetchAsync();
        if (this == null || ticket != _ticket)
        {
            return;
        }

        _refreshButton.interactable = true;
        ShowResult(result);
    }

    private void ShowResult(RankingResult result)
    {
        string pending = result.PendingPoints > 0 ? $"   ·   {result.PendingPoints} pts esperan para enviarse" : string.Empty;

        if (!result.Ok)
        {
            HideRows();
            SetMessage($"No se pudo cargar el ranking.\n{result.Error}\nRevisá tu conexión y tocá ACTUALIZAR.", WarnText);
            _footer.text = result.PendingPoints > 0 ? $"{result.PendingPoints} pts tuyos esperan para enviarse al ranking" : string.Empty;
            return;
        }

        if (result.Entries.Count == 0)
        {
            HideRows();
            SetMessage("Todavía no hay puntajes.\nJugá una partida de 2 o más jugadores para abrir el ranking.", UIStyle.Muted);
            _footer.text = result.PendingPoints > 0 ? $"{result.PendingPoints} pts tuyos esperan para enviarse al ranking" : string.Empty;
            return;
        }

        _message.gameObject.SetActive(false);
        for (int i = 0; i < _rows.Length; i++)
        {
            Row row = _rows[i];
            if (i >= result.Entries.Count)
            {
                row.Root.SetActive(false);
                continue;
            }

            RankingEntry entry = result.Entries[i];
            row.Root.SetActive(true);
            row.Back.color = entry.IsMe ? RowMine : (i % 2 == 0 ? RowEven : RowOdd);
            row.Rank.text = $"#{entry.Rank}";
            row.Name.text = entry.Name + (entry.IsMe ? "  (vos)" : string.Empty);
            row.Name.color = entry.IsMe ? UIStyle.Banana : Color.white;
            row.Score.text = entry.Score.ToString();
            row.Score.color = entry.IsMe ? UIStyle.Banana : Color.white;
        }

        _footer.text = (result.HasMine
            ? $"Tu puesto: #{result.Mine.Rank} con {result.Mine.Score} pts"
            : "Todavía no sumaste puntos en el ranking") + pending;
    }

    private void HideRows()
    {
        for (int i = 0; i < _rows.Length; i++)
        {
            _rows[i].Root.SetActive(false);
        }
    }

    private void SetMessage(string text, Color color)
    {
        _message.gameObject.SetActive(true);
        _message.text = text;
        _message.color = color;
    }
}
