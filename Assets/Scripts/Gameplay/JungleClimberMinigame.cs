using System.Collections.Generic;
using UnityEngine;

public class JungleClimberMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.JungleClimber;

    private const float InitialCameraRiseSpeed = 0.48f;
    private const float MaximumCameraRiseSpeed = 1.35f;
    private const float StartCameraY = 3.5f;
    private const float PlatformWidth = 2.4f;
    private const float PlatformHeight = 0.55f;
    private const float VerticalGap = 2.8f;
    private const float HorizontalRange = 8.8f;
    private const float GenerateAhead = 32f;
    private const float DensePlatformWidth = 2.15f;
    private const float CleanupBelowCamera = 18f;
    private const float CoconutStartTime = 12f;
    private const float BananaStartTime = 24f;

    private BananaGameManager _director;
    private readonly Sprite[] _platformSprites = new Sprite[3];
    private Sprite _coconutSprite;
    private Sprite _explosiveBananaSprite;
    private readonly Dictionary<int, GameObject> _platforms = new Dictionary<int, GameObject>();
    private readonly List<Transform> _hazardVisuals = new List<Transform>();
    private readonly List<ClimberHazardItem> _hazardItems = new List<ClimberHazardItem>();
    private readonly HashSet<int> _eliminatedInAuthority = new HashSet<int>();
    private System.Random _rng;
    private bool _started;
    private int _nextPlatformIndex;
    private float _coconutTimer;
    private float _bananaTimer;
    private int _renderedHazardSequence = -1;

    public void Setup(BananaGameManager director)
    {
        _director = director;
        LoadVisualAssets();
    }

    private void LoadVisualAssets()
    {
        for (int i = 0; i < _platformSprites.Length; i++)
        {
            _platformSprites[i] = CreatePlatformSprite();
        }

#if UNITY_EDITOR
        string atlasPath = "Assets/Sprites/Environment/GroundTileset.png";
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(atlasPath);
        foreach (var asset in assets)
        {
            if (asset is Sprite sprite)
            {
                for (int i = 0; i < _platformSprites.Length; i++)
                {
                    if (sprite.name == $"GroundTile_{i + 4}")
                    {
                        _platformSprites[i] = sprite;
                        break;
                    }
                }
            }
        }
#endif

        if (_platformSprites[0] == null)
        {
            for (int i = 0; i < _platformSprites.Length; i++)
            {
                _platformSprites[i] = CreatePlatformSprite();
            }
        }

        _coconutSprite = CreateCoconutSprite();
        var explosive = Resources.Load<GameObject>("BananaExplosiva");
        _explosiveBananaSprite = explosive != null
            ? explosive.GetComponent<SpriteRenderer>()?.sprite
            : null;
    }

    public void OnCountdownStarted() { }

    public void OnMatchStarted()
    {
        MiniGameWorld.Clear();
        _platforms.Clear();
        _eliminatedInAuthority.Clear();
        ClearHazardVisuals();
        _started = true;
        _nextPlatformIndex = 0;
        _coconutTimer = 2f;
        _bananaTimer = 3.5f;
        _rng = new System.Random(_director.RoundSeed + 4201);
        _renderedHazardSequence = -1;

        if (_director.Object.HasStateAuthority)
        {
            _director.ClimberCameraY = StartCameraY;
            _director.ClimberTopPlatformY = 0f;
            _director.ClimberHazardActive = false;
            _director.ClimberHazardType = 0;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = 7.5f;
            cam.transform.position = new Vector3(0f, StartCameraY, -10f);
        }

        PlaceStartingPlatform();
        GenerateDenseStartingArea();

        int index = 0;
        foreach (var player in _director.GetPlayers())
        {
            player.PrimateRoundDisqualified = false;
            player.SetControlMode(PlayerControlMode.JungleClimber);
            player.SetPushEnabled(false);
            player.SetWorldBounds(-HorizontalRange - 1f, HorizontalRange + 1f, -100f, -12f);
            if (_director.Object.HasStateAuthority)
            {
                float x = (index - (_director.GetPlayers().Length - 1) * 0.5f) * 1.35f;
                player.Teleport(new Vector3(x, 1f, 0f));
            }
            index++;
        }
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!_started)
        {
            return;
        }

        if (hasAuthority)
        {
            float riseProgress = Mathf.Clamp01(_director.MatchTime / 75f);
            float riseSpeed = Mathf.Lerp(InitialCameraRiseSpeed, MaximumCameraRiseSpeed, riseProgress);
            _director.ClimberCameraY += riseSpeed * deltaTime;
            _director.ClimberTopPlatformY = Mathf.Max(_director.ClimberTopPlatformY,
                GetHighestPlatformY());
        }

        if (Camera.main != null)
        {
            Vector3 target = new Vector3(0f, _director.ClimberCameraY, -10f);
            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, target,
                Mathf.Clamp01(deltaTime * 5f));
        }

        GenerateDensePlatforms();
        CleanupOldPlatforms();

        float cameraBottom = _director.ClimberCameraY - 7.5f;
        foreach (var player in _director.GetPlayers())
        {
            if (player.IsEliminated)
            {
                continue;
            }

            if (hasAuthority && player.transform.position.y < cameraBottom - 0.8f)
            {
                EliminatePlayer(player, "quedo fuera de la camara");
            }
        }

        if (hasAuthority)
        {
            TickCoconuts(deltaTime);
            TickExplosiveBananas(deltaTime);
        }

        RefreshHazardVisual();

        if (!hasAuthority)
        {
            return;
        }

        PlayerController[] players = _director.GetPlayers();
        PlayerController last = null;
        int alive = 0;
        foreach (var player in players)
        {
            if (!player.IsEliminated)
            {
                alive++;
                last = player;
            }
        }

        if (alive == 1 && players.Length > 1)
        {
            _director.DeclareWinner(last, "ultimo en escalar");
        }
    }

    public void OnMatchEnded()
    {
        _started = false;
        foreach (var player in _director.GetPlayers())
        {
            player.SetControlMode(PlayerControlMode.Disabled);
        }
    }

    public void Cleanup()
    {
        _started = false;
        MiniGameWorld.Clear();
        _platforms.Clear();
        StageBackdrop.Clear();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = BananaRushConfig.CameraOrthographicSize;
            cam.transform.position = new Vector3(0f, BananaRushConfig.CameraCenterY, -10f);
        }
    }

    private void PlaceStartingPlatform()
    {
        PlacePlatform(-1, 0f, 0f);
    }

    private void GenerateDenseStartingArea()
    {
        for (float y = 2.8f; y < 25f; y += VerticalGap)
        {
            int row = Mathf.RoundToInt(y / VerticalGap);
            for (int column = -3; column <= 3; column++)
            {
                if ((column + row) % 5 == 0)
                {
                    continue;
                }

                float x = column * 2.8f + Mathf.Sin(row * 1.7f) * 0.65f;
                int index = 10000 + row * 10 + (column + 3);
                PlacePlatform(index, x, y);
            }
        }
    }

    private void GenerateDensePlatforms()
    {
        float targetTop = _director.ClimberCameraY + GenerateAhead;
        if (_director.ClimberTopPlatformY >= targetTop)
        {
            return;
        }

        while (_director.ClimberTopPlatformY < targetTop)
        {
            int row = _nextPlatformIndex++;
            float y = row == 0
                ? 25f
                : _director.ClimberTopPlatformY + VerticalGap;

            int count = row % 4 == 0 ? 5 : 6;
            for (int column = 0; column < count; column++)
            {
                float fraction = (column + 0.5f) / count;
                float x = Mathf.Lerp(-HorizontalRange + 1f, HorizontalRange - 1f, fraction);
                float seed = _director.RoundSeed + row * 91f + column * 37f;
                x += Mathf.Sin(seed * 0.71f) * 0.75f;
                x = Mathf.Clamp(x, -HorizontalRange, HorizontalRange);

                if ((row + column) % 5 == 0)
                {
                    x -= 0.8f;
                }

                int index = row * 100 + column;
                PlacePlatform(index, x, y);
            }

            _director.ClimberTopPlatformY = y;
        }
    }

    private void PlacePlatform(int index, float x, float y)
    {
        if (_platforms.ContainsKey(index))
        {
            return;
        }

        int normalizedIndex = index < 0 ? 0 : index;
        Sprite platformSprite = _platformSprites[normalizedIndex % _platformSprites.Length];
        if (platformSprite == null)
        {
            platformSprite = CreatePlatformSprite();
        }
        var platform = MiniGameWorld.SpriteObject($"ClimbPlatform_{index}", platformSprite,
            new Vector3(x, y, 0f), new Vector3(PlatformWidth, PlatformHeight, 1f), "Ground", 4);
        platform.layer = LayerMask.NameToLayer("Ground");
        var renderer = platform.GetComponent<SpriteRenderer>();
        var collider = platform.AddComponent<BoxCollider2D>();
        if (renderer != null && renderer.sprite != null)
        {
            Vector2 spriteSize = renderer.sprite.bounds.size;
            collider.size = new Vector2(spriteSize.x / PlatformWidth, spriteSize.y / PlatformHeight);
        }
        else
        {
            collider.size = Vector2.one;
        }
        collider.offset = Vector2.zero;
        _platforms[index] = platform;
    }

    private float GetPlatformX(int index)
    {
        if (_platforms.TryGetValue(index, out GameObject platform))
        {
            return platform.transform.position.x;
        }

        return 0f;
    }

    private float GetHighestPlatformY()
    {
        float highest = -100f;
        foreach (var platform in _platforms.Values)
        {
            if (platform != null)
            {
                highest = Mathf.Max(highest, platform.transform.position.y);
            }
        }
        return highest;
    }

    private void CleanupOldPlatforms()
    {
        float limit = _director.ClimberCameraY - CleanupBelowCamera;
        List<int> remove = null;
        foreach (var pair in _platforms)
        {
            if (pair.Value == null || pair.Value.transform.position.y < limit)
            {
                remove ??= new List<int>();
                remove.Add(pair.Key);
            }
        }

        if (remove == null)
        {
            return;
        }

        foreach (int key in remove)
        {
            if (_platforms.TryGetValue(key, out GameObject platform) && platform != null)
            {
                Destroy(platform);
            }
            _platforms.Remove(key);
        }
    }

    private void ClearHazardVisuals()
    {
        foreach (var visual in _hazardVisuals)
        {
            if (visual != null)
            {
                Destroy(visual.gameObject);
            }
        }
        _hazardVisuals.Clear();
        _hazardItems.Clear();
    }

    private sealed class ClimberHazardItem
    {
        public int Sequence;
        public int Type;
        public float StartTime;
        public float StartDelay;
        public float X;
        public float Y;
        public float SpeedX;
        public float FallSpeed;
    }

    private void TickCoconuts(float deltaTime)
    {
        if (_director.MatchTime < CoconutStartTime)
        {
            return;
        }

        _coconutTimer -= deltaTime;
        if (_coconutTimer > 0f)
        {
            return;
        }

        float x = Random.Range(-HorizontalRange, HorizontalRange);
        float y = _director.ClimberCameraY + 9f;
        float speedX = 0f;

        if (_director.MatchTime >= 28f)
        {
            PlayerController target = ClosestAlivePlayerTo(x);
            if (target != null)
            {
                speedX = Mathf.Clamp((target.transform.position.x - x) * 0.42f,
                    -4.2f, 4.2f);
            }
        }

        int coconutCount = 3 + Mathf.FloorToInt((_director.MatchTime - CoconutStartTime) / 18f);
        StartHazardBurst(1, Mathf.Clamp(coconutCount, 5, 9));

        float pressure = Mathf.Clamp01((_director.MatchTime - CoconutStartTime) / 35f);
        _coconutTimer = Mathf.Lerp(0.45f, 0.12f, pressure);
    }

    private void TickExplosiveBananas(float deltaTime)
    {
        if (_director.MatchTime < BananaStartTime)
        {
            return;
        }

        _bananaTimer -= deltaTime;
        if (_bananaTimer > 0f)
        {
            return;
        }

        bool fromLeft = Random.value < 0.5f;
        float x = fromLeft ? -HorizontalRange - 2f : HorizontalRange + 2f;
        float y = _director.ClimberCameraY + Random.Range(-3f, 6f);
        float speedX = fromLeft ? Random.Range(5.5f, 7.5f) : Random.Range(-7.5f, -5.5f);
        if (_director.MatchTime >= 32f)
        {
            PlayerController target = ClosestAlivePlayerTo(y, true);
            if (target != null)
            {
                speedX = Mathf.Clamp((target.transform.position.x - x) * 0.3f,
                    -8.5f, 8.5f);
            }
        }
        int bananaCount = 8 + Mathf.FloorToInt((_director.MatchTime - BananaStartTime) / 24f);
        StartHazardBurst(2, Mathf.Clamp(bananaCount, 8, 14));

        float pressure = Mathf.Clamp01((_director.MatchTime - BananaStartTime) / 30f);
        _bananaTimer = Mathf.Lerp(1.8f, 0.65f, pressure);
    }

    private PlayerController ClosestAlivePlayerTo(float value, bool vertical = false)
    {
        PlayerController closest = null;
        float best = float.MaxValue;
        foreach (var player in _director.GetPlayers())
        {
            if (player == null || player.IsEliminated)
            {
                continue;
            }

            float distance = vertical
                ? Mathf.Abs(player.transform.position.y - value)
                : Mathf.Abs(player.transform.position.x - value);
            if (distance < best)
            {
                best = distance;
                closest = player;
            }
        }
        return closest;
    }

    private void StartHazardBurst(int type, int count)
    {
        count = Mathf.Clamp(count, 6, 16);
        _director.ClimberHazardSequence++;
        _director.ClimberHazardActive = true;
        _director.ClimberHazardType = type;
        _director.ClimberHazardX = -HorizontalRange;
        _director.ClimberHazardY = _director.ClimberCameraY + 9f;
        _director.ClimberHazardStartTime = _director.MatchTime;
    }

    private void RefreshHazardVisual()
    {
        if (_director.ClimberHazardActive)
        {
            if (_renderedHazardSequence != _director.ClimberHazardSequence)
            {
                ClearHazardVisuals();
                _renderedHazardSequence = _director.ClimberHazardSequence;
                BuildHazardBurst();
            }

            float burstElapsed = Mathf.Max(0f, _director.MatchTime - _director.ClimberHazardStartTime);
            for (int i = 0; i < _hazardItems.Count; i++)
            {
                var item = _hazardItems[i];
                float elapsed = Mathf.Max(0f, burstElapsed - item.StartDelay);
                float x = item.X + item.SpeedX * elapsed;
                float y = item.Y - item.FallSpeed * elapsed;
                var visual = _hazardVisuals[i];
                visual.position = new Vector3(x, y, 0f);
                visual.Rotate(0f, 0f, (item.Type == 1 ? 130f : 220f) * Time.deltaTime);

                if (_director.Object.HasStateAuthority)
                {
                    foreach (var player in _director.GetPlayers())
                    {
                        if (!player.IsEliminated && (player.transform.position - visual.position).sqrMagnitude < 0.95f)
                        {
                            EliminatePlayer(player, item.Type == 1
                                ? "le cayo un coco"
                                : "lo golpeo una banana explosiva");
                        }
                    }
                }
            }

            if (_director.Object.HasStateAuthority && burstElapsed > 9f)
            {
                _director.ClimberHazardActive = false;
                ClearHazardVisuals();
            }
        }
        else if (_hazardVisuals.Count > 0)
        {
            ClearHazardVisuals();
        }
    }

    private void BuildHazardBurst()
    {
        bool coconut = _director.ClimberHazardType == 1;
        int count = coconut
            ? Mathf.Clamp(8 + Mathf.FloorToInt(_director.MatchTime / 35f), 8, 14)
            : Mathf.Clamp(10 + Mathf.FloorToInt(_director.MatchTime / 30f), 10, 16);

        for (int i = 0; i < count; i++)
        {
            float seed = _director.RoundSeed + _director.ClimberHazardSequence * 137f + i * 43f;
            float x;
            float y;
            float speedX;
            float fallSpeed;

            if (coconut)
            {
                x = Mathf.Sin(seed * 0.71f) * HorizontalRange;
                y = _director.ClimberHazardY + i * Random.Range(0.35f, 0.72f);
                speedX = Mathf.Sin(seed * 0.29f) * 1.8f;
                fallSpeed = Random.Range(6.2f, 8.8f);
            }
            else
            {
                bool fromLeft = i % 2 == 0;
                x = fromLeft ? -HorizontalRange - 2f : HorizontalRange + 2f;
                y = _director.ClimberHazardY + Mathf.Repeat(i * 3.1f, 14f) - 7f;
                speedX = fromLeft ? Random.Range(6f, 9f) : Random.Range(-9f, -6f);
                fallSpeed = Random.Range(0.4f, 1.2f);
            }

            var item = new ClimberHazardItem
            {
                Sequence = _director.ClimberHazardSequence,
                Type = _director.ClimberHazardType,
                StartTime = _director.ClimberHazardStartTime,
                X = x,
                Y = y,
                SpeedX = speedX,
                FallSpeed = fallSpeed,
                StartDelay = coconut ? i * 0.32f : i * 0.18f,
            };
            _hazardItems.Add(item);

            Sprite sprite = coconut ? _coconutSprite : _explosiveBananaSprite;
            if (sprite == null)
            {
                sprite = CreateHazardSprite(coconut);
            }

            GameObject go = MiniGameWorld.SpriteObject(coconut ? $"CoconutBurst_{i}" : $"ExplosiveBananaBurst_{i}",
                sprite, Vector3.zero, coconut ? new Vector3(0.85f, 0.85f, 1f) : new Vector3(0.72f, 0.72f, 1f),
                "Props", 8);
            go.GetComponent<SpriteRenderer>().color = Color.white;
            _hazardVisuals.Add(go.transform);
        }
    }

    private Sprite CreateHazardSprite(bool coconut)
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float nx = (x - 15.5f) / 15.5f;
                float ny = (y - 15.5f) / 15.5f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                if (radius > 0.92f)
                {
                    continue;
                }

                pixels[y * 32 + x] = coconut
                    ? new Color32(125, 78, 42, 255)
                    : new Color32(214, 74, 48, 255);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    private void EliminatePlayer(PlayerController player, string reason)
    {
        if (player == null || player.IsEliminated)
        {
            return;
        }
        if (_eliminatedInAuthority.Contains(player.Object.InputAuthority.PlayerId))
        {
            return;
        }

        _eliminatedInAuthority.Add(player.Object.InputAuthority.PlayerId);
        player.Eliminate();
        _director.RPC_ShowFeedback($"{player.DisplayName} {reason}", 2);

        if (_director.AllowSoloPractice && _director.GetPlayers().Length == 1)
        {
            _director.DeclareSoloDefeat(reason);
        }
    }

    private static Sprite SpriteFromScene(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<SpriteRenderer>()?.sprite : null;
    }

    private Sprite CreateCoconutSprite()
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float nx = (x - 15.5f) / 15.5f;
                float ny = (y - 15.5f) / 15.5f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                if (radius > 0.92f)
                {
                    continue;
                }

                pixels[y * 32 + x] = radius > 0.78f
                    ? new Color32(83, 49, 30, 255)
                    : new Color32(145, 92, 48, 255);

                if (ny > 0.0f && ny < 0.32f &&
                    (Mathf.Abs(nx) < 0.12f || Mathf.Abs(nx - 0.22f) < 0.09f || Mathf.Abs(nx + 0.22f) < 0.09f))
                {
                    pixels[y * 32 + x] = new Color32(45, 30, 23, 255);
                }
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    private Sprite CreatePlatformSprite()
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var pixels = new Color32[64];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(70, 105, 48, 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
    }
}
