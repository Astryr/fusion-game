using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ParkourMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.Parkour;

    private BananaGameManager _director;
    private Sprite _groundSprite;
    private Sprite _dirtSprite;
    private Sprite _flagSprite;
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
        PrepareCourse();
        PlacePlayers(PlayerControlMode.Disabled, teleport: true);
    }

    public void OnMatchStarted()
    {
        if (!_courseReady)
        {
            PrepareCourse();
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
    }

    public void Cleanup()
    {
        _started = false;
        _courseReady = false;
        MiniGameWorld.Clear();
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
        go.AddComponent<BoxCollider2D>();
    }

    private void UpdateAvalancheVisual()
    {
        Camera cam = Camera.main;
        float camY = cam != null ? cam.transform.position.y : 4f;
        float height = cam != null ? cam.orthographicSize * 2.6f : 20f;
        float width = BananaRushConfig.ParkourAvalancheWidth;

        Transform root = MiniGameWorld.GetRoot();
        Transform wall = root.Find("Avalanche");
        if (wall == null)
        {
            Sprite slab = CreateFallbackSprite(Color.white);
            GameObject go = MiniGameWorld.SpriteObject("Avalanche", slab, Vector3.zero, Vector3.one, "Props", 12);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 1f, 1f, 0.94f);
            wall = go.transform;
        }

        wall.localScale = new Vector3(width, height, 1f);
        wall.position = new Vector3(_director.AvalancheX - width * 0.35f, camY, 0f);
    }

    private void PrepareCourse()
    {
        MiniGameWorld.Clear();
        _finishedIds.Clear();
        BuildCourse();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = 6.5f;
            cam.transform.position = new Vector3(BananaRushConfig.ParkourStartX + 4f, 4f, -10f);
        }

        StageBackdrop.BeginScrollingStage(BananaRushConfig.ParkourMinX, BananaRushConfig.ParkourMaxX);
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

            if (teleport && _director.Object.HasStateAuthority)
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
