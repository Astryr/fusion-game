using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ParkourMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.Parkour;

    private const int AvalancheColumns = 8;
    private const int AvalancheRows = 18;

    private BananaGameManager _director;
    private Sprite _groundSprite;
    private Sprite _dirtSprite;
    private Sprite _flagSprite;
    private Sprite _avalancheSprite;
    private Texture2D _avalancheTexture;
    private Transform _avalancheRoot;
    private Transform[] _coconutPieces;
    private Vector3[] _coconutBasePositions;
    private float[] _coconutFallSpeeds;
    private float[] _coconutSwayPhases;
    private float[] _coconutSpinSpeeds;
    private bool _started;
    private bool _courseReady;
    private readonly HashSet<int> _finishedIds = new HashSet<int>();

    public void Setup(BananaGameManager director)
    {
        _director = director;
        _groundSprite = SpriteFromScene("GrassTop") ?? SpriteFromPrefab("Banana");
        _dirtSprite = SpriteFromScene("DirtFill") ?? SpriteFromPrefab("BananaExplosiva");
        _flagSprite = SpriteFromPrefab("Banana");
    }

    public void OnCountdownStarted()
    {
        PrepareCourse(hideLobbyGround: false);
        PlacePlayers(PlayerControlMode.Disabled, teleport: true);
        StageBackdrop.BeginScrollingStage(BananaRushConfig.ParkourMinX, BananaRushConfig.ParkourMaxX);
    }

    public void OnMatchStarted()
    {
        if (!_courseReady)
        {
            PrepareCourse(hideLobbyGround: false);
            PlacePlayers(PlayerControlMode.Disabled, teleport: true);
            StageBackdrop.BeginScrollingStage(BananaRushConfig.ParkourMinX, BananaRushConfig.ParkourMaxX);
        }

        bool atStart = _director.MatchTime < 0.35f;
        PlacePlayers(PlayerControlMode.Platformer, teleport: atStart);
        _started = true;
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!_courseReady)
        {
            return;
        }

        if (!_started)
        {
            FollowRaceCamera();
            if (Camera.main != null)
            {
                StageBackdrop.Tick(Camera.main.transform.position);
            }

            return;
        }

        if (hasAuthority)
        {
            if (_director.MatchTime > 2f)
            {
                _director.AvalancheX += BananaRushConfig.ParkourAvalancheSpeed * deltaTime;
            }

            int finished = 0;
            int alive = 0;
            PlayerController leader = null;
            foreach (var player in _director.GetPlayers())
            {
                if (player.IsEliminated)
                {
                    continue;
                }

                alive++;
                if (player.transform.position.x >= BananaRushConfig.ParkourFinishX)
                {
                    int id = player.Object.InputAuthority.PlayerId;
                    if (_finishedIds.Add(id))
                    {
                        int place = _finishedIds.Count;
                        player.RPC_SetScore(Mathf.Max(10, 110 - place * 25));
                        _director.RPC_ShowFeedback($"{player.DisplayName} llego #{place}", 0);
                    }

                    finished++;
                }
                else if (player.transform.position.x < _director.AvalancheX)
                {
                    player.Eliminate();
                    _director.RPC_ShowFeedback($"{player.DisplayName} lo trago la avalancha", 2);
                }
                else if (leader == null || player.transform.position.x > leader.transform.position.x)
                {
                    leader = player;
                }
            }

            int total = _director.GetPlayers().Length;
            bool everyoneDone = total > 0 && (finished + (total - alive) >= total);
            bool scoresReady = true;
            foreach (var player in _director.GetPlayers())
            {
                if (!player.IsEliminated && player.transform.position.x >= BananaRushConfig.ParkourFinishX && player.Score == 0)
                {
                    scoresReady = false;
                }
            }

            if (alive == 0 || (everyoneDone && finished > 0 && scoresReady))
            {
                _director.DeclareHighestScoreWinner("llego primero");
            }
            else if (alive == 1 && finished == 0 && _director.GetPlayers().Length > 1)
            {
                _director.DeclareWinner(leader, "ultimo en pie");
            }
        }

        FollowRaceCamera();
        UpdateAvalancheVisual();
        if (Camera.main != null)
        {
            StageBackdrop.Tick(Camera.main.transform.position);
        }
    }

    public void OnMatchEnded()
    {
        _started = false;
        foreach (var player in _director.GetPlayers())
        {
            if (player != null && player.IsSpawned)
            {
                player.SetControlMode(PlayerControlMode.Disabled);
            }
        }
    }

    public void Cleanup()
    {
        _started = false;
        _courseReady = false;
        MiniGameWorld.Clear();
        ResetAvalancheVisuals();
        ReleaseAvalancheAsset();
        StageBackdrop.Clear();
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = BananaRushConfig.CameraOrthographicSize;
            cam.transform.position = new Vector3(0f, BananaRushConfig.CameraCenterY, -10f);
        }
    }

    private void BuildCourse()
    {
        Sprite platform = _groundSprite != null ? _groundSprite : _dirtSprite;
        if (platform == null)
        {
            platform = CreateFallbackSprite(new Color(0.45f, 0.32f, 0.18f));
        }

        PlacePlatform("Start", new Vector3(BananaRushConfig.ParkourStartX + 4f, 0f, 0f), new Vector3(16f, 1.2f, 1f), platform);

        float x = 6.2f;
        int index = 0;
        while (x < BananaRushConfig.ParkourFinishX - 5f)
        {
            float y = 0.2f + Mathf.Abs(Mathf.Sin(index * 1.35f)) * 1.7f;
            float width = 3.15f + (index % 3) * 0.3f;
            PlacePlatform($"Plat_{index}", new Vector3(x, y, 0f), new Vector3(width, 0.7f, 1f), platform);
            x += 4.7f + (index % 4) * 0.28f;
            index++;
        }

        PlacePlatform("FinishPad", new Vector3(BananaRushConfig.ParkourFinishX, 0.15f, 0f), new Vector3(7f, 0.85f, 1f), platform);
        GameObject finish = MiniGameWorld.SpriteObject("Finish", _flagSprite != null ? _flagSprite : platform,
            new Vector3(BananaRushConfig.ParkourFinishX, 2.5f, 0f), Vector3.one, "Props", 5);
        finish.name = "FinishFlag";
    }

    private static void PlacePlatform(string name, Vector3 position, Vector3 scale, Sprite sprite)
    {
        GameObject go = MiniGameWorld.SpriteObject(name, sprite, position, scale, "Ground", 1);
        go.layer = LayerMask.NameToLayer("Ground");
        var box = go.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
    }

    private void UpdateAvalancheVisual()
    {
        Camera cam = Camera.main;
        float camY = cam != null ? cam.transform.position.y : 4f;
        float height = cam != null ? cam.orthographicSize * 2.6f : 20f;
        float width = BananaRushConfig.ParkourAvalancheWidth;

        if (_avalancheRoot == null)
        {
            BuildAvalancheVisual(width, height);
        }

        _avalancheRoot.position = new Vector3(_director.AvalancheX - width * 0.35f, camY, 0f);
        float time = Time.time;
        float spacingY = height / AvalancheRows;
        for (int i = 0; i < _coconutPieces.Length; i++)
        {
            Vector3 basePosition = _coconutBasePositions[i];
            float distanceFromTop = height * 0.5f - basePosition.y;
            float fall = Mathf.Repeat(distanceFromTop + time * _coconutFallSpeeds[i], height);
            float sway = Mathf.Sin(time * 2.4f + _coconutSwayPhases[i]) * spacingY * 0.12f;
            _coconutPieces[i].localPosition = new Vector3(basePosition.x + sway, height * 0.5f - fall, basePosition.z);
            _coconutPieces[i].localRotation = Quaternion.Euler(0f, 0f, time * _coconutSpinSpeeds[i] + _coconutSwayPhases[i] * 35f);
        }
    }

    private void BuildAvalancheVisual(float width, float height)
    {
        if (_avalancheSprite == null)
        {
            _avalancheTexture = CreateCoconutSpriteTexture();
            _avalancheSprite = Sprite.Create(_avalancheTexture,
                new Rect(0f, 0f, _avalancheTexture.width, _avalancheTexture.height),
                new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
        }

        var root = new GameObject("CoconutAvalanche");
        root.transform.SetParent(MiniGameWorld.GetRoot(), false);
        _avalancheRoot = root.transform;

        int count = AvalancheColumns * AvalancheRows;
        _coconutPieces = new Transform[count];
        _coconutBasePositions = new Vector3[count];
        _coconutFallSpeeds = new float[count];
        _coconutSwayPhases = new float[count];
        _coconutSpinSpeeds = new float[count];

        float spacingX = width / AvalancheColumns;
        float spacingY = height / AvalancheRows;
        int index = 0;
        for (int row = 0; row < AvalancheRows; row++)
        {
            for (int column = 0; column < AvalancheColumns; column++)
            {
                var piece = new GameObject($"Coconut_{index}");
                piece.transform.SetParent(_avalancheRoot, false);
                piece.transform.localPosition = new Vector3(
                    -width * 0.5f + (column + 0.5f) * spacingX + Random.Range(-spacingX * 0.12f, spacingX * 0.12f),
                    -height * 0.5f + (row + 0.5f) * spacingY,
                    Random.Range(-0.02f, 0.02f));
                float size = Mathf.Min(spacingX, spacingY) * Random.Range(1.02f, 1.22f);
                piece.transform.localScale = Vector3.one * size;
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));

                var renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = _avalancheSprite;
                renderer.color = Color.Lerp(new Color(0.74f, 0.71f, 0.64f), Color.white, Random.Range(0.1f, 0.45f));
                renderer.sortingLayerName = "Props";
                renderer.sortingOrder = 12 + Random.Range(0, 3);

                _coconutPieces[index] = piece.transform;
                _coconutBasePositions[index] = piece.transform.localPosition;
                _coconutFallSpeeds[index] = Random.Range(2.6f, 5.2f);
                _coconutSwayPhases[index] = Random.Range(0f, Mathf.PI * 2f);
                _coconutSpinSpeeds[index] = Random.Range(-100f, 100f);
                index++;
            }
        }
    }

    private void ReleaseAvalancheAsset()
    {
        if (_avalancheSprite != null)
        {
            Destroy(_avalancheSprite);
            _avalancheSprite = null;
        }

        if (_avalancheTexture != null)
        {
            Destroy(_avalancheTexture);
            _avalancheTexture = null;
        }
    }

    private void ResetAvalancheVisuals()
    {
        _avalancheRoot = null;
        _coconutPieces = null;
        _coconutBasePositions = null;
        _coconutFallSpeeds = null;
        _coconutSwayPhases = null;
        _coconutSpinSpeeds = null;
    }

    private static Texture2D CreateCoconutSpriteTexture()
    {
        const int size = 64;
        const float radiusX = 0.83f;
        const float radiusY = 0.88f;
        var pixels = new Color32[size * size];
        var random = new System.Random(4817);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - (size - 1) * 0.5f) / (size * 0.5f);
                float ny = (y - (size - 1) * 0.5f) / (size * 0.5f);
                float angle = Mathf.Atan2(ny, nx);
                float edge = 0.84f + Mathf.Sin(angle * 5f + 0.7f) * 0.045f + Mathf.Sin(angle * 9f) * 0.025f;
                float radius = Mathf.Sqrt(nx * nx / (radiusX * radiusX) + ny * ny / (radiusY * radiusY));
                if (radius > edge)
                {
                    continue;
                }

                float fibers = Mathf.Sin(nx * 48f + ny * 17f) * 0.45f +
                               Mathf.Sin(nx * 19f - ny * 37f) * 0.3f +
                               Mathf.Sin((nx + ny) * 73f) * 0.15f;
                Color32 color = fibers > 0.38f
                    ? new Color32(157, 105, 61, 255)
                    : fibers < -0.42f
                        ? new Color32(79, 45, 28, 255)
                        : new Color32(116, 69, 40, 255);
                if (radius > edge - 0.1f)
                {
                    color = new Color32(54, 33, 25, 255);
                }
                else if (nx < -0.22f && ny < 0.48f)
                {
                    color = new Color32(173, 119, 68, 255);
                }

                bool eyeBand = ny > 0.02f && ny < 0.38f;
                bool eye = eyeBand &&
                           ((nx + 0.2f) * (nx + 0.2f) < 0.0025f || nx * nx < 0.0025f || (nx - 0.2f) * (nx - 0.2f) < 0.0025f);
                if (eye)
                {
                    color = new Color32(37, 25, 22, 255);
                }
                else if (eyeBand &&
                         ((nx + 0.2f) * (nx + 0.2f) < 0.007f || nx * nx < 0.007f || (nx - 0.2f) * (nx - 0.2f) < 0.007f))
                {
                    color = new Color32(187, 139, 84, 255);
                }

                if (random.NextDouble() < 0.025 && radius < edge - 0.14f)
                {
                    color = new Color32(83, 49, 31, 255);
                }

                pixels[y * size + x] = color;
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Parkour_Coconut"
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private void PrepareCourse(bool hideLobbyGround)
    {
        MiniGameWorld.Clear();
        ResetAvalancheVisuals();
        _finishedIds.Clear();
        BuildCourse();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = 6.5f;
            cam.transform.position = new Vector3(BananaRushConfig.ParkourStartX + 4f, 4f, -10f);
        }

        if (hideLobbyGround)
        {
            StageBackdrop.BeginScrollingStage(BananaRushConfig.ParkourMinX, BananaRushConfig.ParkourMaxX);
        }

        if (_director.Object.HasStateAuthority)
        {
            _director.AvalancheX = BananaRushConfig.ParkourMinX - 2f;
        }

        _courseReady = true;
        _started = false;
    }

    private void PlacePlayers(PlayerControlMode mode, bool teleport)
    {
        var players = _director.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            if (!players[i].IsSpawned)
            {
                continue;
            }

            if (teleport)
            {
                float x = BananaRushConfig.ParkourStartX + 1.2f + i * 1.15f;
                players[i].Teleport(new Vector3(x, BananaRushConfig.ParkourSpawnY, 0f));
            }

            players[i].SetControlMode(mode);
            players[i].SetWorldBounds(BananaRushConfig.ParkourMinX, BananaRushConfig.ParkourMaxX, -20f, -6f);
            players[i].SetPushEnabled(false);
        }
    }

    private static void FollowRaceCamera()
    {
        if (Camera.main == null)
        {
            return;
        }

        PlayerController target = FindCameraTarget();
        if (target == null)
        {
            return;
        }

        Vector3 focus = target.transform.position;
        float minCamX = BananaRushConfig.ParkourStartX + 2f;
        float maxCamX = BananaRushConfig.ParkourFinishX - 3f;
        float x = Mathf.Clamp(focus.x, minCamX, maxCamX);
        Vector3 next = new Vector3(x, Mathf.Max(3f, focus.y + 2f), -10f);
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, next, 8f * Time.deltaTime);
    }

    private static PlayerController FindCameraTarget()
    {
        PlayerController local = PlayerController.Local;
        if (local != null && local.IsSpawned && !local.IsEliminated)
        {
            return local;
        }

        PlayerController leader = null;
        foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player == null || !player.IsSpawned || player.IsEliminated)
            {
                continue;
            }

            if (leader == null || player.transform.position.x > leader.transform.position.x)
            {
                leader = player;
            }
        }

        return leader != null ? leader : local;
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

    private static Sprite CreateFallbackSprite(Color color)
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
    }
}
