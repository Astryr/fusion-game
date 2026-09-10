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
///  - Esperar a que haya suficientes jugadores y hacer una cuenta regresiva
///    antes de arrancar (<see cref="MatchStarted"/>).
///  - Hacer llover bananas (normales y explosivas) desde arriba del mapa a
///    intervalos al azar, una vez arrancada la partida.
///  - Vigilar el puntaje de todos los jugadores y declarar un ganador.
///
/// Todo el estado que le importa a la UI (<see cref="MatchStarted"/>,
/// <see cref="CountdownRemaining"/>, <see cref="IsGameOver"/>,
/// <see cref="WinnerName"/>) es <c>[Networked]</c>, asi que cualquier
/// cliente puede mostrarlo mirando el mismo estado sin necesitar RPCs.
/// </summary>
public class BananaGameManager : NetworkBehaviour
{
    public static BananaGameManager Instance { get; private set; }

    [Header("Arranque de la partida")]
    [Tooltip("Cantidad minima de jugadores conectados para arrancar la cuenta regresiva.")]
    [SerializeField] private int _minPlayersToStart = 2;
    [Tooltip("Segundos de cuenta regresiva una vez que hay suficientes jugadores.")]
    [SerializeField] private float _countdownDuration = 5f;

    [Header("Puntaje para ganar")]
    [SerializeField] private int _targetScore = 100;

    [Header("Prefabs (deben vivir en una carpeta Resources)")]
    [SerializeField] private string _bananaPrefabName = "Banana";
    [SerializeField] private string _bananaExplosivaPrefabName = "BananaExplosiva";

    [Header("Ritmo de aparicion")]
    [SerializeField] private float _minSpawnInterval = 0.8f;
    [SerializeField] private float _maxSpawnInterval = 1.5f;
    [SerializeField] private float _minFallSpeed = 2.8f;
    [SerializeField] private float _maxFallSpeed = 3.8f;
    [SerializeField, Range(0f, 1f)] private float _explosiveChance = 0.25f;

    /// <summary>Cantidad de jugadores necesarios para arrancar la cuenta regresiva.</summary>
    public int MinPlayersToStart => _minPlayersToStart;

    /// <summary>True una vez que termino la cuenta regresiva y ya llueven bananas.</summary>
    [Networked] public NetworkBool MatchStarted { get; set; }

    /// <summary>
    /// Segundos restantes de cuenta regresiva. Negativo mientras se espera a
    /// que se sumen suficientes jugadores (todavia no arranco la cuenta).
    /// </summary>
    [Networked] public float CountdownRemaining { get; set; }

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

        if (Object.HasStateAuthority)
        {
            CountdownRemaining = -1f;
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
        // un proxy de solo lectura (las propiedades ya vienen replicadas).
        if (!Object.HasStateAuthority || IsGameOver)
        {
            return;
        }

        if (!MatchStarted)
        {
            UpdateWaitingAndCountdown();
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

    /// <summary>
    /// Nadie arranca a jugar (ni caen bananas) hasta que haya al menos
    /// <see cref="_minPlayersToStart"/> jugadores en la sala. Ahi arranca una
    /// cuenta regresiva para darle tiempo a todos de prepararse; si alguien
    /// se va durante la cuenta y vuelve a faltar gente, se cancela sola.
    /// </summary>
    private void UpdateWaitingAndCountdown()
    {
        int playerCount = Runner.SessionInfo != null ? Runner.SessionInfo.PlayerCount : 0;

        if (playerCount < _minPlayersToStart)
        {
            CountdownRemaining = -1f;
            return;
        }

        if (CountdownRemaining < 0f)
        {
            CountdownRemaining = _countdownDuration;
            return;
        }

        CountdownRemaining -= Runner.DeltaTime;
        if (CountdownRemaining <= 0f)
        {
            CountdownRemaining = 0f;
            MatchStarted = true;
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
