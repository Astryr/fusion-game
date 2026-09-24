using UnityEngine;
using UnityEngine.UI;

public class MemoryPuzzleMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.MemoryPuzzle;

    public const int Columns = 6;
    public const int Rows = 3;
    public const int CellCount = Columns * Rows;
    public const int PairCount = CellCount / 2;
    public const float PeekSeconds = 7f;
    public const float MismatchSeconds = 0.85f;

    private BananaGameManager _director;
    private Canvas _canvas;
    private Text _status;
    private readonly Image[] _cells = new Image[CellCount];
    private readonly int[] _layout = new int[CellCount];
    private readonly bool[] _locked = new bool[CellCount];
    private Sprite[] _pieceSprites;
    private Color[] _pieceTints;
    private bool _playing;
    private bool _localFinished;
    private int _flipA = -1;
    private int _flipB = -1;
    private float _mismatchUntil = -1f;
    private int _pairs;

    public void Setup(BananaGameManager director)
    {
        _director = director;
    }

    public void OnCountdownStarted() { }

    public void OnMatchStarted()
    {
        _playing = true;
        _localFinished = false;
        _pairs = 0;
        _flipA = -1;
        _flipB = -1;
        _mismatchUntil = -1f;
        LoadSprites();
        BuildLayout();
        BuildUI();
        RefreshCells();

        foreach (var player in _director.GetPlayers())
        {
            player.SetControlMode(PlayerControlMode.Disabled);
        }
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!_playing)
        {
            return;
        }

        if (_mismatchUntil > 0f && Time.unscaledTime >= _mismatchUntil)
        {
            _flipA = -1;
            _flipB = -1;
            _mismatchUntil = -1f;
        }

        if (hasAuthority)
        {
            foreach (var player in _director.GetPlayers())
            {
                if (player.IsSpawned && player.Score >= PairCount)
                {
                    _director.DeclareWinner(player, string.Empty);
                    return;
                }
            }
        }

        RefreshCells();
        RefreshStatus();
    }

    public void OnMatchEnded()
    {
        _playing = false;
    }

    public void Cleanup()
    {
        _playing = false;
        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
            _canvas = null;
        }
    }

    private void BuildLayout()
    {
        int playerId = PlayerController.Local != null && PlayerController.Local.IsSpawned
            ? PlayerController.Local.Object.InputAuthority.PlayerId
            : 0;
        int seed = _director.RoundSeed + playerId * 31 + 7;
        for (int i = 0; i < CellCount; i++)
        {
            _layout[i] = i / 2;
            _locked[i] = false;
        }

        var rng = new System.Random(seed);
        for (int i = CellCount - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_layout[i], _layout[j]) = (_layout[j], _layout[i]);
        }
    }

    private void LoadSprites()
    {
        Sprite banana = SpriteFromPrefab("Banana");
        _pieceSprites = new[]
        {
            banana,
            SpriteFromPrefab("BananaExplosiva"),
            Resources.Load<Sprite>("Memotest/MonikoBoca"),
            Resources.Load<Sprite>("Memotest/Cazador"),
            Resources.Load<Sprite>("Memotest/CazadorAzul"),
            Resources.Load<Sprite>("Memotest/BananaEspada"),
            banana,
            banana,
            banana,
        };
        _pieceTints = new[]
        {
            Color.white,
            Color.white,
            Color.white,
            Color.white,
            Color.white,
            Color.white,
            new Color(0.25f, 0.85f, 0.30f, 1f),
            new Color(0.95f, 0.20f, 0.18f, 1f),
            new Color(0.25f, 0.45f, 1f, 1f),
        };
    }

    private void BuildUI()
    {
        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
        }

        _canvas = UIFactory.CreateCanvas("PuzzleCanvas");
        var panel = UIFactory.CreatePanel(_canvas.transform, "PuzzlePanel", new Color(0.08f, 0.09f, 0.12f, 0.82f),
            new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(-360, -200), new Vector2(360, 210));

        _status = UIFactory.CreateText(panel, "Memotest", 16, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
        UIFactory.SetRect(_status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, 8), new Vector2(-8, 44));

        const float pad = 16f;
        float cellW = (720f - pad * 2f) / Columns;
        float cellH = (366f - pad * 2f) / Rows;
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                int index = y * Columns + x;
                var button = UIFactory.CreateButton(panel, string.Empty, new Color(0.18f, 0.2f, 0.24f));
                var rect = button.GetComponent<RectTransform>();
                float left = pad + x * cellW;
                float bottom = pad + (Rows - 1 - y) * cellH;
                UIFactory.SetRect(rect,
                    new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(left, bottom),
                    new Vector2(left + cellW - 8f, bottom + cellH - 8f));

                _cells[index] = button.GetComponent<Image>();
                int captured = index;
                button.onClick.AddListener(() => HandleCellClicked(captured));
            }
        }
    }

    private void HandleCellClicked(int index)
    {
        if (!_playing || _localFinished || _director == null || _director.MatchTime < PeekSeconds)
        {
            return;
        }

        if (_mismatchUntil > 0f || _locked[index] || index == _flipA || index == _flipB)
        {
            return;
        }

        if (_flipA < 0)
        {
            _flipA = index;
            RefreshCells();
            return;
        }

        _flipB = index;
        if (_layout[_flipA] == _layout[_flipB])
        {
            _locked[_flipA] = true;
            _locked[_flipB] = true;
            _flipA = -1;
            _flipB = -1;
            _pairs++;
            if (PlayerController.Local != null)
            {
                PlayerController.Local.RPC_SetScore(_pairs);
            }

            if (_pairs >= PairCount)
            {
                _localFinished = true;
                if (PlayerController.Local != null)
                {
                    PlayerController.Local.RPC_SetScore(PairCount);
                    _director.DeclareWinner(PlayerController.Local, string.Empty);
                }
            }
        }
        else
        {
            _mismatchUntil = Time.unscaledTime + MismatchSeconds;
        }

        RefreshCells();
    }

    private void RefreshStatus()
    {
        if (_status == null || _director == null)
        {
            return;
        }

        if (_director.MatchTime < PeekSeconds)
        {
            int left = Mathf.CeilToInt(Mathf.Max(0f, PeekSeconds - _director.MatchTime));
            _status.text = $"Mira tu tablero... {left}";
            return;
        }

        _status.text = _localFinished
            ? "¡Completaste el memotest!"
            : $"Parejas {_pairs}/{PairCount}  ·  da vuelta 2 iguales";
        _status.color = new Color(1f, 0.85f, 0.35f);
    }

    private void RefreshCells()
    {
        bool peek = _director != null && _director.MatchTime < PeekSeconds;
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] == null)
            {
                continue;
            }

            bool show = peek || _locked[i] || i == _flipA || i == _flipB;
            if (!show)
            {
                _cells[i].sprite = null;
                _cells[i].color = new Color(0.16f, 0.17f, 0.2f, 1f);
                continue;
            }

            int value = _layout[i];
            if (_pieceSprites == null || value < 0 || value >= _pieceSprites.Length || _pieceSprites[value] == null)
            {
                _cells[i].sprite = null;
                _cells[i].color = ColorFor(value);
            }
            else
            {
                _cells[i].sprite = _pieceSprites[value];
                _cells[i].color = TintFor(value);
                _cells[i].preserveAspect = true;
            }
        }
    }

    private Color TintFor(int value)
    {
        if (_pieceTints != null && value >= 0 && value < _pieceTints.Length)
        {
            return _pieceTints[value];
        }

        return Color.white;
    }

    private static Color ColorFor(int value)
    {
        return value switch
        {
            0 => new Color(1f, 0.85f, 0.2f),
            1 => new Color(1f, 0.35f, 0.25f),
            2 => new Color(0.85f, 0.55f, 0.25f),
            3 => new Color(0.95f, 0.35f, 0.3f),
            4 => new Color(0.35f, 0.55f, 0.95f),
            6 => new Color(0.25f, 0.85f, 0.30f),
            7 => new Color(0.95f, 0.20f, 0.18f),
            8 => new Color(0.25f, 0.45f, 1f),
            _ => new Color(0.95f, 0.9f, 0.35f),
        };
    }

    private static Sprite SpriteFromPrefab(string resourceName)
    {
        var prefab = Resources.Load<GameObject>(resourceName);
        return prefab != null ? prefab.GetComponent<SpriteRenderer>()?.sprite : null;
    }
}
