using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Punto central de conexion a Photon Fusion 2.
///
/// Vive en la escena <c>MainMenu</c> y sobrevive al cambio de escena
/// (DontDestroyOnLoad) para seguir recibiendo callbacks de red mientras
/// se juega en <c>Game</c>. Usa <see cref="GameMode.Shared"/> porque es el
/// modo mas simple para arrancar (sin necesidad de manejar host dedicado ni
/// reconciliacion), ideal para esta primera entrega. Ver JR4/JR5 (Photon,
/// autoridad e input de red) para profundizar y, si el equipo lo necesita
/// mas adelante, migrar a Host/Server mode.
/// </summary>
public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkRunnerHandler Instance { get; private set; }

    [Header("Configuracion")]
    [Tooltip("Nombre del prefab del jugador, ubicado dentro de una carpeta Resources.")]
    [SerializeField] private string _playerPrefabResourcePath = "Player";

    [Tooltip("Build Index de la escena de juego (debe estar registrada en Build Settings).")]
    [SerializeField] private int _gameSceneBuildIndex = 1;

    [Tooltip("Sesion por defecto si el campo de texto del menu queda vacio.")]
    [SerializeField] private string _defaultSessionName = "SalaPrincipal";

    public NetworkRunner Runner { get; private set; }
    public bool IsConnected { get; private set; }
    public string CurrentSessionName { get; private set; }

    public event Action OnConnectingEvent;
    public event Action OnConnectedEvent;
    public event Action<string> OnConnectionFailedEvent;
    public event Action OnDisconnectedEvent;
    public event Action<int> OnPlayerCountChangedEvent;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkObject _playerPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Crea (si no existe) o une a la sesion Shared con el nombre indicado.
    /// Al ser Shared Mode, no hay distincion real entre "host" y "cliente":
    /// el primero en usar ese nombre de sala la crea, el resto se une.
    /// </summary>
    public async Task<bool> ConnectAsync(string sessionName)
    {
        if (Runner != null)
        {
            Debug.LogWarning("[NetworkRunnerHandler] Ya hay una conexion activa.");
            return false;
        }

        sessionName = string.IsNullOrWhiteSpace(sessionName) ? _defaultSessionName : sessionName.Trim();

        OnConnectingEvent?.Invoke();

        _playerPrefab = Resources.Load<NetworkObject>(_playerPrefabResourcePath);
        if (_playerPrefab == null)
        {
            Debug.LogError($"[NetworkRunnerHandler] No se encontro '{_playerPrefabResourcePath}' dentro de una carpeta Resources. " +
                            "Revisa SETUP.md -> 'Crear el prefab del jugador'.");
        }

        Runner = gameObject.AddComponent<NetworkRunner>();
        Runner.ProvideInput = true;
        Runner.AddCallbacks(this);

        var sceneRef = SceneRef.FromIndex(_gameSceneBuildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (sceneRef.IsValid)
        {
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);
        }

        StartGameResult result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            Scene = sceneInfo,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
        });

        if (result.Ok)
        {
            CurrentSessionName = sessionName;
            IsConnected = true;
            OnConnectedEvent?.Invoke();
            return true;
        }

        Debug.LogError($"[NetworkRunnerHandler] Fallo la conexion: {result.ShutdownReason}");
        OnConnectionFailedEvent?.Invoke(result.ShutdownReason.ToString());

        if (Runner != null)
        {
            Destroy(Runner);
            Runner = null;
        }

        return false;
    }

    /// <summary>Corta la conexion actual (boton "Salir" del HUD).</summary>
    public void Disconnect()
    {
        Runner?.Shutdown();
    }

    private static Vector3 GetSpawnPosition(int playerIndex)
    {
        const float spacing = 2.5f;
        const int columns = 4;
        int row = playerIndex / columns;
        int col = playerIndex % columns;
        return new Vector3((col - (columns - 1) / 2f) * spacing, row * spacing, 0f);
    }

    // ---------------------------------------------------------------
    // INetworkRunnerCallbacks
    // ---------------------------------------------------------------

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[NetworkRunnerHandler] Jugador conectado: {player.PlayerId}");

        // En Shared Mode cada cliente hace spawn de SU PROPIO personaje
        // (tiene la autoridad de estado sobre lo que crea), por eso se
        // compara contra runner.LocalPlayer.
        if (_playerPrefab != null && player == runner.LocalPlayer)
        {
            Vector3 spawnPosition = GetSpawnPosition(player.PlayerId);
            NetworkObject playerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            _spawnedPlayers[player] = playerObject;
        }

        OnPlayerCountChangedEvent?.Invoke(runner.SessionInfo != null ? runner.SessionInfo.PlayerCount : 0);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
        {
            if (playerObject != null && playerObject.HasStateAuthority)
            {
                runner.Despawn(playerObject);
            }

            _spawnedPlayers.Remove(player);
        }

        OnPlayerCountChangedEvent?.Invoke(runner.SessionInfo != null ? runner.SessionInfo.PlayerCount : 0);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();
        Vector2 direction = Vector2.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction += Vector2.up;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction += Vector2.down;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction += Vector2.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction += Vector2.right;

        data.MoveDirection = direction.normalized;
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        IsConnected = false;
        CurrentSessionName = null;
        _spawnedPlayers.Clear();
        Runner = null;
        OnDisconnectedEvent?.Invoke();
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        OnConnectionFailedEvent?.Invoke(reason.ToString());
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
