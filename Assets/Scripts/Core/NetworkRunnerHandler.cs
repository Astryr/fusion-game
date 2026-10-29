using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable CS0618

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkRunnerHandler Instance { get; private set; }

    [Header("Configuracion")]
    [Tooltip("Nombre del prefab del jugador, ubicado dentro de una carpeta Resources.")]
    [SerializeField] private string _playerPrefabResourcePath = "Player";

    [Tooltip("Nombre del prefab del director de partida (bananas + victoria), en una carpeta Resources.")]
    [SerializeField] private string _gameManagerPrefabResourcePath = "BananaGameManager";

    [Tooltip("Build Index de la escena de juego (debe estar registrada en Build Settings).")]
    [SerializeField] private int _gameSceneBuildIndex = 1;

    [Tooltip("Sesion por defecto si el campo de texto del menu queda vacio.")]
    [SerializeField] private string _defaultSessionName = "SalaPrincipal";

    [Tooltip("Nombre de jugador por defecto si el campo de texto del menu queda vacio.")]
    [SerializeField] private string _defaultNickname = "Jugador";

    public NetworkRunner Runner { get; private set; }
    public bool IsConnected { get; private set; }
    public string CurrentSessionName { get; private set; }
    public MiniGameId PendingMiniGame { get; private set; } = MiniGameId.BananaRain;
    public IReadOnlyList<SessionInfo> AvailableSessions => _availableSessions;

    public string LocalNickname { get; private set; }

    public event Action OnConnectingEvent;
    public event Action OnConnectedEvent;
    public event Action<string> OnConnectionFailedEvent;
    public event Action OnDisconnectedEvent;
    public event Action<int> OnPlayerCountChangedEvent;
    public event Action<List<SessionInfo>> OnSessionListUpdatedEvent;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private readonly List<SessionInfo> _availableSessions = new List<SessionInfo>();
    private NetworkObject _playerPrefab;
    private NetworkObject _gameManagerPrefab;
    private bool _gameManagerSpawnRequested;
    private NetworkRunner _browseRunner;
    private bool _browsing;
    private bool _browseStopping;

    // GetKeyDown dura un frame; OnInput de Fusion puede perderlo.
    private bool _jumpQueued;
    private bool _actionQueued;
    private string _disconnectMessage;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const int RttWindowSize = 60;
    private readonly float[] _rttWindow = new float[RttWindowSize];
    private int _rttWindowCount;
    private int _rttWindowIndex;
    private float _rttSampleTimer;
    private float _previousRttMs = -1f;
    private float _rttJitterMs;
    private bool _showNetworkDiagnostics = true;
    private GUIStyle _networkDiagnosticsStyle;
#endif

    public string ConsumeDisconnectMessage()
    {
        string message = _disconnectMessage;
        _disconnectMessage = null;
        return message;
    }

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

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UpdateNetworkDiagnostics();
        if (!GameChat.IsTyping && Input.GetKeyDown(KeyCode.F3))
        {
            _showNetworkDiagnostics = !_showNetworkDiagnostics;
        }
