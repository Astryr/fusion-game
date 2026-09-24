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
    private readonly HashSet<int> _finishedIds = new HashSet<int>();

    public void Setup(BananaGameManager director)
    {
        _director = director;
        _groundSprite = SpriteFromScene("GrassTop") ?? SpriteFromPrefab("Banana");
        _dirtSprite = SpriteFromScene("DirtFill") ?? SpriteFromPrefab("BananaExplosiva");
        _flagSprite = SpriteFromPrefab("Banana");
    }

    public void OnMatchStarted()
    {
        MiniGameWorld.Clear();
        _finishedIds.Clear();
        BuildCourse();
        _started = true;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = 6.5f;
        }

        var players = _director.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            float x = BananaRushConfig.ParkourStartX + i * 1.1f;
            if (_director.Object.HasStateAuthority)
            {
                players[i].Teleport(new Vector3(x, 1.2f, 0f));
            }
            players[i].SetControlMode(PlayerControlMode.Platformer);
            players[i].SetWorldBounds(BananaRushConfig.ParkourMinX, BananaRushConfig.ParkourMaxX, -20f, -6f);
            players[i].SetPushEnabled(false);
        }

        if (_director.Object.HasStateAuthority)
        {
            _director.AvalancheX = BananaRushConfig.ParkourMinX - 2f;
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

        UpdateAvalancheVisual();
        FollowLocalPlayer();
    }

    public void OnMatchEnded()
    {
        _started = false;
    }

    public void Cleanup()
    {
        _started = false;
        MiniGameWorld.Clear();
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

        PlacePlatform("Start", new Vector3(-2f, 0f, 0f), new Vector3(8f, 1f, 1f), platform);
        float[] xs = { 6f, 11f, 16.5f, 22f, 27.5f, 33f, 38f };
        float[] ys = { 0.4f, 1.6f, 0.2f, 1.8f, 0.6f, 1.4f, 0.3f };
        for (int i = 0; i < xs.Length; i++)
        {
            PlacePlatform($"Plat_{i}", new Vector3(xs[i], ys[i], 0f), new Vector3(3.4f, 0.7f, 1f), platform);
        }

        GameObject finish = MiniGameWorld.SpriteObject("Finish", _flagSprite != null ? _flagSprite : platform,
            new Vector3(BananaRushConfig.ParkourFinishX, 2.4f, 0f), Vector3.one, "Props", 5);
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
        Transform root = MiniGameWorld.GetRoot();
        Transform wall = root.Find("Avalanche");
        if (wall == null)
        {
            Sprite dirt = _dirtSprite != null ? _dirtSprite : CreateFallbackSprite(new Color(0.35f, 0.12f, 0.08f));
            GameObject go = MiniGameWorld.SpriteObject("Avalanche", dirt, Vector3.zero, new Vector3(3.5f, 16f, 1f), "Props", 8);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.color = new Color(0.75f, 0.15f, 0.1f, 0.85f);
            wall = go.transform;
        }

        wall.position = new Vector3(_director.AvalancheX - 1.2f, 4f, 0f);
    }

    private static void FollowLocalPlayer()
    {
        if (PlayerController.Local == null || Camera.main == null)
        {
            return;
        }

        Vector3 target = PlayerController.Local.transform.position;
        float x = Mathf.Clamp(target.x, 0f, BananaRushConfig.ParkourFinishX - 4f);
        Vector3 next = new Vector3(x, Mathf.Max(3f, target.y + 2f), -10f);
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, next, 8f * Time.deltaTime);
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
