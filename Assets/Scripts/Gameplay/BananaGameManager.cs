using Fusion;
using UnityEngine;

/// <summary>
/// Autoridad de la partida en Shared Mode. La spawnea unicamente el Master
/// Client (ver <see cref="NetworkRunnerHandler"/>) con el flag
/// <c>SharedModeStateAuthMasterClient</c>, asi que si ese jugador se
/// desconecta la autoridad de este objeto pasa sola al nuevo Master Client
/// (no hace falta re-spawnearlo).
///
/// Responsabilidades:
///  - Hacer llover bananas (normales y explosivas) desde arriba del mapa a
///    intervalos al azar.
///  - Vigilar el puntaje de todos los jugadores y declarar un ganador.
///
/// El resultado (<see cref="IsGameOver"/> / <see cref="WinnerName"/>) es
/// <c>[Networked]</c>, asi que cualquier cliente puede mostrar la pantalla
/// de victoria mirando el mismo estado sin necesitar RPCs adicionales.
/// </summary>
public class BananaGameManager : NetworkBehaviour
{
    public static BananaGameManager Instance { get; private set; }

    [Header("Puntaje para ganar")]
    [SerializeField] private int _targetScore = 30;

    [Header("Prefabs (deben vivir en una carpeta Resources)")]
    [SerializeField] private string _bananaPrefabName = "Banana";
    [SerializeField] private string _bananaExplosivaPrefabName = "BananaExplosiva";

    [Header("Ritmo de aparicion")]
    [SerializeField] private float _minSpawnInterval = 0.8f;
    [SerializeField] private float _maxSpawnInterval = 1.5f;
    [SerializeField] private float _minFallSpeed = 3.5f;
    [SerializeField] private float _maxFallSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float _explosiveChance = 0.25f;

    [Networked] public NetworkBool IsGameOver { get; set; }
    [Networked] public NetworkString<_32> WinnerName { get; set; }

    private NetworkObject _bananaPrefab;
    private NetworkObject _bananaExplosivaPrefab;
    private float _spawnTimer;

    public override void Spawned()
    {
        Instance = this;
        _bananaPrefab = Resources.Load<NetworkObject>(_bananaPrefabName);
        _bananaExplosivaPrefab = Resources.Load<NetworkObject>(_bananaExplosivaPrefabName);

        if (_bananaPrefab == null || _bananaExplosivaPrefab == null)
        {
            Debug.LogError("[BananaGameManager] Faltan los prefabs de banana en Resources.");
        }

        _spawnTimer = _minSpawnInterval;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Solo quien tiene la autoridad (el Master Client) hace correr la
        // logica de la partida; en el resto de los clientes este objeto es
        // un proxy de solo lectura (IsGameOver / WinnerName ya replicados).
        if (!Object.HasStateAuthority || IsGameOver)
        {
            return;
        }

        CheckForWinner();
        if (IsGameOver)
        {
            return;
        }

        _spawnTimer -= Runner.DeltaTime;
        if (_spawnTimer <= 0f)
        {
            SpawnBanana();
            _spawnTimer = Random.Range(_minSpawnInterval, _maxSpawnInterval);
        }
    }

    private void CheckForWinner()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.Score >= _targetScore)
            {
                IsGameOver = true;
                WinnerName = player.Nickname;
                return;
            }
        }
    }

    private void SpawnBanana()
    {
        bool explosive = Random.value < _explosiveChance;
        NetworkObject prefab = explosive ? _bananaExplosivaPrefab : _bananaPrefab;
        if (prefab == null)
        {
            return;
        }

        float x = Random.Range(-BananaRushConfig.BananaSpawnX, BananaRushConfig.BananaSpawnX);
        Vector3 position = new Vector3(x, BananaRushConfig.BananaSpawnY, 0f);
        float fallSpeed = Random.Range(_minFallSpeed, _maxFallSpeed);

        NetworkObject spawned = Runner.Spawn(prefab, position, Quaternion.identity);
        if (spawned != null && spawned.TryGetComponent(out BananaController banana))
        {
            banana.Configure(fallSpeed);
        }
    }
}
