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

        _spawnTimer -= deltaTime;
        if (_spawnTimer > 0f)
        {
            return;
        }

        SpawnBanana();
        _spawnTimer = Random.Range(_minSpawnInterval, _maxSpawnInterval);
    }

    public void OnMatchEnded() { }

    public void Cleanup() { }

    private void SpawnBanana()
    {
        bool explosive = Random.value < _explosiveChance;
        NetworkObject prefab = explosive ? _explosivePrefab : _bananaPrefab;
        if (prefab == null)
        {
            return;
        }

        float x = Random.Range(-BananaRushConfig.BananaSpawnX, BananaRushConfig.BananaSpawnX);
        Vector3 position = new Vector3(x, BananaRushConfig.BananaSpawnY, 0f);
        float fallSpeed = Random.Range(_minFallSpeed, _maxFallSpeed);

        NetworkObject spawned = _director.Runner.Spawn(prefab, position, Quaternion.identity);
        if (spawned != null && spawned.TryGetComponent(out BananaController banana))
        {
            banana.Configure(fallSpeed);
        }
    }
}