#endif

        if (GameChat.IsTyping)
        {
            _jumpQueued = false;
            _actionQueued = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            _jumpQueued = true;
            _actionQueued = true;
        }
    }

    public async Task StartBrowsingAsync()
    {
        if (_browsing || IsConnected)
        {
            return;
        }

        _browsing = true;
        var host = new GameObject("BrowseRunner");
        host.transform.SetParent(transform, false);
        _browseRunner = host.AddComponent<NetworkRunner>();
        _browseRunner.AddCallbacks(this);

        try
        {
            await _browseRunner.JoinSessionLobby(SessionLobby.Shared);
        }
        catch (Exception ex)
        {
            if (!_browseStopping && !IsBrowseShutdown(ex))
            {
                Debug.LogWarning("[NetworkRunnerHandler] No se pudo listar salas: " + ex.Message);
                OnConnectionFailedEvent?.Invoke("No se pudo entrar al lobby de salas.");
            }

            if (!IsConnected)
            {
                StopBrowsing();
            }
        }
    }

    public void StopBrowsing()
    {
        _ = StopBrowsingAsync();
    }

    public async Task StopBrowsingAsync()
    {
        _browseStopping = true;
        _browsing = false;
        if (_browseRunner == null)
        {
            _browseStopping = false;
            return;
        }

        NetworkRunner runner = _browseRunner;
        _browseRunner = null;
        runner.RemoveCallbacks(this);
        try
        {
            await runner.Shutdown();
        }
        catch (Exception)
        {
            // Shutdown en quit tira ObjectDisposedException.
        }

        _browseStopping = false;
        if (runner != null)
        {
            Destroy(runner.gameObject);
        }
    }

    public Task<bool> CreateSessionAsync(string sessionName, string nickname, MiniGameId miniGame)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            sessionName = "Sala" + UnityEngine.Random.Range(1000, 9999);
        }

        PendingMiniGame = miniGame;
        return ConnectInternalAsync(sessionName.Trim(), nickname, createIfMissing: true, miniGame);
    }

    public Task<bool> JoinSessionAsync(string sessionName, string nickname)
    {
        return ConnectInternalAsync(sessionName, nickname, createIfMissing: false, MiniGameId.BananaRain);
    }

    public Task<bool> ConnectAsync(string sessionName, string nickname)
    {
        return CreateSessionAsync(sessionName, nickname, PendingMiniGame);
    }

    private async Task<bool> ConnectInternalAsync(string sessionName, string nickname, bool createIfMissing, MiniGameId miniGame)
    {
        if (Runner != null)
        {
            Debug.LogWarning("[NetworkRunnerHandler] Ya hay una conexion activa.");
            return false;
        }

        await StopBrowsingAsync();

        sessionName = string.IsNullOrWhiteSpace(sessionName) ? _defaultSessionName : sessionName.Trim();
        LocalNickname = string.IsNullOrWhiteSpace(nickname) ? _defaultNickname : nickname.Trim();
        PendingMiniGame = miniGame;

        OnConnectingEvent?.Invoke();

        _playerPrefab = Resources.Load<NetworkObject>(_playerPrefabResourcePath);
        if (_playerPrefab == null)
        {
            Debug.LogError($"[NetworkRunnerHandler] No se encontro '{_playerPrefabResourcePath}' dentro de una carpeta Resources. " +
                            "Revisa SETUP.md -> 'Crear el prefab del jugador'.");
        }

        _gameManagerPrefab = Resources.Load<NetworkObject>(_gameManagerPrefabResourcePath);
        if (_gameManagerPrefab == null)
        {
            Debug.LogError($"[NetworkRunnerHandler] No se encontro '{_gameManagerPrefabResourcePath}' dentro de una carpeta Resources.");
        }

        _gameManagerSpawnRequested = false;

        Runner = gameObject.AddComponent<NetworkRunner>();
        Runner.ProvideInput = true;
        Runner.AddCallbacks(this);

        var sceneRef = SceneRef.FromIndex(_gameSceneBuildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (sceneRef.IsValid)
        {
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);
        }

        var properties = new Dictionary<string, SessionProperty>
        {
            { MiniGameNames.SessionPropertyKey, (int)miniGame },
        };

        StartGameResult result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            PlayerCount = MiniGameNames.MaxPlayers,
            IsVisible = true,
            IsOpen = true,
            EnableClientSessionCreation = createIfMissing,
            SessionProperties = properties,
            Scene = sceneInfo,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
        });

        if (result.Ok)
        {
            CurrentSessionName = Runner.SessionInfo != null ? Runner.SessionInfo.Name : sessionName;
            IsConnected = true;
            OnConnectedEvent?.Invoke();
            return true;
        }

        bool expected = result.ShutdownReason == ShutdownReason.GameClosed
                        || result.ShutdownReason == ShutdownReason.GameIsFull
                        || result.ShutdownReason == ShutdownReason.GameNotFound
                        || result.ShutdownReason == ShutdownReason.GameIdAlreadyExists;
        string failure = $"[NetworkRunnerHandler] Fallo la conexion: {result.ShutdownReason}";
        if (expected)
        {
            Debug.LogWarning(failure);
        }
        else
        {
            Debug.LogError(failure);
        }

        OnConnectionFailedEvent?.Invoke(HumanizeShutdown(result.ShutdownReason));
        DestroyRunner(Runner);
        Runner = null;
        return false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void UpdateNetworkDiagnostics()
    {
        if (Runner == null || !Runner.IsRunning)
        {
            return;
        }

        _rttSampleTimer -= Time.unscaledDeltaTime;
        if (_rttSampleTimer > 0f)
        {
            return;
        }

        _rttSampleTimer = 0.5f;
        float rttMs = (float)(Runner.GetPlayerRtt(PlayerRef.None) * 1000d);
        if (rttMs <= 0f)
        {
            return;
        }

        if (_previousRttMs >= 0f)
        {
            float delta = Mathf.Abs(rttMs - _previousRttMs);
            _rttJitterMs += (delta - _rttJitterMs) * 0.125f;
        }

        _previousRttMs = rttMs;
        _rttWindow[_rttWindowIndex] = rttMs;
        _rttWindowIndex = (_rttWindowIndex + 1) % RttWindowSize;
        _rttWindowCount = Mathf.Min(_rttWindowCount + 1, RttWindowSize);
    }

    private void OnGUI()
    {
        if (!_showNetworkDiagnostics || Runner == null || !Runner.IsRunning)
        {
            return;
        }

        float sum = 0f;
        float min = float.MaxValue;
        float max = 0f;
        for (int i = 0; i < _rttWindowCount; i++)
        {
            float sample = _rttWindow[i];
            sum += sample;
            min = Mathf.Min(min, sample);
            max = Mathf.Max(max, sample);
        }

        string stats = _rttWindowCount == 0
            ? "RTT: esperando muestras..."
            : $"RTT ms  prom {sum / _rttWindowCount:0} | min {min:0} | max {max:0}\nVariacion RTT: {_rttJitterMs:0.0} ms";
        int feedbackRpcCount = BananaGameManager.Instance != null
            ? BananaGameManager.Instance.FeedbackRpcCount
            : 0;
        stats += $"\nRPC de feedback recibidos: {feedbackRpcCount}";
        if (_networkDiagnosticsStyle == null)
        {
            _networkDiagnosticsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = Color.white },
            };
        }

        Rect panel = new Rect(12f, 12f, 430f, 102f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.035f, 0.055f, 0.075f, 0.96f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = previousColor;
        GUI.Label(new Rect(24f, 18f, 406f, 90f), $"RED - DIAGNOSTICO (F3)\n{stats}", _networkDiagnosticsStyle);
    }
