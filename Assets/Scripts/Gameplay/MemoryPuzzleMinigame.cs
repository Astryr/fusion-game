using UnityEngine;
using UnityEngine.UI;

public class MemoryPuzzleMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.MemoryPuzzle;

    private const int Size = 4;
    private const float PreviewSeconds = 5f;

    private BananaGameManager _director;
    private Canvas _canvas;
    private Text _status;
    private readonly Image[] _cells = new Image[Size * Size];
    private readonly int[] _target = new int[Size * Size];
    private readonly int[] _board = new int[Size * Size];
    private readonly bool[] _locked = new bool[Size * Size];
    private int _cursor;
    private bool _playing;
    private bool _preview;
    private bool _localFinished;
    private Sprite[] _pieceSprites;

    public void Setup(BananaGameManager director)
    {
        _director = director;
    }

    public void OnMatchStarted()
    {
        _localFinished = false;
        _playing = true;
        _preview = true;
        _cursor = 0;
        LoadSprites();
        BuildTarget();
        BuildUI();
        RefreshCells(true);

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

        if (_preview)
        {
            if (!hasAuthority && _status != null)
            {
                int left = Mathf.CeilToInt(Mathf.Max(0f, PreviewSeconds - _director.MatchTime));
                _status.text = $"Memoriza el cuadro... {left}";
            }

            if (_director.MatchTime >= PreviewSeconds && _preview)
            {
                _preview = false;
                ScrambleBoard();
                RefreshCells(false);
                if (_status != null)
                {
                    _status.text = "Reconstruilo. Click = colocar la siguiente pieza.";
                }
            }

            return;
        }

        if (hasAuthority)
        {
            foreach (var player in _director.GetPlayers())
            {
                if (player.Score >= 16)
                {
                    _director.DeclareWinner(player, "armo el cuadro");
                    return;
                }
            }

            if (_director.MatchTime >= 70f)
            {
                _director.DeclareHighestScoreWinner("mas fichas correctas");
            }
        }
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

    private void BuildTarget()
    {
        var rng = new System.Random(_director.RoundSeed + (PlayerController.Local != null ? PlayerController.Local.Object.InputAuthority.PlayerId : 0));
        for (int i = 0; i < _target.Length; i++)
        {
            _target[i] = i % 4;
            _board[i] = _target[i];
            _locked[i] = false;
        }

        for (int i = _target.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_target[i], _target[j]) = (_target[j], _target[i]);
            _board[i] = _target[i];
        }
    }

    private void ScrambleBoard()
    {
        var rng = new System.Random(_director.RoundSeed + 99);
        for (int i = 0; i < _board.Length; i++)
        {
            _board[i] = -1;
            _locked[i] = false;
        }

        // Deja 4 fichas puestas al azar para que no arranque vacio.
        for (int n = 0; n < 4; n++)
        {
            int index = rng.Next(_board.Length);
            _board[index] = _target[index];
            _locked[index] = true;
        }
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

        _status = UIFactory.CreateText(panel, "Memoriza el cuadro", 16, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
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
        if (!_playing || _preview || _localFinished || _locked[index])
        {
            return;
        }

        int piece = _cursor % 4;
        _board[index] = piece;
        if (_board[index] == _target[index])
        {
            _locked[index] = true;
            _cursor++;
            RefreshCells(false);
            UpdateLocalScore();
            if (CountCorrect() >= 16)
            {
                _localFinished = true;
                if (PlayerController.Local != null)
                {
                    PlayerController.Local.RPC_SetScore(16);
                }

                _director.RPC_ShowFeedback($"{PlayerController.Local.DisplayName} completo el puzzle", 0);
            }
        }
        else
        {
            NudgeACorrectPiece();
            RefreshCells(false);
            UpdateLocalScore();
            GameFeedback.Toast("Ficha mal: se movio otra que estaba bien", new Color(1f, 0.5f, 0.4f));
        }
    }

    private void NudgeACorrectPiece()
    {
        for (int i = 0; i < _locked.Length; i++)
        {
            if (_locked[i])
            {
                _locked[i] = false;
                _board[i] = (_target[i] + 1) % 4;
                return;
            }
        }
    }

    private void UpdateLocalScore()
    {
        if (PlayerController.Local != null)
        {
            PlayerController.Local.RPC_SetScore(CountCorrect());
        }
    }

    private int CountCorrect()
    {
        int count = 0;
        for (int i = 0; i < _board.Length; i++)
        {
            if (_board[i] == _target[i])
            {
                count++;
            }
        }

        return count;
    }

    private void RefreshCells(bool showTarget)
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] == null)
            {
                continue;
            }

            int value = showTarget ? _target[i] : _board[i];
            if (value < 0 || _pieceSprites == null || value >= _pieceSprites.Length || _pieceSprites[value] == null)
            {
                _cells[i].sprite = null;
                _cells[i].color = value < 0 ? new Color(0.15f, 0.16f, 0.18f) : ColorFor(value);
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
