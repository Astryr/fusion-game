using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Memotest compartido 4x4. Todos ven el mismo tablero; por turnos
/// cada jugador da vuelta dos fichas. Pareja = 1 punto y sigue jugando.
/// </summary>
public class MemoryPuzzleMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.MemoryPuzzle;

    public const int Size = 4;
    public const int CellCount = Size * Size;
    public const float PeekSeconds = 3f;
    public const float MismatchSeconds = 1.15f;
    public const float TurnTimeout = 18f;

    private BananaGameManager _director;
    private Canvas _canvas;
    private Text _status;
    private readonly Image[] _cells = new Image[CellCount];
    private bool _playing;
    private Sprite[] _pieceSprites;

    private static int _cachedSeed = int.MinValue;
    private static readonly int[] Layout = new int[CellCount];

    public void Setup(BananaGameManager director)
    {
        _director = director;
    }

    public void OnMatchStarted()
    {
        _playing = true;
        LoadSprites();
        EnsureLayout(_director.RoundSeed);
        BuildUI();
        RefreshCells();

        foreach (var player in _director.GetPlayers())
        {
            player.SetControlMode(PlayerControlMode.Disabled);
        }

        if (_director.Object.HasStateAuthority)
        {
            _director.ResetPuzzleState();
        }
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!_playing)
        {
            return;
        }

        if (hasAuthority)
        {
            _director.TickPuzzle(PeekSeconds, TurnTimeout);
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

    public static int PieceAt(int seed, int index)
    {
        EnsureLayout(seed);
        if (index < 0 || index >= CellCount)
        {
            return 0;
        }

        return Layout[index];
    }

    private static void EnsureLayout(int seed)
    {
        if (_cachedSeed == seed)
        {
            return;
        }

        _cachedSeed = seed;
        for (int i = 0; i < CellCount; i++)
        {
            Layout[i] = (i / 2) % 4;
        }

        var rng = new System.Random(seed);
        for (int i = CellCount - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (Layout[i], Layout[j]) = (Layout[j], Layout[i]);
        }
    }

    private void LoadSprites()
    {
        _pieceSprites = new[]
        {
            SpriteFromPrefab("Banana"),
            SpriteFromPrefab("BananaExplosiva"),
            SpriteFromScene("GrassTop"),
            SpriteFromScene("DirtFill"),
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
            new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(-210, -210), new Vector2(210, 210));

        _status = UIFactory.CreateText(panel, "Memotest", 16, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
        UIFactory.SetRect(_status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, 8), new Vector2(-8, 44));

        const float pad = 18f;
        float cell = (420f - pad * 2f) / Size;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int index = y * Size + x;
                var button = UIFactory.CreateButton(panel, string.Empty, new Color(0.18f, 0.2f, 0.24f));
                var rect = button.GetComponent<RectTransform>();
                float left = pad + x * cell;
                float bottom = pad + (Size - 1 - y) * cell;
                UIFactory.SetRect(rect,
                    new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(left, bottom),
                    new Vector2(left + cell - 6f, bottom + cell - 6f));

                var image = button.GetComponent<Image>();
                _cells[index] = image;
                int captured = index;
                button.onClick.AddListener(() => HandleCellClicked(captured));
            }
        }
    }

    private void HandleCellClicked(int index)
    {
        if (!_playing || _director == null || PlayerController.Local == null)
        {
            return;
        }

        if (_director.MatchTime < PeekSeconds)
        {
            return;
        }

        if (!_director.IsLocalPuzzleTurn())
        {
            GameFeedback.Toast("Todavia no es tu turno", new Color(1f, 0.75f, 0.4f));
            return;
        }

        _director.RPC_RequestPuzzleFlip(index, PlayerController.Local.Object.InputAuthority);
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
            _status.text = $"Mira el tablero... {left}";
            _status.color = new Color(1f, 0.85f, 0.35f);
            return;
        }

        if (_director.PuzzleRevealUntil >= 0f && _director.MatchTime < _director.PuzzleRevealUntil)
        {
            _status.text = "No era pareja";
            _status.color = new Color(1f, 0.5f, 0.4f);
            return;
        }

        PlayerController turn = _director.FindPlayerById(_director.PuzzleTurnId);
        string name = turn != null ? turn.DisplayName : "alguien";
        if (_director.IsLocalPuzzleTurn())
        {
            _status.text = "Tu turno: da vuelta 2 fichas iguales";
            _status.color = new Color(0.55f, 1f, 0.6f);
        }
        else
        {
            _status.text = $"Turno de {name}";
            _status.color = new Color(0.85f, 0.86f, 0.9f);
        }
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

            bool show = peek ||
                        (_director != null && (_director.IsPuzzleCellLocked(i) || _director.IsPuzzleCellFlipped(i)));
            if (!show)
            {
                _cells[i].sprite = null;
                _cells[i].color = new Color(0.16f, 0.17f, 0.2f, 1f);
                continue;
            }

            int value = PieceAt(_director.RoundSeed, i);
            if (_pieceSprites == null || value < 0 || value >= _pieceSprites.Length || _pieceSprites[value] == null)
            {
                _cells[i].sprite = null;
                _cells[i].color = ColorFor(value);
            }
            else
            {
                _cells[i].sprite = _pieceSprites[value];
                _cells[i].color = Color.white;
                _cells[i].preserveAspect = true;
            }
        }
    }

    private static Color ColorFor(int value)
    {
        return value switch
        {
            0 => new Color(1f, 0.85f, 0.2f),
            1 => new Color(1f, 0.35f, 0.25f),
            2 => new Color(0.35f, 0.7f, 0.3f),
            _ => new Color(0.45f, 0.3f, 0.2f),
        };
    }

    private static Sprite SpriteFromScene(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<SpriteRenderer>()?.sprite : null;
    }

    private static Sprite SpriteFromPrefab(string resourceName)
    {
        var prefab = Resources.Load<GameObject>(resourceName);
        return prefab != null ? prefab.GetComponent<SpriteRenderer>()?.sprite : null;
    }
}
