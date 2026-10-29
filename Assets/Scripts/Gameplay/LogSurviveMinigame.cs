using System;
using UnityEngine;

public class LogSurviveMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.LogSurvive;

    private const float LogHalfWidth = 12f;
    private const int SegmentCount = 26;
    private const float SegmentWidth = LogHalfWidth * 2f / SegmentCount;
    private const float PlatformThickness = 1.05f;
    private const float LogVisualHeight = 1.75f;
    private const float PlatformTopY = 0f;
    private const float RockSpawnY = 9f;
    private const float RockVisualHeight = 1.25f;
    private const float RockImpactY = PlatformTopY + RockVisualHeight * 0.5f;
    private const float DeathY = -8f;
    private const float MatchDuration = 95f;

    private BananaGameManager _director;
    private Sprite _logSprite;
    private Sprite _stoneSprite;
    private Sprite _terrainSprite;
    private Texture2D _logTexture;
    private Texture2D _stoneTexture;
    private Transform _rockVisual;
    private System.Random _rng;
    private float _rockTimer;
    private int _renderedHoleMask = int.MinValue;
    private bool _started;

    public void Setup(BananaGameManager director)
    {
        _director = director;
        _terrainSprite = SpriteFromScene("DirtFill") ?? SpriteFromScene("GrassTop");
    }

    public void OnCountdownStarted() { }

    public void OnMatchStarted()
    {
        MiniGameWorld.Clear();
        BuildVisualAssets();
        StageBackdrop.BeginScrollingStage(-LogHalfWidth - 10f, LogHalfWidth + 10f);
        _started = true;
        _rockTimer = 1.5f;
        _rng = new System.Random(_director.RoundSeed + 503);
        _rockVisual = null;
        _renderedHoleMask = int.MinValue;

        if (_director.Object.HasStateAuthority)
        {
            _director.LogHoleMask = 0;
            _director.LogRockActive = false;
            _director.LogRockX = 0f;
            _director.LogRockY = 0f;
        }

        RefreshPlatformVisual(_director.LogHoleMask);
        BuildRockVisual();

        PlayerController[] players = _director.GetPlayers();
        float spacing = players.Length > 1
            ? Mathf.Min(1.6f, (LogHalfWidth * 2f - 1f) / (players.Length - 1))
            : 0f;
        int index = 0;
        foreach (var player in players)
        {
            float x = (index - (players.Length - 1) * 0.5f) * spacing;
            if (_director.Object.HasStateAuthority)
            {
                player.Teleport(new Vector3(x, PlatformTopY, 0f));
            }
            player.SetControlMode(PlayerControlMode.Platformer);
            player.SetPushEnabled(true);
            player.SetWorldBounds(-LogHalfWidth - 1f, LogHalfWidth + 1f, -100f, DeathY);
            index++;
        }
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!_started)
        {
            return;
        }

        RefreshPlatformVisual(_director.LogHoleMask);
        UpdateRockVisual();

        PlayerController[] players = _director.GetPlayers();
        bool soloRun = _director.AllowSoloPractice && players.Length == 1;
        foreach (var player in players)
        {
            if (player.IsEliminated)
            {
                continue;
            }

            player.SetWorldBounds(-LogHalfWidth - 1f, LogHalfWidth + 1f, -100f, DeathY);
            if (hasAuthority && player.transform.position.y <= DeathY)
            {
                player.Eliminate();
                _director.RPC_ShowFeedback($"{player.DisplayName} se cayo del tronco", 2);
            }
        }

        if (hasAuthority && soloRun && players[0].IsEliminated)
        {
            _director.DeclareSoloDefeat("Caíste al vacío");
            return;
        }

        if (!hasAuthority)
        {
            return;
        }

        TickRock(deltaTime, players);
        if (_director.Phase != MatchPhase.Playing)
        {
            return;
        }

        int alive = 0;
        PlayerController last = null;
        foreach (var player in players)
        {
            if (!player.IsEliminated)
            {
                alive++;
                last = player;
            }
        }

        if (!soloRun && alive <= 1 && players.Length > 1)
        {
            _director.DeclareWinner(last, "ultimo en el tronco");
        }
        else if (!soloRun && _director.MatchTime >= MatchDuration)
        {
            _director.DeclareWinner(ClosestToCenter() ?? last, "aguanto mas");
        }
    }

    private PlayerController ClosestToCenter()
    {
        PlayerController best = null;
        foreach (var player in _director.GetPlayers())
        {
            if (!player.IsEliminated &&
                (best == null || Mathf.Abs(player.transform.position.x) < Mathf.Abs(best.transform.position.x)))
            {
                best = player;
            }
        }

        return best;
    }

    public void OnMatchEnded()
    {
        _started = false;
        foreach (var player in _director.GetPlayers())
        {
            player.SetPushEnabled(false);
            player.SetControlMode(PlayerControlMode.Disabled);
        }
    }

    public void Cleanup()
    {
        _started = false;
        MiniGameWorld.Clear();
        StageBackdrop.Clear();
        ReleaseVisualAssets();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = BananaRushConfig.CameraOrthographicSize;
            cam.transform.position = new Vector3(0f, BananaRushConfig.CameraCenterY, -10f);
        }
    }

    private void BuildVisualAssets()
    {
        ReleaseVisualAssets();
        _logTexture = CreateLogTexture();
        _logSprite = Sprite.Create(_logTexture, new Rect(0f, 0f, _logTexture.width, _logTexture.height),
            new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(20f, 8f, 20f, 8f));
        _stoneTexture = CreateStoneTexture();
        _stoneSprite = Sprite.Create(_stoneTexture, new Rect(0f, 0f, _stoneTexture.width, _stoneTexture.height),
            new Vector2(0.5f, 0.5f), 70f, 0, SpriteMeshType.FullRect);
    }

    private void ReleaseVisualAssets()
    {
        if (_logSprite != null)
        {
            Destroy(_logSprite);
            _logSprite = null;
        }
        if (_stoneSprite != null)
        {
            Destroy(_stoneSprite);
            _stoneSprite = null;
        }
        if (_logTexture != null)
        {
            Destroy(_logTexture);
            _logTexture = null;
        }
        if (_stoneTexture != null)
        {
            Destroy(_stoneTexture);
            _stoneTexture = null;
        }
    }

    private static Texture2D CreateLogTexture()
    {
        const int width = 192;
        const int height = 64;
        var pixels = new Color32[width * height];
        var bark = new Color32(104, 51, 27, 255);
        var warmBark = new Color32(137, 72, 36, 255);
        var darkBark = new Color32(67, 33, 22, 255);
        var lightBark = new Color32(172, 101, 51, 255);

        for (int y = 0; y < height; y++)
        {
            float normalizedY = (y - height * 0.5f) / (height * 0.5f);
            for (int x = 0; x < width; x++)
            {
                float edgeWobble = Mathf.Sin(x * 0.16f) * 1.5f + Mathf.Sin(x * 0.047f + 1.7f) * 1.2f;
                float edge = 7f + edgeWobble;
                if (y < edge || y >= height - edge)
                {
                    continue;
                }

                float grain = Mathf.Sin(y * 0.67f + Mathf.Sin(x * 0.055f) * 2.1f) +
                              Mathf.Sin(y * 1.49f + Mathf.Sin(x * 0.12f) * 0.8f) * 0.32f;
                Color32 color = grain > 0.63f ? lightBark : grain < -0.48f ? darkBark : grain > 0.1f ? warmBark : bark;

                int endDistance = Mathf.Min(x, width - 1 - x);
                if (endDistance < 22)
                {
                    float ringX = endDistance / 22f;
                    float ringY = normalizedY * 0.92f;
                    float ring = Mathf.Sin(Mathf.Sqrt(ringX * ringX + ringY * ringY) * 34f);
                    if (ring > 0.48f)
                    {
                        color = lightBark;
                    }
                    else if (ring < -0.55f)
                    {
                        color = darkBark;
                    }
                }

                pixels[y * width + x] = color;
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "LogSurvive_WoodTexture"
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D CreateStoneTexture()
    {
        const int width = 96;
        const int height = 88;
        var pixels = new Color32[width * height];
        var stone = new Color32(103, 111, 111, 255);
        var lightStone = new Color32(157, 165, 160, 255);
        var midStone = new Color32(126, 137, 134, 255);
        var darkStone = new Color32(64, 73, 75, 255);

        for (int y = 0; y < height; y++)
        {
            float ny = (y - (height - 1) * 0.5f) / (height * 0.5f);
            for (int x = 0; x < width; x++)
            {
                float nx = (x - (width - 1) * 0.5f) / (width * 0.5f);
                float angle = Mathf.Atan2(ny, nx);
                float edge = 0.79f + Mathf.Sin(angle * 5f + 0.4f) * 0.1f + Mathf.Sin(angle * 9f) * 0.045f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                if (radius > edge)
                {
                    continue;
                }

                float facet = nx * 0.65f - ny * 0.72f;
                Color32 color = facet > 0.18f ? lightStone : facet < -0.35f ? darkStone : midStone;
                if (radius > edge - 0.1f)
                {
                    color = darkStone;
                }
                else if (Mathf.Sin((nx + ny * 0.7f) * 18f) > 0.88f && facet > 0f)
                {
                    color = stone;
                }

                pixels[y * width + x] = color;
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "LogSurvive_StoneTexture"
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private void TickRock(float deltaTime, PlayerController[] players)
    {
        if (_director.LogRockActive)
        {
            float nextY = _director.LogRockY - RockFallSpeed() * deltaTime;
            _director.LogRockY = nextY;

            foreach (var player in players)
            {
                if (player.IsEliminated || Mathf.Abs(player.transform.position.x - _director.LogRockX) > 0.7f)
                {
                    continue;
                }

                float playerBottom = player.transform.position.y;
                if (nextY >= playerBottom - 0.2f && nextY <= playerBottom + 1.15f)
                {
                    player.Eliminate();
                    _director.RPC_ShowFeedback($"{player.DisplayName} fue golpeado por una piedra", 2);
                    if (_director.AllowSoloPractice && players.Length == 1)
                    {
                        _director.DeclareSoloDefeat("Te golpeó una piedra");
                        return;
                    }
                }
            }

            if (nextY <= RockImpactY)
            {
                BreakPlatform(_director.LogRockX);
                _director.LogRockActive = false;
                _rockTimer = RockInterval();
            }

            return;
        }

        _rockTimer -= deltaTime;
        if (_rockTimer <= 0f)
        {
            SpawnRock();
        }
    }

    private float RockInterval()
    {
        return Mathf.Lerp(1.55f, 0.6f, Mathf.Clamp01(_director.MatchTime / 70f));
    }

    private float RockFallSpeed()
    {
        return Mathf.Lerp(5.5f, 8.5f, Mathf.Clamp01(_director.MatchTime / 70f));
    }

    private void SpawnRock()
    {
        int firstCell = _rng.Next(SegmentCount);
        int cell = -1;
        for (int offset = 0; offset < SegmentCount; offset++)
        {
            int candidate = (firstCell + offset) % SegmentCount;
            if ((_director.LogHoleMask & (1 << candidate)) == 0)
            {
                cell = candidate;
                break;
            }
        }

        if (cell < 0)
        {
            _rockTimer = RockInterval();
            return;
        }

        float left = -LogHalfWidth + cell * SegmentWidth;
        _director.LogRockX = left + SegmentWidth * (0.25f + (float)_rng.NextDouble() * 0.5f);
        _director.LogRockY = RockSpawnY;
        _director.LogRockActive = true;
    }

    private void BreakPlatform(float x)
    {
        int center = Mathf.Clamp(Mathf.FloorToInt((x + LogHalfWidth) / SegmentWidth), 0, SegmentCount - 1);
        int radius = (_director.LogHoleMask & (1 << center)) != 0 ? 2 : 1;
        int mask = _director.LogHoleMask;
        for (int i = Mathf.Max(0, center - radius); i <= Mathf.Min(SegmentCount - 1, center + radius); i++)
        {
            mask |= 1 << i;
        }

        _director.LogHoleMask = mask;
    }

    private void RefreshPlatformVisual(int holeMask)
    {
        if (_renderedHoleMask == holeMask)
        {
            return;
        }

        _renderedHoleMask = holeMask;
        Transform root = MiniGameWorld.GetRoot();
        Transform previous = root.Find("LogSegments");
        if (previous != null)
        {
            Destroy(previous.gameObject);
        }

        var segments = new GameObject("LogSegments").transform;
        segments.SetParent(root, false);
        int groundLayer = LayerMask.NameToLayer("Ground");

        int cell = 0;
        while (cell < SegmentCount)
        {
            if ((holeMask & (1 << cell)) != 0)
            {
                cell++;
                continue;
            }

            int firstCell = cell;
            while (cell < SegmentCount && (holeMask & (1 << cell)) == 0)
            {
                cell++;
            }

            int cellCount = cell - firstCell;
            float runWidth = cellCount * SegmentWidth;
            float centerX = -LogHalfWidth + (firstCell + cellCount * 0.5f) * SegmentWidth;
            var segment = new GameObject($"Log_{firstCell}_{cell - 1}");
            segment.transform.SetParent(segments, false);
            segment.transform.position = new Vector3(centerX, PlatformTopY - PlatformThickness * 0.5f, 0f);
            if (groundLayer >= 0)
            {
                segment.layer = groundLayer;
            }

            var renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = _logSprite != null ? _logSprite : _terrainSprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(runWidth, LogVisualHeight);
            renderer.sortingLayerName = "Ground";
            renderer.sortingOrder = 5;

            var collider = segment.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(runWidth, PlatformThickness);
        }
    }

    private void BuildRockVisual()
    {
        if (_stoneSprite == null)
        {
            return;
        }

        GameObject stone = MiniGameWorld.SpriteObject("FallingStone", _stoneSprite, Vector3.zero,
            Vector3.one, "Props", 20);
        stone.GetComponent<SpriteRenderer>().color = Color.white;

        _rockVisual = stone.transform;
        _rockVisual.gameObject.SetActive(false);
    }

    private void UpdateRockVisual()
    {
        if (_rockVisual == null)
        {
            return;
        }

        bool active = _director.LogRockActive;
        if (_rockVisual.gameObject.activeSelf != active)
        {
            _rockVisual.gameObject.SetActive(active);
        }

        if (active)
        {
            _rockVisual.position = new Vector3(_director.LogRockX, _director.LogRockY, 0f);
        }
    }

    private static Sprite SpriteFromScene(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<SpriteRenderer>()?.sprite : null;
    }
}