#endif

    private void DestroyRunner(NetworkRunner runner)
    {
        if (runner == null)
        {
            return;
        }

        var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager != null)
        {
            Destroy(sceneManager);
        }

        Destroy(runner);
    }

    private static string HumanizeShutdown(ShutdownReason reason)
    {
        return reason switch
        {
            ShutdownReason.GameIsFull => "La sala esta llena (maximo 4).",
            ShutdownReason.GameNotFound => "No se encontro esa sala.",
            ShutdownReason.GameClosed => "La partida ya empezo. Proba de nuevo cuando termine.",
            ShutdownReason.GameIdAlreadyExists => "Ya existe una sala con ese nombre.",
            ShutdownReason.MaxCcuReached => "Photon llego al limite de jugadores simultaneos (CCU).",
            ShutdownReason.InvalidRegion => "La region de Photon configurada no es valida.",
            ShutdownReason.PhotonCloudTimeout or ShutdownReason.ConnectionTimeout or ShutdownReason.OperationTimeout
                => "Se perdio la conexion con Photon.",
            ShutdownReason.DisconnectedByPluginLogic => "Photon corto la conexion.",
            _ => reason.ToString(),
        };
    }

    public void Disconnect()
    {
        Runner?.Shutdown();
    }

    public static Vector3 GetSpawnPosition(int playerIndex)
    {
        const int slots = 4;
        float spread = BananaRushConfig.PlayerClampX * 0.85f;

        int index = ((playerIndex % slots) + slots) % slots;
        float t = index / (float)(slots - 1);
        float x = Mathf.Lerp(-spread, spread, t);

        return new Vector3(x, BananaRushConfig.GroundTopY, 0f);
    }

    private void TrySpawnGameManager(NetworkRunner runner)
    {
        if (_gameManagerSpawnRequested || _gameManagerPrefab == null)
        {
            return;
        }

        if (!runner.IsSharedModeMasterClient)
        {
            return;
        }

        _gameManagerSpawnRequested = true;
        runner.Spawn(_gameManagerPrefab, Vector3.zero, Quaternion.identity, inputAuthority: null,
            onBeforeSpawned: null, flags: NetworkSpawnFlags.SharedModeStateAuthMasterClient);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!IsGameRunner(runner))
        {
            return;
        }

        // Shared Mode: cada cliente spawnea su propio Player.
        if (player == runner.LocalPlayer && !_spawnedPlayers.ContainsKey(player))
        {
            if (_playerPrefab != null)
            {
                Vector3 spawnPosition = GetSpawnPosition(player.PlayerId);
                NetworkObject playerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
                _spawnedPlayers[player] = playerObject;
            }

            TrySpawnGameManager(runner);
        }

        OnPlayerCountChangedEvent?.Invoke(runner.SessionInfo != null ? runner.SessionInfo.PlayerCount : 0);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!IsGameRunner(runner))
        {
            return;
        }

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
        if (!IsGameRunner(runner))
        {
            return;
        }

        var data = new NetworkInputData();

        bool typing = GameChat.IsTyping;
        float horizontal = 0f;
        if (!typing)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        }

        // Solo Space; el buffer se consume aca.
        var buttons = default(NetworkButtons);
        buttons.Set(InputButton.Jump, _jumpQueued && !typing);
        buttons.Set(InputButton.Action, _actionQueued && !typing);
        _jumpQueued = false;
        _actionQueued = false;

        data.Horizontal = horizontal;
        data.Buttons = buttons;
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (!IsGameRunner(runner))
        {
            return;
        }

        bool wasConnected = IsConnected;
        IsConnected = false;
        CurrentSessionName = null;
        _spawnedPlayers.Clear();
        GameChat.Clear();
        if (wasConnected && shutdownReason != ShutdownReason.Ok)
        {
            _disconnectMessage = "Te desconectaste: " + HumanizeShutdown(shutdownReason);
        }
        if (Runner == runner)
        {
            Runner = null;
        }

        if (runner != null)
        {
            Destroy(runner);
        }

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
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        if (runner != _browseRunner && !IsGameRunner(runner))
        {
            return;
        }
        _availableSessions.Clear();
        if (sessionList != null)
        {
            _availableSessions.AddRange(sessionList);
        }

        OnSessionListUpdatedEvent?.Invoke(_availableSessions);
    }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    private bool IsGameRunner(NetworkRunner runner)
    {
        return runner != null && runner == Runner && runner != _browseRunner;
    }

    private static bool IsBrowseShutdown(Exception ex)
    {
        if (ex == null)
        {
            return false;
        }

        string message = ex.ToString();
        return message.Contains("ApplicationQuit") || message.Contains("DisconnectException");
    }
}
