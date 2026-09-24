using Fusion;
using UnityEngine;

public class LogSurviveMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.LogSurvive;

    private const float StartHalfWidth = 5.5f;
    private const float MinHalfWidth = 1.15f;
    private const float ShrinkDuration = 80f;
    private const float PeelStartTime = 45f;

    private BananaGameManager _director;
    private NetworkObject _peelPrefab;
    private float _peelTimer;
    private bool _started;

    public void Setup(BananaGameManager director)
    {
        _director = director;
        _peelPrefab = Resources.Load<NetworkObject>("BananaExplosiva");
    }

    public void OnMatchStarted()
    {
        MiniGameWorld.Clear();
        _started = true;
        _peelTimer = 2f;
        BuildLogVisual();

        int index = 0;
        foreach (var player in _director.GetPlayers())
        {
            float x = -2.4f + index * 1.6f;
            if (_director.Object.HasStateAuthority)
            {
                player.Teleport(new Vector3(x, BananaRushConfig.GroundTopY, 0f));
            }
            player.SetControlMode(PlayerControlMode.Platformer);
            player.SetPushEnabled(true);
            player.SetWorldBounds(-StartHalfWidth, StartHalfWidth, BananaRushConfig.GroundTopY, -8f);
            index++;
        }

        if (_director.Object.HasStateAuthority)
        {
            _director.LogHalfWidth = StartHalfWidth;
        }
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!_started)
        {
            return;
        }

        float t = Mathf.Clamp01(_director.MatchTime / ShrinkDuration);
        float half = Mathf.Lerp(StartHalfWidth, MinHalfWidth, t);
        if (hasAuthority)
        {
            _director.LogHalfWidth = half;
        }

        UpdateLogVisual(_director.LogHalfWidth);

        foreach (var player in _director.GetPlayers())
        {
            if (player.IsEliminated)
            {
                continue;
            }

            player.SetWorldBounds(-_director.LogHalfWidth, _director.LogHalfWidth, BananaRushConfig.GroundTopY, -8f);
            if (Mathf.Abs(player.transform.position.x) > _director.LogHalfWidth + 0.05f)
            {
                player.SetWorldBounds(-BananaRushConfig.PlayerClampX, BananaRushConfig.PlayerClampX, -20f, -4f);
                player.Eliminate();
                _director.RPC_ShowFeedback($"{player.DisplayName} se cayo del tronco", 2);
            }
        }

        if (!hasAuthority)
        {
            return;
        }

        if (_director.MatchTime >= PeelStartTime)
        {
            _peelTimer -= deltaTime;
            if (_peelTimer <= 0f)
            {
                SpawnPeel();
                _peelTimer = Random.Range(1.6f, 2.8f);
            }
        }

        int alive = 0;
        PlayerController last = null;
        foreach (var player in _director.GetPlayers())
        {
            if (!player.IsEliminated)
            {
                alive++;
                last = player;
            }
        }

        if (alive <= 1 && _director.GetPlayers().Length > 1)
        {
            _director.DeclareWinner(last, "ultimo en el tronco");
        }
        else if (_director.MatchTime >= ShrinkDuration + 15f)
        {
            _director.DeclareWinner(last, "aguanto mas");
        }
    }

    public void OnMatchEnded()
    {
        _started = false;
        foreach (var player in _director.GetPlayers())
        {
            player.SetPushEnabled(false);
        }
    }

    public void Cleanup()
    {
        _started = false;
        MiniGameWorld.Clear();
    }

    private void BuildLogVisual()
    {
        Sprite dirt = SpriteFromScene("DirtFill") ?? SpriteFromScene("GrassTop");
        if (dirt == null)
        {
            return;
        }

        MiniGameWorld.SpriteObject("Log", dirt, new Vector3(0f, -0.15f, 0f), new Vector3(StartHalfWidth * 2.2f, 0.55f, 1f), "Ground", 6);
    }

    private void UpdateLogVisual(float halfWidth)
    {
        Transform log = MiniGameWorld.GetRoot().Find("Log");
        if (log != null)
        {
            log.localScale = new Vector3(halfWidth * 2.2f, 0.55f, 1f);
        }
    }

    private void SpawnPeel()
    {
        if (_peelPrefab == null)
        {
            return;
        }

        float x = Random.Range(-_director.LogHalfWidth * 0.9f, _director.LogHalfWidth * 0.9f);
        var spawned = _director.Runner.Spawn(_peelPrefab, new Vector3(x, BananaRushConfig.BananaSpawnY, 0f), Quaternion.identity);
        if (spawned != null && spawned.TryGetComponent(out BananaController banana))
        {
            banana.Configure(4.2f);
            banana.BecomeStunPeel(3f);
        }
    }

    private static Sprite SpriteFromScene(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<SpriteRenderer>()?.sprite : null;
    }
}
