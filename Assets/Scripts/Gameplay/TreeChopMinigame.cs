using UnityEngine;
using UnityEngine.UI;

public class TreeChopMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.TreeChop;

    private static readonly KeyCode[] Pool =
    {
        KeyCode.Q, KeyCode.E, KeyCode.R, KeyCode.F, KeyCode.G, KeyCode.H,
        KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Z, KeyCode.C, KeyCode.V,
        KeyCode.B, KeyCode.N, KeyCode.M, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
    };

    private BananaGameManager _director;
    private Canvas _canvas;
    private Text _keyLabel;
    private Text _hintLabel;
    private Text _scoreLabel;
    private Image _treeFill;
    private KeyCode _current;
    private bool _troll;
    private float _timeLeft;
    private bool _playing;
    private System.Random _rng;

    public void Setup(BananaGameManager director)
    {
        _director = director;
    }

    public void OnCountdownStarted() { }

    public void OnMatchStarted()
    {
        _playing = true;
        int seed = _director.RoundSeed + (PlayerController.Local != null ? PlayerController.Local.Object.InputAuthority.PlayerId * 17 : 1);
        _rng = new System.Random(seed);
        BuildUI();
        NextKey();

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

        if (!hasAuthority && PlayerController.Local != null && !PlayerController.Local.IsEliminated)
        {
            _timeLeft -= deltaTime;
            RefreshHud();

            if (Input.anyKeyDown)
            {
                HandleKey();
            }

            if (_timeLeft <= 0f)
            {
                if (!_troll)
                {
                    ApplyDelta(-1, "Muy lento");
                }

                NextKey();
            }
        }

        if (hasAuthority)
        {
            foreach (var player in _director.GetPlayers())
            {
                if (player.Score >= 30)
                {
                    _director.DeclareWinner(player, "rompio el arbol");
                    return;
                }
            }

            if (_director.MatchTime >= 60f)
            {
                _director.DeclareHighestScoreWinner("mas golpes");
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

    private void HandleKey()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            return;
        }

        bool pressedCurrent = Input.GetKeyDown(_current);
        if (_troll)
        {
            if (pressedCurrent)
            {
                ApplyDelta(-1, "¡Era trampa!");
                NextKey();
            }

            return;
        }

        if (pressedCurrent)
        {
            ApplyDelta(1, "¡Tack!");
            NextKey();
        }
        else if (AnyPoolKeyDown())
        {
            ApplyDelta(-1, "Tecla incorrecta");
            NextKey();
        }
    }

    private bool AnyPoolKeyDown()
    {
        foreach (var key in Pool)
        {
            if (Input.GetKeyDown(key))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyDelta(int delta, string reason)
    {
        if (PlayerController.Local == null)
        {
            return;
        }

        int next = Mathf.Max(0, PlayerController.Local.Score + delta);
        PlayerController.Local.RPC_SetScore(next);
        Color color = delta >= 0 ? new Color(0.6f, 1f, 0.5f) : new Color(1f, 0.45f, 0.4f);
        GameFeedback.Toast($"{reason}  ({next}/30)", color);
        if (delta > 0)
        {
            GameFeedback.WorldPopup(PlayerController.Local.transform.position + Vector3.up, "+1", color);
        }
    }

    private void NextKey()
    {
        _current = Pool[_rng.Next(Pool.Length)];
        _troll = _rng.NextDouble() < 0.18;
        _timeLeft = Mathf.Max(1.05f, 2.4f - _director.MatchTime * 0.02f);
        RefreshHud();
    }

    private void RefreshHud()
    {
        if (_keyLabel == null)
        {
            return;
        }

        _keyLabel.text = KeyLabel(_current);
        _keyLabel.color = _troll ? new Color(1f, 0.25f, 0.22f) : new Color(1f, 0.86f, 0.25f);
        _hintLabel.text = _troll ? "NO la apretes. Dejala pasar." : "¡Apreta esta tecla!";
        int score = PlayerController.Local != null ? PlayerController.Local.Score : 0;
        _scoreLabel.text = $"Golpes {score}/30   |   {Mathf.CeilToInt(Mathf.Max(0f, _timeLeft))}s";
        if (_treeFill != null)
        {
            _treeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(score / 30f), 1f);
        }
    }

    private void BuildUI()
    {
        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
        }

        _canvas = UIFactory.CreateCanvas("TreeChopCanvas");
        var panel = UIFactory.CreatePanel(_canvas.transform, "QtePanel", new Color(0.08f, 0.1f, 0.08f, 0.72f),
            new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), new Vector2(-240, -70), new Vector2(240, 90));

        _hintLabel = UIFactory.CreateText(panel, "¡Apreta esta tecla!", 16, TextAnchor.MiddleCenter, Color.white);
        UIFactory.SetRect(_hintLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -28), new Vector2(-8, -4));

        _keyLabel = UIFactory.CreateTitleText(panel, "Q", 54, new Color(1f, 0.86f, 0.25f), new Color(0.2f, 0.1f, 0.05f));
        UIFactory.SetRect(_keyLabel.rectTransform, new Vector2(0, 0.25f), new Vector2(1, 0.85f), new Vector2(10, 0), new Vector2(-10, 0));

        _scoreLabel = UIFactory.CreateText(panel, "Golpes 0/30", 15, TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 0.8f));
        UIFactory.SetRect(_scoreLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 8), new Vector2(-8, 30));

        var bar = UIFactory.CreatePanel(_canvas.transform, "TreeBarBack", new Color(0.15f, 0.12f, 0.08f, 0.9f),
            new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), new Vector2(-220, -10), new Vector2(220, 10));
        var fill = UIFactory.CreatePanel(bar, "TreeBarFill", new Color(0.45f, 0.72f, 0.28f, 1f),
            new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
        _treeFill = fill.GetComponent<Image>();
    }

    private static string KeyLabel(KeyCode key)
    {
        string raw = key.ToString();
        if (raw.StartsWith("Alpha"))
        {
            return raw.Substring(5);
        }

        return raw;
    }
}
