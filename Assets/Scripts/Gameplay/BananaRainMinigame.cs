using Fusion;
using UnityEngine;

public class BananaRainMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.BananaRain;

    [SerializeField] private string _bananaPrefabName = "Banana";
    [SerializeField] private string _bananaExplosivaPrefabName = "BananaExplosiva";
    [SerializeField] private float _minSpawnInterval = 0.8f;
    [SerializeField] private float _maxSpawnInterval = 1.5f;
    [SerializeField] private float _minFallSpeed = 2.8f;
    [SerializeField] private float _maxFallSpeed = 3.8f;
    [SerializeField, Range(0f, 1f)] private float _explosiveChance = 0.25f;

    private BananaGameManager _director;
    private NetworkObject _bananaPrefab;
    private NetworkObject _explosivePrefab;
    private float _spawnTimer;
    private bool _overtimeAnnounced;

    public void Setup(BananaGameManager director)
    {
        _director = director;
        _bananaPrefab = Resources.Load<NetworkObject>(_bananaPrefabName);
        _explosivePrefab = Resources.Load<NetworkObject>(_bananaExplosivaPrefabName);
    }

    public void OnCountdownStarted() { }

    public void OnMatchStarted()
    {
        _spawnTimer = _minSpawnInterval;
        _overtimeAnnounced = false;
        foreach (var player in _director.GetPlayers())
        {
            player.SetControlMode(PlayerControlMode.Platformer);
            player.SetWorldBounds(-BananaRushConfig.PlayerClampX, BananaRushConfig.PlayerClampX, BananaRushConfig.GroundTopY, -8f);
            player.SetPushEnabled(false);
        }
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!hasAuthority)
        {
            return;
        }

        if (_director.MatchTime >= BananaRushConfig.BananaRegularDuration && _director.OvertimeRemaining < 0f)
        {
            _director.OvertimeRemaining = BananaRushConfig.BananaOvertimeDuration;
            if (!_overtimeAnnounced)
            {
                _overtimeAnnounced = true;
                _director.RPC_ShowFeedback("¡Corte! 30 segundos", 2);
            }
        }

        if (_director.OvertimeRemaining >= 0f)
        {
            _director.OvertimeRemaining -= deltaTime;
            if (_director.OvertimeRemaining <= 0f)
            {
                _director.DeclareHighestScoreWinner("mejor puntaje al corte");
                return;
            }
        }

        foreach (var player in _director.GetPlayers())
        {
            if (player.Score >= BananaRushConfig.BananaTargetScore)
            {
                _director.DeclareWinner(player, "100 puntos");
                return;
            }
        }

        float pressure = Mathf.Clamp01(_director.MatchTime / BananaRushConfig.BananaRegularDuration);
        float minInterval = Mathf.Lerp(_minSpawnInterval, 0.22f, pressure);
        float maxInterval = Mathf.Lerp(_maxSpawnInterval, 0.4f, pressure);

        _spawnTimer -= deltaTime;
        if (_spawnTimer > 0f)
        {
            return;
        }

        int burst = pressure > 0.65f ? 2 : 1;
        if (pressure > 0.28f && Random.value < pressure * 0.45f)
        {
            burst++;
        }

        for (int i = 0; i < burst; i++)
        {
            SpawnBanana(pressure);
        }

        _spawnTimer = Random.Range(minInterval, maxInterval);
    }

    public void OnMatchEnded() { }

    public void Cleanup() { }

    private void SpawnBanana(float pressure)
    {
        bool green = Random.value < BananaRushConfig.BananaGreenChance;
        bool explosive = !green && Random.value < _explosiveChance;
        NetworkObject prefab = explosive ? _explosivePrefab : _bananaPrefab;
        if (prefab == null)
        {
            return;
        }

        float minFall = Mathf.Lerp(_minFallSpeed, 5.2f, pressure);
        float maxFall = Mathf.Lerp(_maxFallSpeed, 7.2f, pressure);
        float fallSpeed = Random.Range(minFall, maxFall);
        int points = explosive ? BananaRushConfig.BananaExplosivePoints : BananaRushConfig.BananaNormalPoints;
        if (green)
        {
            points = BananaRushConfig.BananaGreenPoints;
            fallSpeed *= 0.72f;
        }

        float x = Random.Range(-BananaRushConfig.BananaSpawnX, BananaRushConfig.BananaSpawnX);
        Vector3 position = new Vector3(x, BananaRushConfig.BananaSpawnY, 0f);
        NetworkObject spawned = _director.Runner.Spawn(prefab, position, Quaternion.identity);
        if (spawned != null && spawned.TryGetComponent(out BananaController banana))
        {
            banana.Configure(fallSpeed, points);
        }
    }
}
